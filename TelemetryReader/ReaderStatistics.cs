// ---------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ---------------------------------------------------------------

namespace TelemetryReader
{
    using System;
    using System.Diagnostics;

    public class ReaderStatistics
    {
        public long Count;
        public TimeSpan Interval;
        public double AverageE2ELatency;
        public double AverageIoTHubLatency;
        public double NintyPercentileE2ELatency;
    }
}
