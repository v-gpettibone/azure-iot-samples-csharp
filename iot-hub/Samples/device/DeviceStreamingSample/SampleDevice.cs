// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging;
using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.Devices.Client.Samples
{
    public class SampleDevice
    {
        // Look for "HostName=", and then grab all the characters until just before the next semi-colon.
        private static readonly Regex s_hostNameRegex = new Regex("(?<=HostName=).*?(?=;)", RegexOptions.Compiled);

        private const int DelayAfterDeviceCreationSeconds = 0;
        private static readonly SemaphoreSlim s_semaphore = new SemaphoreSlim(1, 1);

        private static ILogger _logger;
        private static string _iotHubConnectionString;

        private SampleDevice(Device device, IAuthenticationMethod authenticationMethod)
        {
            Device = device;
            AuthenticationMethod = authenticationMethod;
        }

        /// <summary>
        /// Factory method.
        /// </summary>
        /// <param name="namePrefix">The prefix to apply to your device name</param>
        /// <param name="type">The way the device will authenticate</param>
        public static async Task<SampleDevice> GetTestDeviceAsync(ILogger logger, string namePrefix, string iotHubConnectionString)
        {
            _iotHubConnectionString = iotHubConnectionString;
            _logger = logger;

            try
            {
                await s_semaphore.WaitAsync().ConfigureAwait(false);
                SampleDevice ret = await CreateDeviceAsync(namePrefix).ConfigureAwait(false);

                _logger.LogDebug($"{nameof(GetTestDeviceAsync)}: Using device {ret.Id}.");
                return ret;
            }
            finally
            {
                s_semaphore.Release();
            }
        }

        private static async Task<SampleDevice> CreateDeviceAsync(string prefix)
        {
            string deviceName = prefix + Guid.NewGuid();

            // Delete existing devices named this way and create a new one.
            using var rm = RegistryManager.CreateFromConnectionString(_iotHubConnectionString);
            _logger.LogInformation($"{nameof(GetTestDeviceAsync)}: Creating device {deviceName} with type SAS.");

            IAuthenticationMethod auth = null;

            var requestDevice = new Device(deviceName);
            Device device = await rm.AddDeviceAsync(requestDevice).ConfigureAwait(false);

            _logger.LogInformation($"{nameof(GetTestDeviceAsync)}: Pausing for {DelayAfterDeviceCreationSeconds}s after device was created.");
            await Task.Delay(DelayAfterDeviceCreationSeconds * 1000).ConfigureAwait(false);

            await rm.CloseAsync().ConfigureAwait(false);

            return new SampleDevice(device, auth);
        }

        /// <summary>
        /// Used in conjunction with DeviceClient.CreateFromConnectionString()
        /// </summary>
        public string ConnectionString
        {
            get
            {
                string iotHubHostName = GetHostName(_iotHubConnectionString);
                return $"HostName={iotHubHostName};DeviceId={Device.Id};SharedAccessKey={Device.Authentication.SymmetricKey.PrimaryKey}";
            }
        }

        /// <summary>
        /// Used in conjunction with DeviceClient.Create()
        /// </summary>
        public string IoTHubHostName => GetHostName(_iotHubConnectionString);

        /// <summary>
        /// Device Id
        /// </summary>
        public string Id => Device.Id;

        /// <summary>
        /// Device identity object.
        /// </summary>
        public Device Device { get; private set; }

        public Client.IAuthenticationMethod AuthenticationMethod { get; private set; }

        public DeviceClient CreateDeviceClient(TransportType transport, ClientOptions options = default)
        {
            DeviceClient deviceClient = null;

            if (AuthenticationMethod == null)
            {
                deviceClient = DeviceClient.CreateFromConnectionString(ConnectionString, transport, options);
                _logger.LogInformation($"{nameof(CreateDeviceClient)}: Created {nameof(DeviceClient)} {Device.Id} from connection string: {transport}");
            }
            else
            {
                deviceClient = DeviceClient.Create(IoTHubHostName, AuthenticationMethod, transport, options);
                _logger.LogInformation($"{nameof(CreateDeviceClient)}: Created {nameof(DeviceClient)} {Device.Id} from IAuthenticationMethod: {transport}");
            }

            return deviceClient;
        }

        public DeviceClient CreateDeviceClient(ITransportSettings[] transportSettings, ClientOptions options = default)
        {
            DeviceClient deviceClient = null;

            if (AuthenticationMethod == null)
            {
                deviceClient = DeviceClient.CreateFromConnectionString(ConnectionString, transportSettings, options);
                _logger.LogInformation($"{nameof(CreateDeviceClient)}: Created {nameof(DeviceClient)} {Device.Id} from device connection string");
            }
            else
            {
                deviceClient = DeviceClient.Create(IoTHubHostName, AuthenticationMethod, transportSettings, options);
                _logger.LogInformation($"{nameof(CreateDeviceClient)}: Created {nameof(DeviceClient)} {Device.Id} from IAuthenticationMethod");
            }

            return deviceClient;
        }

        public async Task RemoveDeviceAsync()
        {
            using var rm = RegistryManager.CreateFromConnectionString(_iotHubConnectionString);
            await rm.RemoveDeviceAsync(Id).ConfigureAwait(false);
        }

        private static string GetHostName(string iotHubConnectionString)
        {
            return s_hostNameRegex.Match(iotHubConnectionString).Value;
        }
    }
}
