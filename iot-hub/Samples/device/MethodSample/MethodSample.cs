// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Threading;
using System.Diagnostics;

namespace Microsoft.Azure.Devices.Client.Samples
{
    public class MethodSample
    {
        private readonly TimeSpan _sleepTime = TimeSpan.FromSeconds(10);
        private readonly DeviceClient _deviceClient;

        private class DeviceData
        {
            [JsonPropertyName("name")]
            public string Name { get; set; }
        }

        public MethodSample(DeviceClient deviceClient)
        {
            _deviceClient = deviceClient ?? throw new ArgumentNullException(nameof(deviceClient));
        }

        public async Task RunSampleAsync()
        {
            _deviceClient.SetConnectionStatusChangesHandler(ConnectionStatusChangeHandler);

            // Method Call processing will be enabled when the first method handler is added.
            // Setup a callback for the 'WriteToConsole' method.
            await _deviceClient.SetMethodHandlerAsync("WriteToConsole", WriteToConsoleAsync, null);

            // Setup a callback for the 'GetDeviceName' method.
            await _deviceClient.SetMethodHandlerAsync(
                "GetDeviceName",
                GetDeviceNameAsync,
                new DeviceData { Name = "DeviceClientMethodSample" });

            PrintLogWithTime("Press Control+C to quit the sample.\n");
            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (sender, eventArgs) =>
            {
                eventArgs.Cancel = true;
                cts.Cancel();
                PrintLogWithTime("Sample execution cancellation requested; will exit.");
            };

            var waitTime = TimeSpan.FromMinutes(5);
            var timer = Stopwatch.StartNew();
            PrintLogWithTime("Use the IoT Hub Azure Portal to call methods GetDeviceName or WriteToConsole within this time.");

            PrintLogWithTime($"Waiting up to {waitTime} for IoT Hub method calls ...");
            while (!cts.IsCancellationRequested
                && timer.Elapsed < waitTime)
            {
                await Task.Delay(1000);
            }
        }

        private void ConnectionStatusChangeHandler(ConnectionStatus status, ConnectionStatusChangeReason reason)
        {
            PrintLogWithTime($"Connection status changed: status={status}, reason={reason}.\n");
        }

        private async Task<MethodResponse> WriteToConsoleAsync(MethodRequest methodRequest, object userContext)
        {
            PrintLogWithTime($"*** {methodRequest.Name} was called.");

            PrintLogWithTime($"Now sleeping for {_sleepTime} from {nameof(WriteToConsoleAsync)}");
            await Task.Delay(_sleepTime);
            PrintLogWithTime($"Done sleeping from {nameof(WriteToConsoleAsync)}");

            PrintLogWithTime($"Exiting {nameof(WriteToConsoleAsync)}");
            return new MethodResponse(new byte[0], 200);
        }

        private async Task<MethodResponse> GetDeviceNameAsync(MethodRequest methodRequest, object userContext)
        {
            PrintLogWithTime($"*** {methodRequest.Name} was called.");

            PrintLogWithTime($"Now sleeping for {_sleepTime} from {nameof(GetDeviceNameAsync)}");
            await Task.Delay(_sleepTime);
            PrintLogWithTime($"Done sleeping from {nameof(GetDeviceNameAsync)}");

            MethodResponse retValue;
            if (userContext == null)
            {
                retValue = new MethodResponse(new byte[0], 500);
            }
            else
            {
                var deviceData = (DeviceData)userContext;
                string result = JsonSerializer.Serialize(deviceData);
                retValue = new MethodResponse(Encoding.UTF8.GetBytes(result), 200);
            }

            PrintLogWithTime($"Exiting {nameof(GetDeviceNameAsync)}");
            return retValue;
        }

        internal static void PrintLogWithTime(string formattedMessage)
        {
            Console.WriteLine($"{DateTime.Now}> {formattedMessage}");
        }
    }
}
