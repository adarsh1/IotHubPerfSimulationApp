using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
using Newtonsoft.Json;
using SimulationModels;

namespace Agent
{
    public class SimulationAgent
    {
        IModel deviceModel;
        StatisticsTracker tracker;
        int intervalAdjustment = 10;
        private DeviceClient client;
        private string DeviceId = "";
        private string deviceConnectionString;
        private const string SupportedMethodsProperty = "SupportedMethods";
        private const string TelemetryProperty = "Telemetry";
        const string TypeProperty = "Type";
        const string TransportTypeProperty = "TransportType";
        static Random randomGenerator = new Random(Guid.NewGuid().GetHashCode());
        bool initialized;
        TransportType transportType;


        public SimulationAgent(string connectionString, TransportType transportTypeValue)
        {
            initialized = false;
            deviceConnectionString = connectionString;
            transportType = transportTypeValue;
            CreateClient();
            var connectionStringBuilder = IotHubConnectionStringBuilder.Create(deviceConnectionString);
            DeviceId = connectionStringBuilder.DeviceId;
            deviceModel = new ThermostatModel();
        }

        private void CreateClient()
        {
            var oldClient = Interlocked.Exchange(ref client, DeviceClient.CreateFromConnectionString(deviceConnectionString, transportType));
            oldClient?.Dispose();
        }

        public async Task Initialize()
        {
            var twin = await client.GetTwinAsync();
            await client.OpenAsync();
            TwinCollection properties = new TwinCollection();
            properties[TransportTypeProperty] = transportType.ToString();
            properties[TypeProperty] = deviceModel.DeviceType;
            properties[SupportedMethodsProperty] = String.Join(",", deviceModel.SupportedMethods.Select(x => x.Item1));
            properties[TelemetryProperty] = deviceModel.TelemetrySchema;
            foreach (var property in deviceModel.InitialProperties)
            {
                properties[property.Item1] = property.Item2;
            }

            await client.UpdateReportedPropertiesAsync(properties);

            foreach(var method in deviceModel.SupportedMethods)
            {
                await client.SetMethodHandlerAsync(method.Item1, method.Item2, null);
            }

            await client.SetDesiredPropertyUpdateCallbackAsync(deviceModel.DesiredPropertyUpdateCallbackProperty, null);

            initialized = true;

        }

        public async Task RunAsync(WorkLoadType workLoad, TransportType transportTypeValue, CancellationToken token, TimeSpan? time)
        {
            tracker = new StatisticsTracker();

            if (transportType != transportTypeValue)
            {
                transportType = transportTypeValue;
                CreateClient();
            }

            if (!initialized)
            {
                await Initialize();
            }

            DateTime target;

            if (time.HasValue)
            {
                target = DateTime.UtcNow + time.Value;
            }
            else
            {
                target = DateTime.MaxValue;
            }

            Stopwatch watch = Stopwatch.StartNew();

            Func<Stopwatch, Task> workLoadMethod;

            switch (workLoad)
            {
                case WorkLoadType.DeviceTelemetry:
                    workLoadMethod = SendTelemetry;
                    break;
                case WorkLoadType.ReportedPropertyUpdate:
                    workLoadMethod = UpdateReportedProperty;
                    break;
                default:
                    throw new InvalidOperationException();
            }


            while (!token.IsCancellationRequested && DateTime.UtcNow < target)
            {
                await Task.Run(() => Work(workLoadMethod,watch));
            }
        }

        async Task SendTelemetry(Stopwatch watch)
        {
            watch.Stop();
            var message = deviceModel.GetTelemetryMessage(randomGenerator);
            watch.Start();
            await client.SendEventAsync(message);
        }

        async Task UpdateReportedProperty(Stopwatch watch)
        {
            watch.Stop();
            var message = deviceModel.GetReportedPropertyUpdate(randomGenerator);
            watch.Start();
            await client.UpdateReportedPropertiesAsync(message);
        }

        public async Task Work(Func<Stopwatch, Task> method, Stopwatch watch)
        {
            try
            {
                watch.Reset();
                watch.Start();
                await method(watch);
                watch.Stop();
                tracker.Increment();
                tracker.UpdateAverageLatency(watch.ElapsedMilliseconds);
            }
            catch (Exception)
            {
                tracker.FailureIncrement();
            }

        }

        public AgentStatistics GetIntervalStats()
        {
            return tracker?.Reset();
        }
    }
}
