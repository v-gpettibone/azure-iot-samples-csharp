// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using CommandLine;
using Microsoft.Azure.Devices.Logging;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace Microsoft.Azure.Devices.Client.Samples
{
    public class Program
    {
        /// <summary>
        /// A sample to illustrate device streaming.
        /// </summary>
        /// <param name="args">
        /// Run with `--help` to see a list of required and optional parameters.
        /// </param>
        public static async Task<int> Main(string[] args)
        {
            // Parse application parameters
            Parameters parameters = null;
            ParserResult<Parameters> result = Parser.Default.ParseArguments<Parameters>(args)
                .WithParsed(parsedParams =>
                {
                    parameters = parsedParams;
                })
                .WithNotParsed(errors =>
                {
                    Environment.Exit(1);
                });

            // Set up logging
            ILoggerFactory loggerFactory = new LoggerFactory();
            loggerFactory.AddColorConsoleLogger(
                new ColorConsoleLoggerConfiguration
                {
                    MinLogLevel = LogLevel.Debug,
                });
            var logger = loggerFactory.CreateLogger<Program>();

            const string SdkEventProviderPrefix = "Microsoft-Azure-";
            // Instantiating this seems to do all we need for outputting SDK events to our console log
            _ = new ConsoleEventListener(SdkEventProviderPrefix, logger);

            using var serviceClient = ServiceClient.CreateFromConnectionString(parameters.IotHubConnectionString);
            using var deviceClient = DeviceClient.CreateFromConnectionString(parameters.DeviceConnectionString, parameters.TransportType);
            string deviceId = IotHubConnectionStringBuilder.Create(parameters.DeviceConnectionString).DeviceId;

            logger.LogDebug($"Using deviceId={deviceId}, transport={parameters.TransportType}");

            var sample = new DeviceStreamSample(serviceClient, deviceId, deviceClient, logger);
            await sample.RunSampleAsync();

            Console.WriteLine("Done.");
            return 0;
        }
    }
}
