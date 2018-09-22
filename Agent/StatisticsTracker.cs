// ---------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ---------------------------------------------------------------

namespace Agent
{
    using System;
    using System.Diagnostics;
    using System.Threading;

    public class StatisticsTracker
    {
        AgentStatistics stats;
        Stopwatch stopwatch;

        public StatisticsTracker()
        {
            stats = new AgentStatistics();
            stopwatch = Stopwatch.StartNew();
        }

        public void Increment()
        {
            Interlocked.Increment(ref stats.Count);
        }

        public void UpdateAverageLatency(double val)
        {
            stats.AverageLatency = ((stats.AverageLatency * (stats.Count - 1)) + val) / stats.Count;
        }

        public void FailureIncrement()
        {
            Interlocked.Increment(ref stats.FailureCount);
        }

        public AgentStatistics Reset()
        {
            Stopwatch prevWatch = Interlocked.Exchange(ref stopwatch, Stopwatch.StartNew());
            AgentStatistics prevStats = Interlocked.Exchange(ref stats, new AgentStatistics());

            prevStats.Interval = prevWatch.Elapsed;
            return prevStats;
        }
    }
}
