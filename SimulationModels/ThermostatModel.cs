// ---------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ---------------------------------------------------------------

namespace SimulationModels
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Shared;
    using Newtonsoft.Json;

    public class ThermostatModel:IModel
    {
        const string targetTempProperty = "targetTempProperty";
        const string firmwareProperty = "Firmware";
        const string desiredFirmwareProperty = "DesiredFirmware";
        const string colorPaletteProperty = "colorPalette";
        const string StandByStatus = "StandBy";

        private const string HeatingStatus = "Heating";
        private const string CoolingStatus = "Cooling";
        private const string MessageSchema = "temperatureSchema";
        private const string MessageTemplate = "{\"temperature\":${temperature}}";
        private const string SupportedMethodsProperty = "SupportedMethods";
        private const string TelemetryProperty = "Telemetry";

        public string DeviceType => "Thermostat";

        public object TelemetrySchema => new
        {
            TemperatureSchema = new
            {
                Interval = "00:00:01",
                MessageTemplate = ThermostatModel.MessageTemplate,
                MessageSchema = new
                {
                    Name = MessageSchema,
                    Format = "JSON",
                    Fields = new
                    {
                        temperature = "Double"
                    }
                }
            }
        };

        public IEnumerable<(string, MethodCallback)> SupportedMethods => new (string, MethodCallback)[] { (nameof(AirConditioning), AirConditioning), (nameof(IncrementCloud), IncrementCloud) , (nameof(DecrementCloud), DecrementCloud) };
        public IEnumerable<(string, Object)> InitialProperties => new (string, Object)[0];

        public DesiredPropertyUpdateCallback DesiredPropertyUpdateCallbackProperty => DesiredPropertyUpdateCallback;

        public Message GetTelemetryMessage(Random randomGenerator)
        {
            var telemetryDataPoint = new
            {
                temperature = randomGenerator.Next(7, 50)
            };
            var messageString = JsonConvert.SerializeObject(telemetryDataPoint);
            var message = new Message(Encoding.UTF8.GetBytes(messageString));
            message.Properties.Add("$$CreationTimeUtc", DateTime.UtcNow.ToString());
            message.Properties.Add("$$MessageSchema", MessageSchema);
            message.Properties.Add("$$ContentType", "JSON");
            message.Properties.Add("$ThresholdExceeded", telemetryDataPoint.temperature > 25 ? "True" : "False");
            return message;
        }

        public TwinCollection GetReportedPropertyUpdate(Random randomGenerator)
        {
            TwinCollection properties = new TwinCollection();
            properties[targetTempProperty] = randomGenerator.Next(7, 50);
            return properties;
        }

        Task<MethodResponse> AirConditioning(MethodRequest methodRequest, object userContext)
        {
            return Task.FromResult(new MethodResponse(200));
        }

        Task<MethodResponse> IncrementCloud(MethodRequest methodRequest, object userContext)
        {
            return Task.FromResult(new MethodResponse(200));
        }

        Task<MethodResponse> DecrementCloud(MethodRequest methodRequest, object userContext)
        {
            return Task.FromResult(new MethodResponse(200));
        }

        Task DesiredPropertyUpdateCallback(TwinCollection desiredProperties, object userContext)
        {
            return Task.FromResult(true);
        }

      }
}
