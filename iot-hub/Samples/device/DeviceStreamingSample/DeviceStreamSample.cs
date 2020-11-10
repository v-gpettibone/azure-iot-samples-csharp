// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Samples.Common;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.Devices.Client.Samples
{
    public class DeviceStreamSample
    {
        private readonly ServiceClient _serviceClient;
        private readonly SampleDevice _sampleDevice;
        private readonly DeviceClient _deviceClient;
        private readonly ILogger _logger;

        public DeviceStreamSample(ServiceClient serviceClient, SampleDevice sampleDevice, TransportType transportType, ILogger logger)
        {
            _logger = logger;

            _serviceClient = serviceClient;
            _sampleDevice = sampleDevice;
            _deviceClient = sampleDevice.CreateDeviceClient(transportType);

            _deviceClient.SetConnectionStatusChangesHandler((status, reason) =>
            {
                logger.LogDebug($">>> Connection status changed: status={status}, reason={reason}");
            });
        }

        public async Task RunSampleAsync()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(30));

            Task<DeviceStreamRequest> clientRequestTask = _deviceClient.WaitForDeviceStreamRequestAsync(cts.Token);
            Task<Devices.DeviceStreamResponse> serviceRequestTask = _serviceClient.CreateStreamAsync(_sampleDevice.Id, new Devices.DeviceStreamRequest("blah"));

            DeviceStreamRequest clientRequest = await clientRequestTask;

            if (clientRequest != null)
            {
                _logger.LogInformation($"Device streaming request received " +
                    $"(name={clientRequest.Name}; " +
                    $"uri={clientRequest.Uri}; " +
                    $"authToken={clientRequest.AuthorizationToken})");

                await _deviceClient.AcceptDeviceStreamRequestAsync(clientRequest, cts.Token);

                Devices.DeviceStreamResponse serviceResponse = await serviceRequestTask;

                _logger.LogInformation($"Device streaming response received " +
                    $"(name={serviceResponse.StreamName}; " +
                    $"accepted={serviceResponse.IsAccepted}; " +
                    $"uri={serviceResponse.Uri}; " +
                    $"authToken={serviceResponse.AuthorizationToken})");

                _logger.LogInformation("Now testing if we can echo information through the streaming gateway");

                Task<ClientWebSocket> deviceWSClientTask = DeviceStreamingCommon
                    .GetStreamingClientAsync(clientRequest.Uri, clientRequest.AuthorizationToken, cts.Token);
                Task<ClientWebSocket> serviceWSClientTask = DeviceStreamingCommon
                    .GetStreamingClientAsync(serviceResponse.Uri, serviceResponse.AuthorizationToken, cts.Token);

                await Task.WhenAll(deviceWSClientTask, serviceWSClientTask).ConfigureAwait(false);

                ClientWebSocket deviceWSClient = deviceWSClientTask.Result;
                ClientWebSocket serviceWSClient = serviceWSClientTask.Result;

                byte[] serviceBuffer = Encoding.ASCII.GetBytes("This is a test message !!!@#$@$423423\r\n");
                byte[] clientBuffer = new byte[serviceBuffer.Length];

                await Task
                    .WhenAll(
                        serviceWSClient.SendAsync(new ArraySegment<byte>(serviceBuffer), WebSocketMessageType.Binary, true, cts.Token),
                        deviceWSClient.ReceiveAsync(new ArraySegment<byte>(clientBuffer), cts.Token).ContinueWith((wsrr) =>
                        {
                            _logger.LogInformation($"Received stream data by device ws client: {Encoding.UTF8.GetString(clientBuffer)}");
                        }, TaskScheduler.Current))
                    .ConfigureAwait(false);

                await Task
                    .WhenAll(
                        deviceWSClient.SendAsync(new ArraySegment<byte>(clientBuffer), WebSocketMessageType.Binary, true, cts.Token),
                        serviceWSClient.ReceiveAsync(new ArraySegment<byte>(serviceBuffer), cts.Token).ContinueWith((wsrr) =>
                        {
                            _logger.LogInformation($"Received stream data by service ws client: {Encoding.UTF8.GetString(serviceBuffer)}");
                        }, TaskScheduler.Current))
                    .ConfigureAwait(false);

                await Task
                    .WhenAll(
                        deviceWSClient.CloseAsync(WebSocketCloseStatus.NormalClosure, "End of test", cts.Token),
                        serviceWSClient.CloseAsync(WebSocketCloseStatus.NormalClosure, "End of test", cts.Token))
                    .ConfigureAwait(false);

                deviceWSClient.Dispose();
                serviceWSClient.Dispose();

                _deviceClient.Dispose();
                _serviceClient.Dispose();
            }
        }
    }
}
