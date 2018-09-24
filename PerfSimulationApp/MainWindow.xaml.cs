using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
using Agent;
using IotHubServer;
using LiveCharts;
using LiveCharts.Configurations;
using LiveCharts.Wpf;
using SimulatedDevice;

namespace PerfSimulationApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        const int CHART_QUEUE_LENGTH = 25;
        const int AGENT_CREATION_BATCH_SIZE = 50;

        public SeriesCollection ThroughputCollection { get; set; }

        public string DeviceCount { get; set; }

        public SeriesCollection LatencyCollection { get; set; }

        public Func<double, string> YFormatter { get; set; }
        public Func<double, string> XFormatter { get; set; }

        SizeLimitedQueue<(double, DateTime)> DataPointQueue;
        SizeLimitedQueue<(double, DateTime)> FailurePointQueue;
        SizeLimitedQueue<(double, DateTime)> LatencyPointQueue;

        private LineSeries latencySeries;
        private LineSeries throughputSeries;
        private LineSeries failureSeries;

        private System.Timers.Timer aTimer;

        private List<SimulationAgent> agentList;
        CancellationTokenSource tokenSource;


        public MainWindow()
        {
            InitializeComponent();

            var dayConfig = Mappers.Xy<(double, DateTime)>()
            .X(dayModel => (double)dayModel.Item2.Ticks / TimeSpan.FromSeconds(1).Ticks)
            .Y(dayModel => dayModel.Item1);

            DeviceCount = "0";

            DataPointQueue = null;
            FailurePointQueue = null;

            tokenSource = null;

            latencySeries = new LineSeries
            {
                Title = "AverageLatency",
                Values = new ChartValues<(double, DateTime)> { }
            };

            throughputSeries = new LineSeries
            {
                Title = "Throughput",
                Values = new ChartValues<(double, DateTime)> { }
            };

            failureSeries = new LineSeries
            {
                Title = "Failures/Sec",
                Values = new ChartValues<(double, DateTime)> { }
            };

            ThroughputCollection = new SeriesCollection(dayConfig)
            {
               throughputSeries,
               failureSeries
            };

            LatencyCollection = new SeriesCollection(dayConfig)
            {
               latencySeries
            };

            YFormatter = value => value.ToString("G");
            XFormatter = value => new DateTime((long)(value * TimeSpan.FromSeconds(1).Ticks)).ToString("HH:mm:ss");
            agentList = new List<SimulationAgent>();
            SetTimer();
            this.DataContext = this;

            int maxWorker, maxIOC;
            ThreadPool.GetMaxThreads(out maxWorker, out maxIOC);
            // Change the minimum number of worker threads to four, but
            // keep the old setting for minimum asynchronous I/O 
            // completion threads.
            if (ThreadPool.SetMaxThreads(200, 200))
            {
                // The minimum number of threads was set successfully.
            }
            else
            {
                // The minimum number of threads was not changed.
            }
        }

        private void SetTimer()
        {
            // Create a timer with a two second interval.
            aTimer = new System.Timers.Timer(2000);
            // Hook up the Elapsed event for the timer. 
            aTimer.Elapsed += OnTimedEvent;
            aTimer.AutoReset = true;
            aTimer.Enabled = true;
        }

        private void OnTimedEvent(Object source, ElapsedEventArgs e)
        {
            DeviceCount = agentList.Count+"";
            Dispatcher.Invoke((Action)(() => DeviceCountLabel.Text = DeviceCount));
            if (agentList.Any() && DataPointQueue != null)
            {
                double throughput = 0;
                double failureThroughput = 0;
                double averageLatencySum = 0;

                var activeAgentCount = 0;
                foreach(var agent in agentList)
                {
                    var stats = agent.GetIntervalStats();

                    if (stats == null)
                    {
                        continue;
                    }

                    throughput += stats.Count * 1000.0 / stats.Interval.TotalMilliseconds;
                    failureThroughput += stats.FailureCount * 1000.0 / stats.Interval.TotalMilliseconds;
                    averageLatencySum += stats.AverageLatency;
                    activeAgentCount++;
                }

                if (activeAgentCount <= 0)
                    return;

                DataPointQueue.Enqueue((throughput, DateTime.UtcNow));
                FailurePointQueue.Enqueue((failureThroughput, DateTime.UtcNow));
                LatencyPointQueue.Enqueue((averageLatencySum / activeAgentCount, DateTime.UtcNow));

                var points = DataPointQueue.ToArray();
                throughputSeries.Values.Clear();
                throughputSeries.Values.AddRange(points.Select(x => (object)x));

                points = FailurePointQueue.ToArray();
                failureSeries.Values.Clear();
                failureSeries.Values.AddRange(points.Select(x => (object)x));

                points = LatencyPointQueue.ToArray();
                latencySeries.Values.Clear();
                latencySeries.Values.AddRange(points.Select(x => (object)x));
            }
        }

        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            tokenSource?.Cancel();
            tokenSource?.Dispose();
            tokenSource = new CancellationTokenSource();
            DataPointQueue = new SizeLimitedQueue<(double, DateTime)>(CHART_QUEUE_LENGTH);
            FailurePointQueue = new SizeLimitedQueue<(double, DateTime)>(CHART_QUEUE_LENGTH);
            LatencyPointQueue = new SizeLimitedQueue<(double, DateTime)>(CHART_QUEUE_LENGTH);

            var agentCount = Convert.ToInt32(DeviceCountBox.Text);

            TimeSpan? runDuration;
            if(TimeSpan.TryParse(TimeSpanBox.Text, out TimeSpan time))
            {
                runDuration = time;
            }
            else
            {
                runDuration = null;
            }

            while (agentList.Count > agentCount)
            {
                agentList.RemoveAt(agentList.Count - 1);
            }


            foreach (var agent in agentList)
            {
                if (tokenSource.Token.IsCancellationRequested)
                    return;

                Task.Run(() => agent.RunAsync(tokenSource.Token, runDuration));
            }

            while (agentList.Count < agentCount)
            {
                var connections = await DeviceManager.CreateDevices(HubConnectionStringBox.Text, Enumerable.Range(agentList.Count, Math.Min(AGENT_CREATION_BATCH_SIZE, agentCount - agentList.Count)).Select(x => "perfDevice" + (agentList.Count+x)));
                var agents = connections.Select(x => new SimulationAgent(x));
                foreach (var agent in agents)
                {
                    if (tokenSource.Token.IsCancellationRequested)
                        return;

                    Task.Run(() => agent.RunAsync(tokenSource.Token, runDuration));
                    agentList.Add(agent);
                }
            }
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            tokenSource?.Cancel();
            tokenSource?.Dispose();
            tokenSource = null;
        }

        private void HubConnectionStringBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = false;
        }

        private void DeviceCountBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9]+");

            e.Handled = regex.IsMatch(e.Text)&&Convert.ToInt32(e.Text)>0;
        }
    }
}
