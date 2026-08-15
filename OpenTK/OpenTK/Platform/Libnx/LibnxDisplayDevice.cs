using System;
using System.Collections.Generic;
using System.Text;

using OpenTK.Graphics;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace OpenTK.Platform.Libnx
{
    internal class LibnxDisplayDevice : IDisplayDeviceDriver
    {
        readonly DisplayDevice mainDisplay;

        public LibnxDisplayDevice()
        {
            var res = new DisplayResolution(0,0, 1280, 720, 32, 60);
            mainDisplay = new DisplayDevice(res, true, [res], new System.Drawing.Rectangle(0, 0, 1280, 720));
        }

        public bool TryChangeResolution(DisplayDevice device, DisplayResolution resolution)
        {
            if (resolution == null)
                return true;

            if (resolution.Width == 1280 && resolution.Height == 720)
                return true;

            return false;
        }

        public bool TryRestoreResolution(DisplayDevice device)
        {
            return TryChangeResolution(device, null);
        }
    }
}
