// ---------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ---------------------------------------------------------------

namespace TelemetryReader
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Collections.Generic;

    public class StatisticsTracker
    {
        ReaderStatistics stats;
        Stopwatch stopwatch;
        List<double> dataPoints;

        public StatisticsTracker()
        {
            stats = new ReaderStatistics();
            dataPoints = new List<double>();
            stopwatch = Stopwatch.StartNew();
        }

        public void Increment()
        {
            Interlocked.Increment(ref stats.Count);
        }

        public void UpdateAverageE2ELatency(double val)
        {
            stats.AverageE2ELatency = ((stats.AverageE2ELatency * (stats.Count - 1)) + val) / stats.Count;
            dataPoints.Add(val);
        }

        public void UpdateAverageIoTHubLatency(double val)
        {
            stats.AverageIoTHubLatency = ((stats.AverageIoTHubLatency * (stats.Count - 1)) + val) / stats.Count;
        }

        public ReaderStatistics Reset()
        {
            Stopwatch prevWatch = Interlocked.Exchange(ref stopwatch, Stopwatch.StartNew());
            ReaderStatistics prevStats = Interlocked.Exchange(ref stats, new ReaderStatistics());
            var prevPoints = Interlocked.Exchange(ref dataPoints, new List<double>());
            prevStats.Interval = prevWatch.Elapsed;
            if (prevPoints.Count > 0)
            {
                prevStats.NintyPercentileE2ELatency = prevPoints[(int)(prevPoints.Count * 0.9)];
            }
            return prevStats;
        }
    }
}
