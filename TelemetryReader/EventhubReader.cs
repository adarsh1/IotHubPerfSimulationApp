using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.EventHubs;

namespace TelemetryReader
{
    public class EventhubReader
    {
        public static async Task<IEnumerable<EventhubReader>> CreateEventHubReaders(string eventhubCompatibleConnectionString, string hubName, string consumerGroup)
        {
            var connectionString = new EventHubsConnectionStringBuilder(eventhubCompatibleConnectionString)
            {
                EntityPath = hubName
            };

            var eventHubClient = EventHubClient.CreateFromConnectionString(connectionString.ToString());

            var runtimeInfo = await eventHubClient.GetRuntimeInformationAsync();

             return runtimeInfo.PartitionIds.Select(x => new EventhubReader(eventHubClient, consumerGroup, x));
        }

        private StatisticsTracker tracker;
        private PartitionReceiver receiver;
        private EventHubClient eventHubClient;
        private string partitonId;

        public EventhubReader(EventHubClient eventHubClient, string consumerGroup, string partitionId)
        {
            this.eventHubClient = eventHubClient;
            this.partitonId = partitionId;
            CreateReceiver(consumerGroup, this.partitonId);
        }

        private void CreateReceiver(string consumerGroup, string partitionId)
        {
            var oldClient = Interlocked.Exchange(ref this.receiver, this.eventHubClient.CreateReceiver(consumerGroup, partitionId, EventPosition.FromEnqueuedTime(DateTime.Now)));
        }

        public async Task RunAsync(CancellationToken token)
        {
           tracker = new StatisticsTracker();

            while (!token.IsCancellationRequested)
            {
                try
                {
                    var events = await receiver.ReceiveAsync(100);
                    var readTime = DateTime.UtcNow;
                    // If there is data in the batch, process it.
                    if (events == null) continue;

                    foreach (EventData eventData in events)
                    {
                        tracker.Increment();
                        var e2eLatency = readTime - (DateTime)eventData.SystemProperties["iothub-enqueuedtime"];
                        var iotHubLatency = (DateTime)eventData.SystemProperties["x-opt-enqueued-time"]- (DateTime)eventData.SystemProperties["iothub-enqueuedtime"];
                        tracker.UpdateAverageE2ELatency(e2eLatency.TotalMilliseconds);
                        tracker.UpdateAverageIoTHubLatency(iotHubLatency.TotalMilliseconds);
                    }
                }
                catch (Exception) { }
            }
        }

        public ReaderStatistics GetIntervalStats()
        {
            return tracker?.Reset();
        }
    }
}
