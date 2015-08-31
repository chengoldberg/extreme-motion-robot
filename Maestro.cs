using Pololu.Usc;
using Pololu.UsbWrapper;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Windows.Forms;

namespace CSharpVisualSkeletonSample
{
    class Maestro
    {
        Dictionary<int, List<int>> physicalConfig = new Dictionary<int, List<int>>() {
                // Channel, [target, degree, target, degree]
                {1, new List<int>(){525, 80, 2113, -70}},
                {2, new List<int>(){863, -80, 2448, 90}},
                {4, new List<int>(){736, 90, 2448, -90}},
                {5, new List<int>(){625, -90, 2448, 90}}};                

        /// <summary>
        /// Attempts to set the target (width of pulses sent) of a channel.
        /// </summary>
        /// <param name="channel">Channel number from 0 to 23.</param>
        /// <param name="target">
        ///   Target, in units of quarter microseconds.  For typical servos,
        ///   6000 is neutral and the acceptable range is 4000-8000.
        /// </param>
        public void TrySetTarget(Byte channel, UInt16 target)
        {
            try
            {
                using (Usc device = ConnectToDevice())  // Find a device and temporarily connect.
                {
                    device.setTarget(channel, target);

                    // device.Dispose() is called automatically when the "using" block ends,
                    // allowing other functions and processes to use the device.
                }
            }
            catch (Exception exception)  // Handle exceptions by displaying them to the user.
            {
                displayException(exception);
            }
        }

        /// <summary>
        /// Connects to a Maestro using native USB and returns the Usc object
        /// representing that connection.  When you are done with the
        /// connection, you should close it using the Dispose() method so that
        /// other processes or functions can connect to the device later.  The
        /// "using" statement can do this automatically for you.
        /// </summary>
        Usc ConnectToDevice()
        {
            // Get a list of all connected devices of this type.
            List<DeviceListItem> connectedDevices = Usc.getConnectedDevices();

            foreach (DeviceListItem dli in connectedDevices)
            {
                // If you have multiple devices connected and want to select a particular
                // device by serial number, you could simply add a line like this:
                //   if (dli.serialNumber != "00012345"){ continue; }

                Usc device = new Usc(dli); // Connect to the device.
                return device;             // Return the device.
            }
            throw new Exception("Could not find device.  Make sure it is plugged in to USB " +
                "and check your Device Manager (Windows) or run lsusb (Linux).");
        }

        /// <summary>
        /// Displays an exception to the user by popping up a message box.
        /// </summary>
        void displayException(Exception exception)
        {
            StringBuilder stringBuilder = new StringBuilder();
            do
            {
                stringBuilder.Append(exception.Message + "  ");
                if (exception is Win32Exception)
                {
                    stringBuilder.Append("Error code 0x" + ((Win32Exception)exception).NativeErrorCode.ToString("x") + ".  ");
                }
                exception = exception.InnerException;
            }
            while (exception != null);
            MessageBox.Show(stringBuilder.ToString(), "", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        public ushort DegreeToTarget(int channel, int degree)
        {
            var config = physicalConfig[channel];
            int min_val = Math.Min(config[1], config[3]);
            float alpha = (degree - (float)(min_val)) / (Math.Max(config[1], config[3])-min_val);
            alpha = Math.Max(Math.Min(alpha, 1), 0);
            alpha = config[1] > config[3] ? 1-alpha : alpha;            
            min_val = Math.Min(config[0], config[2]);
            int target = (int) Math.Round(alpha*(Math.Max(config[0], config[2])-min_val) + (float)(min_val));
            return (ushort) (target*4);
        }

        public void DisableAll()
        {
            TrySetTarget(5, 0);
            TrySetTarget(4, 0);
            TrySetTarget(1, 0);
            TrySetTarget(2, 0);
        }
    }
}
