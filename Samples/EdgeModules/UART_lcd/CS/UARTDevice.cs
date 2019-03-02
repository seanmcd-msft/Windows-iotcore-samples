﻿//
// Copyright (c) Microsoft. All rights reserved.
//

using EdgeModuleSamples.Common;
using static EdgeModuleSamples.Common.AsyncHelper;
using EdgeModuleSamples.Common.Device;
using EdgeModuleSamples.Common.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.SerialCommunication;
// do not use anything out of Universal API contract except Windows.Devices.Enumeration and the portion of Windows.Storage.Streams needed for serial
using Windows.Storage.Streams;


namespace UARTLCD
{

    public class UARTDevice : SPBDevice
    {
        protected static readonly byte LCD_ROWS = 0x02;
        protected static readonly byte LCD_COLS = 0x10;

        // firmware for the device is here: https://github.com/adafruit/Adafruit-USB-Serial-RGB-Character-Backpack
        protected static readonly byte LCD_INITIATE = 0xFE;

        protected static readonly byte LCD_CMD_DISPLAY_ON = 0x42;
        protected static readonly byte LCD_CMD_DISPLAY_OFF = 0x46;
        protected static readonly byte LCD_CMD_SET_BRIGHTNESS = 0x99;
        protected static readonly byte LCD_CMD_SET_SAVE_BRIGHTNESS = 0x98;
        protected static readonly byte LCD_CMD_SET_CONTRAST = 0x50;
        protected static readonly byte LCD_CMD_SET_SAVE_CONTRAST = 0x91;
        protected static readonly byte LCD_CMD_AUTOSCROLL_ON = 0x51;
        protected static readonly byte LCD_CMD_AUTOSCROLL_OFF = 0x52;
        protected static readonly byte LCD_CMD_CLEAR_SCREEN = 0x58;
        protected static readonly byte LCD_CMD_CHANGE_SPLASH = 0x40; // followed by 32 chars
        protected static readonly byte LCD_CMD_SET_CURSOR = 0x47;
        protected static readonly byte LCD_CMD_CURSOR_HOME = 0x48;
        protected static readonly byte LCD_CMD_CURSOR_BACK = 0x4c;
        protected static readonly byte LCD_CMD_CURSOR_FWD = 0x4d;
        protected static readonly byte LCD_CMD_UNDERLINE_CURSOR_ON = 0x4a;
        protected static readonly byte LCD_CMD_UNDERLINE_CURSOR_OFF = 0x4b;
        protected static readonly byte LCD_CMD_BLOCK_CURSOR_ON = 0x53;
        protected static readonly byte LCD_CMD_BLOCK_CURSOR_OFF = 0x54;
        protected static readonly byte LCD_CMD_BACKLIGHT_COLOR = 0xD0; // followed by 3 bytes of RGB
        protected static readonly byte LCD_CMD_SET_SAVE_SIZE = 0xD1; // followed by 2 bytes COLS; ROWS
        protected static readonly byte LCD_CMD_CREATE_CUSTOM_CHARACTER = 0x4E; // followed by 8 bytes of pixel data
        protected static readonly byte LCD_CMD_LOAD_CUSTOM_CHARACTER = 0xC0;
        protected static readonly byte LCD_CMD_SAVE_CUSTOM_CHARACTER = 0xC1;
        protected static readonly byte LCD_CMD_AUTO_LINEWRAP_ON = 0x43;
        protected static readonly byte LCD_CMD_AUTO_LINEWRAP_OFF = 0x44;

        private DataWriter _write = null;

        public SerialDevice Device { get; private set; }

