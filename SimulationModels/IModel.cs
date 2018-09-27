// ---------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ---------------------------------------------------------------

namespace SimulationModels
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Shared;

    public interface IModel
    {
        string DeviceType { get; }

        Object TelemetrySchema { get; }

        IEnumerable<(string, MethodCallback)> SupportedMethods { get; }
        IEnumerable<(string, Object)> InitialProperties { get; }

        Message GetTelemetryMessage(Random randomGenerator);

        TwinCollection GetReportedPropertyUpdate(Random randomGenerator);
    }
}
