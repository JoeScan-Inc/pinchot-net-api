// This example application demonstrates how to configure, connect, and
// disconnect from a single scan head. For configuring the scan head, functions
// and data structures from the Pinchot will be introduced and utilized in
// a friendly manner. Following successful configuration, the application will
// connect to the scan head, print out its current status, and then finally
// disconnect.

using JoeScan.Pinchot;
using System;

namespace Examples
{
    public class ConfigureAndConnect
    {
        private static void Main(string[] args)
        {
            // Display the API version to console output for visual confirmation as to
            // the version being used for this example.
            Console.WriteLine($"Pinchot API version: {VersionInformation.Version}");

            // Grab the serial number of the scan head from the command line.
            if (args.Length == 0)
            {
                Console.WriteLine("Must provide a scan head serial number as argument.");
                return;
            }

            if (!uint.TryParse(args[0], out uint serialNumber))
            {
                Console.WriteLine($"Argument {args[0]} cannot be parsed as a uint.");
                return;
            }

            // One of the first calls to the API should be to create a scan manager
            // software object. This object will be used to manage groupings of scan
            // heads, telling them when to start and stop scanning.
            using var scanSystem = new ScanSystem(ScanSystemUnits.Inches);

            // Create a scan head software object for the user's specified serial
            // number and associate it with the scan manager we just created. We'll
            // also assign it a user defined ID that can be used within the application
            // as an optional identifier if preferred over the serial number. Note that
            // at this point, we haven't connected with the physical scan head yet.
            var scanHead = scanSystem.CreateScanHead(serialNumber, id: 1);

            // After a scan head has been created, we can query its firmware version
            Console.WriteLine($"Scan head version: {scanHead.Version}");

            // Now that we have successfully created the required software objects
            // needed to interface with the scan head and the scan system it is 
            // associated with, we can begin to configure the scan head.

            // Many of the settings directly related to the operation of the cameras
            // and lasers can be found in the `ScanHeadConfiguration` class. Refer
            // to the API documentation for specific details regarding each field. For
            // this example, we'll use some generic values not specifically set for any
            // particular scenario.
            var configuration = new ScanHeadConfiguration
            {
                LaserDetectionThreshold = 120,
                SaturationThreshold = 800,
                SaturationPercentage = 30
            };
            configuration.SetCameraExposureTime(10000, 47000, 900000);
            configuration.SetLaserOnTime(100, 100, 1000);
            scanHead.Configure(configuration);

            // Proper window selection can be crucial to successful scanning as it
            // allows users to limit the region of interest for scanning; filtering out
            // other sources of light that could complicate scanning. It is worth
            // noting that there is an inverse relationship with the scan window and
            // the overall scan rate a system can run at. Using larger scan windows
            // will reduce the maximum scan rate of a system, whereas using a smaller
            // scan window will increase the maximum scan rate.
            var scanWindow = ScanWindow.CreateScanWindowRectangular(30.0, -30.0, -30.0, 30.0);
            scanHead.SetWindow(scanWindow);

            // Setting the alignment through the following function can help to
            // correct for any mounting issues with a scan head that could affect
            // the 3D measurement. For this example, we'll assume that the scan head
            // is mounted perfectly such that the laser is pointed directly at the scan
            // target.
            scanHead.Orientation = ScanHeadOrientation.CableIsUpstream;
            scanHead.SetAlignment(0, 0, 0);

            // We've now successfully configured the scan head. Now comes the time to
            // connect to the physical scanner and transmit the configuration values
            // we previously set up.
            var scanHeadsThatFailedToConnect = scanSystem.Connect(TimeSpan.FromSeconds(3));
            if (scanHeadsThatFailedToConnect.Count > 0)
            {
                Console.WriteLine("Failed to connect to scan system.");
                return;
            }

            // Now that we are connected, we can query the scan head to get its
            // current status.
            var status = scanHead.RequestStatus();
            PrintScanHeadStatus(status);

            // Display the capabilties of the scan head. This will print out some physical
            // features and functional limits of the scan head.
            PrintScanHeadCapabilities(scanHead.Capabilities);

            // Once connected, this is the point where we could command the scan system
            // to start scanning; obtaining profile data from the scan heads associated
            // with it. This will be the focus of a later example.

            // We've accomplished what we set out to do for this example; now it's time
            // to bring down our system.
            scanSystem.Disconnect();

            // Clean up resources allocated by the scan manager.
            scanSystem.Dispose();
        }

        /// <summary>
        /// Prints the <see cref="ScanHeadCapabilities"/> of the provided <see cref="ScanHead"/>
        /// to the console.
        /// </summary>
        /// <param name="capabilities">The <see cref="ScanHeadCapabilities"/> to be printed.</param>
        private static void PrintScanHeadCapabilities(ScanHeadCapabilities capabilities)
        {
            Console.WriteLine("Capabilities");
            Console.WriteLine($"  Number of cameras: {capabilities.NumCameras}");
            Console.WriteLine($"  Number of lasers: {capabilities.NumLasers}");
            Console.WriteLine($"  Camera image height: {capabilities.MaxCameraImageHeight}");
            Console.WriteLine($"  Camera image width: {capabilities.MaxCameraImageWidth}");
            Console.WriteLine($"  Max scan period: {capabilities.MaxScanPeriodUs}");
            Console.WriteLine($"  Min scan period: {capabilities.MinScanPeriodUs}");
            Console.WriteLine($"  Max supported encoders: {capabilities.MaxSupportedEncoders}");
            Console.WriteLine($"  Brightness bit depth: {capabilities.CameraBrightnessBitDepth}");
        }

        /// <summary>
        /// Prints the <see cref="ScanHeadStatus"/> provided to the console.
        /// </summary>
        /// <param name="status">The <see cref="ScanHeadStatus"/> to be printed.</param>
        private static void PrintScanHeadStatus(ScanHeadStatus status)
        {
            Console.WriteLine("Status");
            Console.WriteLine($"  Global time: {status.GlobalTimeNs} ns");

            foreach (var encoderValue in status.EncoderValues)
            {
                Console.WriteLine($"  Encoder {encoderValue.Key}: {encoderValue.Value}");
            }

            foreach (var temperature in status.Temperatures)
            {
                Console.WriteLine($"  Temperature {temperature.Key}: {temperature.Value} °C");
            }

            Console.WriteLine($"  Number of profiles sent: {status.ProfilesSentCount}");
        }
    }
}
