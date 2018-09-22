using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Devices;

namespace IotHubServer
{
    public class DeviceManager
    {
        public static async Task<string> CreateDevice(string hubConnectionString, string deviceId)
        {
            IotHubConnectionStringBuilder connection = IotHubConnectionStringBuilder.Create(hubConnectionString);
            RegistryManager registryManager = RegistryManager.CreateFromConnectionString(connection.ToString());

            Device device;
            try
            {
                device = await registryManager.AddDeviceAsync(new Device(deviceId));
            }
            catch (Exception)
            {
                device = await registryManager.GetDeviceAsync(deviceId);
            }

            return "HostName=" + connection.HostName + ";DeviceId=" + deviceId + ";SharedAccessKey=" + device.Authentication.SymmetricKey.PrimaryKey;
        }

        public static async Task<IEnumerable<string>> CreateDevices(string hubConnectionString, IEnumerable<string> deviceIds)
        {
            if(deviceIds.Count() <= 1)
            {
                return new string[] { await CreateDevice(hubConnectionString, deviceIds.FirstOrDefault()) };
            }

            IotHubConnectionStringBuilder connection = IotHubConnectionStringBuilder.Create(hubConnectionString);
            RegistryManager registryManager = RegistryManager.CreateFromConnectionString(connection.ToString());

            var tempIds = deviceIds;
            do
            {
                await registryManager.AddDevices2Async(tempIds.Take(100).Select(deviceId => new Device(deviceId)));
                tempIds = tempIds.Skip(100);
            } while (tempIds.Any());

            ConcurrentBag < Device > devices = new ConcurrentBag<Device>();

            tempIds = deviceIds;

            do
            {
                await Task.WhenAll(tempIds.Take(50).Select(x => AddDevice(devices, x, registryManager)));
                tempIds = tempIds.Skip(50);
            } while (tempIds.Any());
            
            
            return devices.Select(x=>"HostName=" + connection.HostName + ";DeviceId=" + x.Id + ";SharedAccessKey=" + x.Authentication.SymmetricKey.PrimaryKey);
        }

        private static async Task AddDevice(ConcurrentBag<Device> devices, string deviceId, RegistryManager registryManager)
        {
            int retryCount = 0;
            bool failed = false;

            do
            {
                try
                {
                    devices.Add(await registryManager.GetDeviceAsync(deviceId));
                }
                catch(Exception){
                    failed = true;
                    if (retryCount++ > 3)
                    {
                        throw;
                    }
                }
                
            } while (failed);

        }
    }
}
