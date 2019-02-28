﻿//
// Copyright (c) Microsoft. All rights reserved.
//

using EdgeModuleSamples.Common.Logging;
using EdgeModuleSamples.Common.Messages;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;

namespace UARTLCD
{
    class Program
    {
        static async Task<int> MainAsync(string[] args)
        {
            Log.WriteLine("Starting async...");
            var Options = new AppOptions();

            Options.Parse(args);
            Log.Enabled = !Options.Quiet;
            Log.Verbose = Options.Verbose;
            Log.WriteLine("arg parse complete...");
            AzureConnection connection = null;
            UARTDevice uart = null;
            await Task.WhenAll(
                Task.Run(async () => {
                    try { 
                        if (!Options.Test.HasValue)
                        {
                            Log.WriteLine("starting connection creation");
                            connection = await AzureConnection.CreateAzureConnectionAsync();
                        }
                    }
                    catch (Exception e)
                    {
                        Log.WriteLine("Main CreateAzureConnectionAsync exception {0}", e.ToString());
                    }
                }),
                Task.Run(async () =>
                    {
                        try
                        {
                            Log.WriteLine("creating UART device {0}", Options.DeviceName != null ? Options.DeviceName : "(default)");
                            uart = await UARTDevice.CreateUARTDevice(Options.DeviceName);
                            uart.InitAsync().Wait();
                            if (Options.Test.HasValue)
                            {
                                Log.WriteLine("initiating test");
                                uart.Test(Options.Test.Value);
                                Environment.Exit(2);
                            }                            
                        }
                        catch (Exception e)
                        {
                            Log.WriteLine("UART exception {0}", e.ToString());
                        }
                    }
                )
            );

#if TODO
            uart.OrientationChanged += (device, change) =>
            {
                connection.UpdateObject(new KeyValuePair<string, object>(Keys.Orientation, change.newOrientation));
            };
#endif
            Log.WriteLine("Initialization Complete. have connection and device.  ");

#if TODO
            Task.WaitAll(Task.Run(async () =>
            {
                try {
                    await uart.BeginOrientationMonitoringAsync();
                }
                catch (Exception e)
                {
                    Log.WriteLine("I2c wait spin exception {0}", e.ToString());
                }

            }));
#endif
            return 0;
        }

        static int Main(string[] args)
        {
            Log.Enabled = true;
            Log.WriteLine("Starting...");
            int rc = 0;
            try
            {
                Task.WaitAll(Task.Run(async () =>
                    rc = await MainAsync(args))
                );
            }
            catch (Exception e)
            {
                Log.WriteLineError("app failed {0}", e.ToString());
                rc = 1;
            }
            Log.WriteLine("Complete....");
            Console.Out.Flush();
            return rc;

        }
    }
}
