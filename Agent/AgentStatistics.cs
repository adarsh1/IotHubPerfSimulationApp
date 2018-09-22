// ---------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ---------------------------------------------------------------

namespace Agent
{
    using System;
    using System.Diagnostics;

    public class AgentStatistics
    {
        public long Count;
        public long FailureCount;
        public TimeSpan Interval;
        public double AverageLatency;
    }
}