        private UARTDevice() 
        {
        }
        public async Task InitAsync()
        {
            // reset display
            // configure display
            await Task.Run(async () =>
            {
                await SendCmdAsync(LCD_CMD_SET_SAVE_SIZE);
                await WriteByteAsync(LCD_COLS);
                await WriteByteAsync(LCD_ROWS);
                await SendCmdAsync(LCD_CMD_DISPLAY_ON);
                await WriteByteAsync(0); // unclear why display on needs an extra 0 byte. but, it does -- see firmware source.
                await AsAsync(_write.StoreAsync());
                Log.WriteLine("display on cmd sent, pausing");
                Thread.Sleep(TimeSpan.FromMilliseconds(300));
                await SendCmdAsync(LCD_CMD_DISPLAY_OFF); // display off doesn't need the mystery 0
                Log.WriteLine("display off cmd sent, pausing");
                Thread.Sleep(TimeSpan.FromMilliseconds(1000));
                await SendCmdAsync(LCD_CMD_DISPLAY_ON);
                await WriteByteAsync(0); // unclear why display on needs an extra 0 byte. but, it does -- see firmware source.
                await AsAsync(_write.StoreAsync()); //flush before pause
                Thread.Sleep(TimeSpan.FromMilliseconds(300));
                Log.WriteLine("display on again pausing");
                await SendCmdAsync(LCD_CMD_BLOCK_CURSOR_OFF);
                await SendCmdAsync(LCD_CMD_UNDERLINE_CURSOR_ON);
                await Clear();
                Log.WriteLine("cleared screen with underline cursor");
                await SendCmdAsync(LCD_CMD_SET_CONTRAST);
                await SetContrast(220); // 220 is what the reference code python test uses
                await SetBrightness(0xff);
                await SetBackgroundAsync(Colors.Red);
                Log.WriteLine("init complete");
            });

            return;
        }
        private async Task SendCmdAsync(byte cmd)
        {
            await WriteByteAsync(LCD_INITIATE);
            await WriteByteAsync(cmd);
            await AsAsync(_write.StoreAsync());
        }
        public async Task SetContrast(byte b)
        {
            await SendCmdAsync(LCD_CMD_SET_CONTRAST);
            await WriteByteAsync(b);
        }
        public async Task SetBrightness(byte b)
        {
            await SendCmdAsync(LCD_CMD_SET_BRIGHTNESS);
            await WriteByteAsync(b);
        }
        public async Task Clear()
        {
            await SendCmdAsync(LCD_CMD_CLEAR_SCREEN);
            await SendCmdAsync(LCD_CMD_CURSOR_HOME);
        }
        public async Task SetBackgroundAsync(RGBColor c)
        {
            await SendCmdAsync(LCD_CMD_BACKLIGHT_COLOR);
            var b = new byte[3];
            b[0] = c.Red;
            b[1] = c.Green;
            b[2] = c.Blue;
            await WriteBytesAsync(b);
        }

        // note: this is the only async write that doesn't flush
        private async Task WriteByteAsync(byte b)
        {
            await Task.Run(() => _write.WriteByte(b));
            //await AsAsync(_write.StoreAsync());
        }
        // everything should be using high level commands or writestring
        private async Task WriteBytesAsync(byte[] b)
        {
            await Task.Run(() => _write.WriteBytes(b));
            await AsAsync(_write.StoreAsync());
        }
        public async Task WriteStringAsync(string s)
        {
            _write.WriteString(s);
            await AsAsync(_write.StoreAsync());
        }

        public static async Task<UARTDevice> CreateUARTDevice(string deviceName)
        {
            Log.WriteLine("finding device {0}", deviceName != null ? deviceName : "(default)");

            Windows.Devices.Enumeration.DeviceInformation info = null;
            if (deviceName != null)
            {
                info = await FindAsync(SerialDevice.GetDeviceSelector(deviceName));
            } else
            {
                info = await FindAsync(SerialDevice.GetDeviceSelector());
            }
            Log.WriteLine("UART device info {0} null", info == null ? "is" : "is not");

            var d = new UARTDevice();
            d.Device = await AsAsync(SerialDevice.FromIdAsync(info.Id));
            d.Device.DataBits = 8;
            d.Device.Handshake = SerialHandshake.None;
            d.Device.BaudRate = 9600;
            d.Device.WriteTimeout = TimeSpan.FromMilliseconds(1000);
            d.Device.ReadTimeout = TimeSpan.FromMilliseconds(1000);
            d.Device.StopBits = SerialStopBitCount.One;
            d.Device.Parity = SerialParity.None;
            Log.WriteLine("UART Settings complete");
            d._write = new DataWriter(d.Device.OutputStream);
            Log.WriteLine("have datawriter");
            return d;
        }
        public async Task TestAsync(string msg)
        {
            Log.WriteLine("testing...");
            Log.WriteLine("test clearing...");
            await Clear();
            Log.WriteLine("test bg white");
            await SetBackgroundAsync(Colors.White);
            Log.WriteLine("test msg");
            await WriteStringAsync(msg);
            Log.WriteLine("test complete");
        }

    }
}
