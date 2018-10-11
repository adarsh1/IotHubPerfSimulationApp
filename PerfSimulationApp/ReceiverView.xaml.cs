using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using LiveCharts;
using LiveCharts.Configurations;
using LiveCharts.Wpf;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using SimulatedDevice;
using TelemetryReader;

namespace PerfSimulationApp
{
    /// <summary>
    /// Interaction logic for ReceiverView.xaml
    /// </summary>
    public partial class ReceiverView : UserControl,IDisposable
    {
        const int CHART_QUEUE_LENGTH = 25;
        const int AGENT_CREATION_BATCH_SIZE = 50;

        public SeriesCollection ReceiverThroughputCollection { get; set; }

        public string PartitionCount { get; set; }
        public long ReceiverTotalMessages { get; set; }

        public SeriesCollection ReceiverLatencyCollection { get; set; }

        public Func<double, string> ReceiverYFormatter { get; set; }
        public Func<double, string> ReceiverXFormatter { get; set; }

        SizeLimitedQueue<(double, DateTime)> DataPointQueue;
        SizeLimitedQueue<(double, DateTime)> E2ELatencyPointQueue;
        SizeLimitedQueue<(double, DateTime)> IotHubLatencyPointQueue;
        SizeLimitedQueue<(double, DateTime)> E2E90thLatencyPointQueue;

        private LineSeries e2eLatencySeries;
        private LineSeries e2e90thLatencySeries;
        private LineSeries iotHubLatencySeries;
        private LineSeries throughputSeries;

        private System.Timers.Timer aTimer;

        private List<EventhubReader> readerList;
        CancellationTokenSource tokenSource;
        TelemetryClient telemetryClient;


        public ReceiverView()
        {
            InitializeComponent();
            telemetryClient = new TelemetryClient();
            telemetryClient.Context.User.Id = Environment.UserName;
            telemetryClient.Context.Session.Id = Guid.NewGuid().ToString();
            telemetryClient.Context.Device.OperatingSystem = Environment.OSVersion.ToString();

            var dayConfig = Mappers.Xy<(double, DateTime)>()
            .X(dayModel => (double)dayModel.Item2.Ticks / TimeSpan.FromSeconds(1).Ticks)
            .Y(dayModel => dayModel.Item1);

            PartitionCount = "0";

            tokenSource = null;

            e2eLatencySeries = new LineSeries
            {
                Title = "AverageE2ELatency",
                Values = new ChartValues<(double, DateTime)> { }
            };

            e2e90thLatencySeries = new LineSeries
            {
                Title = "90thPercentileE2ELatency",
                Values = new ChartValues<(double, DateTime)> { }
            };

            iotHubLatencySeries = new LineSeries
            {
                Title = "AverageIoTHubLatency",
                Values = new ChartValues<(double, DateTime)> { }
            };

            throughputSeries = new LineSeries
            {
                Title = "Throughput",
                Values = new ChartValues<(double, DateTime)> { }
            };

            ReceiverThroughputCollection = new SeriesCollection(dayConfig)
            {
               throughputSeries
            };

            ReceiverLatencyCollection = new SeriesCollection(dayConfig)
            {
               e2eLatencySeries,
               e2e90thLatencySeries,
               iotHubLatencySeries
            };

            ReceiverYFormatter = value => value.ToString("G");
            ReceiverXFormatter = value => new DateTime((long)(value * TimeSpan.FromSeconds(1).Ticks)).ToString("HH:mm:ss");
            readerList = new List<EventhubReader>();
            SetTimer();
            this.DataContext = this;
        }

        private void SetTimer()
        {
            // Create a timer with a two second interval.
            aTimer = new System.Timers.Timer(1000);
            // Hook up the Elapsed event for the timer. 
            aTimer.Elapsed += OnTimedEvent;
            aTimer.AutoReset = true;
            aTimer.Enabled = true;
        }

