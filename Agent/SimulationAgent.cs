using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
using Newtonsoft.Json;

namespace Agent
{
    public class SimulationAgent
    {
        StatisticsTracker tracker;
        int intervalAdjustment = 10;
        private DeviceClient client;
        private string DeviceId = "";
        private string deviceConnectionString;

        public SimulationAgent(string connectionString)
        {
            deviceConnectionString = connectionString;
            client = DeviceClient.CreateFromConnectionString(deviceConnectionString, TransportType.Amqp);
            var connectionStringBuilder = IotHubConnectionStringBuilder.Create(deviceConnectionString);
            DeviceId = connectionStringBuilder.DeviceId;
        }

        //public async Task Initialize()
        //{
        //    var twin = await client.GetTwinAsync();

        //    TwinCollection properties = new TwinCollection();
        //    properties[transportTypeProperty] = this.transportType.ToString();
        //    properties[typeProperty] = DeviceType;
        //    properties[SupportedMethodsProperty] = nameof(AirConditioning) + "," + nameof(IncrementCloud) + "," + nameof(DecrementCloud);
        //    properties[TelemetryProperty] = new
        //    {
        //        TemperatureSchema = new
        //        {
        //            Interval = "00:00:01",
        //            MessageTemplate = Thermostat.MessageTemplate,
        //            MessageSchema = new
        //            {
        //                Name = MessageSchema,
        //                Format = "JSON",
        //                Fields = new
        //                {
        //                    temperature = "Double"
        //                }
        //            }
        //        }
        //    };
        //    properties[firmwareProperty] = Firmware;
        //    try
        //    {
        //        await client.UpdateReportedPropertiesAsync(properties);
        //    }
        //    catch (Exception e)
        //    {
        //    }

        //}

        public async Task RunAsync(CancellationToken token, int ms)
        {
            tracker = new StatisticsTracker();
            int target = ms * intervalAdjustment;
            Stopwatch watch = Stopwatch.StartNew();
            while (!token.IsCancellationRequested)
            {
               await Task.Run(() => Work(watch));
            }
        }

        async Task SendTelemetry()
        {
                var telemetryDataPoint = new
                {
                    temperature = 1
                };
                var messageString = JsonConvert.SerializeObject(telemetryDataPoint);
                var message = new Message(Encoding.UTF8.GetBytes(messageString));
                message.Properties.Add("$$CreationTimeUtc", DateTime.UtcNow.ToString());
               // message.Properties.Add("$$MessageSchema", MessageSchema);
                message.Properties.Add("$$ContentType", "JSON");
                message.Properties.Add("$ThresholdExceeded", telemetryDataPoint.temperature > 25 ? "True" : "False");
                await client.SendEventAsync(message);
        }

        public async Task Work(Stopwatch watch)
        {
            try
            {
                watch.Reset();
                watch.Start();
               await SendTelemetry();
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