        private void OnTimedEvent(Object source, ElapsedEventArgs e)
        {
            PartitionCount = readerList.Count + "";
            Dispatcher.Invoke((Action)(() => PartitionCountLabel.Text = PartitionCount));
            if (readerList.Any() && DataPointQueue != null)
            {
                double throughput = 0;
                double averageE2ELatencySum = 0;
                double averageIotHubLatencySum = 0;
                double ninetyE2ELatency = 0;

                var activeAgentCount = 0;
                foreach (var reader in readerList)
                {
                    var stats = reader.GetIntervalStats();

                    if (stats == null)
                    {
                        continue;
                    }

                    ReceiverTotalMessages += stats.Count;
                    throughput += stats.Count * 1000.0 / stats.Interval.TotalMilliseconds;
                    averageE2ELatencySum += stats.AverageE2ELatency;
                    averageIotHubLatencySum += stats.AverageIoTHubLatency;
                    ninetyE2ELatency = Math.Max(ninetyE2ELatency, stats.NintyPercentileE2ELatency);
                    activeAgentCount++;
                }

                if (activeAgentCount <= 0)
                    return;

                telemetryClient.TrackMetric(new MetricTelemetry("ReceiverThroughput", throughput));
                telemetryClient.TrackMetric(new MetricTelemetry("AverageEndToEndLatency", averageE2ELatencySum / activeAgentCount));
                telemetryClient.TrackMetric(new MetricTelemetry("90thPercentileEndToEndLatency", ninetyE2ELatency));
                telemetryClient.TrackMetric(new MetricTelemetry("AverageIoTHubLatency", averageIotHubLatencySum / activeAgentCount));

                DataPointQueue.Enqueue((throughput, DateTime.UtcNow));
                E2ELatencyPointQueue.Enqueue((averageE2ELatencySum / activeAgentCount, DateTime.UtcNow));
                E2E90thLatencyPointQueue.Enqueue((ninetyE2ELatency, DateTime.UtcNow));
                IotHubLatencyPointQueue.Enqueue((averageIotHubLatencySum / activeAgentCount, DateTime.UtcNow));

                var points = DataPointQueue.ToArray();
                throughputSeries.Values.Clear();
                throughputSeries.Values.AddRange(points.Select(x => (object)x));

                points = E2ELatencyPointQueue.ToArray();
                e2eLatencySeries.Values.Clear();
                e2eLatencySeries.Values.AddRange(points.Select(x => (object)x));

                points = E2E90thLatencyPointQueue.ToArray();
                e2e90thLatencySeries.Values.Clear();
                e2e90thLatencySeries.Values.AddRange(points.Select(x => (object)x));

                points = IotHubLatencyPointQueue.ToArray();
                iotHubLatencySeries.Values.Clear();
                iotHubLatencySeries.Values.AddRange(points.Select(x => (object)x));

                var totMsgString = ReceiverTotalMessages + "";
                Dispatcher.Invoke((Action)(() => TotalMessageBox.Text = totMsgString));
            }
        }

        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            tokenSource?.Cancel();
            tokenSource?.Dispose();
            tokenSource = new CancellationTokenSource();
            DataPointQueue = new SizeLimitedQueue<(double, DateTime)>(CHART_QUEUE_LENGTH);
            E2ELatencyPointQueue = new SizeLimitedQueue<(double, DateTime)>(CHART_QUEUE_LENGTH);
            E2E90thLatencyPointQueue = new SizeLimitedQueue<(double, DateTime)>(CHART_QUEUE_LENGTH);
            IotHubLatencyPointQueue = new SizeLimitedQueue<(double, DateTime)>(CHART_QUEUE_LENGTH);
            ReceiverTotalMessages = 0;
            //var agentCount = Convert.ToInt32(DeviceCountBox.Text);

            //TimeSpan? runDuration;
            //if (TimeSpan.TryParse(TimeSpanBox.Text, out TimeSpan time))
            //{
            //    runDuration = time;
            //}
            //else
            //{
            //    runDuration = null;
            //}

            //while (readerList.Count > agentCount)
            //{
            //    readerList.RemoveAt(readerList.Count - 1);
            //}


            //foreach (var agent in readerList)
            //{
            //    if (tokenSource.Token.IsCancellationRequested)
            //        return;

            //    Task.Run(() => agent.RunAsync(WorkLoadType, TransportType, tokenSource.Token, runDuration));
            //}

            //while (readerList.Count < agentCount)
            //{
              //  var connections = await DeviceManager.CreateDevices(HubConnectionStringBox.Text, Enumerable.Range(readerList.Count, Math.Min(AGENT_CREATION_BATCH_SIZE, agentCount - readerList.Count)).Select(x => "perfDevice" + (readerList.Count + x)));
            readerList.Clear();
            readerList.AddRange(await EventhubReader.CreateEventHubReaders(HubConnectionStringBox.Text, EventHubNameBox.Text, ConsumerGroupBox.Text));
                foreach (var reader in readerList)
                {
                    if (tokenSource.Token.IsCancellationRequested)
                        return;

                    Task.Run(() => reader.RunAsync(tokenSource.Token));
                }
            //}
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            tokenSource?.Cancel();
            tokenSource?.Dispose();
            tokenSource = null;
        }

        public void Dispose()
        {
           aTimer?.Stop();
           tokenSource?.Cancel();
            tokenSource?.Dispose();
            if (telemetryClient != null)
            {
                telemetryClient.Flush(); // only for desktop apps

                // Allow time for flushing:
                System.Threading.Thread.Sleep(1000);
            }
        }
    }
}
