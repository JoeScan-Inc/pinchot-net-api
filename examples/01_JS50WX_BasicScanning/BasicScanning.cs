// This application shows the fundamentals of how to stream profile data
// from scan heads up through the client API and into your own code. Each scan
// head will be initially configured before scanning using generous settings
// that should guarantee that valid profile data is obtained. Following
// configuration, a limited number of profiles will be collected before halting
// the scan and disconnecting from the scan heads.

using JoeScan.Pinchot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Examples
{
    public class BasicScanning
    {
        private static void Main(string[] args)
        {
            // Display the API version to console output for visual confirmation as to
            // the version being used for this example.
            Console.WriteLine($"Pinchot API version: {VersionInformation.Version}");

            if (args.Length == 0)
            {
                Console.WriteLine("Must provide one or more scan head serial numbers as arguments.");
                return;
            }

            // Grab the serial number of the scan head(s) from the command line.
            var serialNumbers = new List<uint>();
            foreach (string argument in args)
            {
                if (!uint.TryParse(argument, out uint scanHeadSerialNumber))
                {
                    Console.WriteLine($"Argument {argument} cannot be parsed as a uint.");
                    return;
                }

                serialNumbers.Add(scanHeadSerialNumber);
            }

            // First step is to create a scan manager to manage the scan heads.
            using var scanSystem = new ScanSystem(ScanSystemUnits.Inches);

            // Create a scan head software object for each serial number passed in
            // through the command line. We'll assign each one a unique ID starting at
            // zero; we'll use this as an easy index for associating profile data with
            // a given scan head.
            uint id = 0;
            foreach (uint serialNumber in serialNumbers)
            {
                var scanHead = scanSystem.CreateScanHead(serialNumber, id++);
                Console.WriteLine($"Scan head version: {scanHead.Version}");
            }

            // For this example application, we'll just use the same configuration
            // settings we made use of in the "ConfigureAndConnect" example. The
            // only real difference here is that we will be applying this configuration
            // to multiple scan heads, using a "foreach" loop to configure each scan head
            // one after the other.
            var configuration = new ScanHeadConfiguration
            {
                LaserDetectionThreshold = 120,
                SaturationThreshold = 800,
                SaturationPercentage = 30,
            };
            configuration.SetCameraExposureTime(10000, 47000, 900000);
            configuration.SetLaserOnTime(100, 100, 1000);

            foreach (var scanHead in scanSystem.ScanHeads)
            {
                scanHead.Configure(configuration);

                // To illustrate that each scan head can be configured independently,
                // we'll alternate between two different windows for each scan head. The
                // other options we will leave the same only for the sake of convenience;
                // these can be independently configured as needed.
                var scanWindow = scanHead.ID % 2 == 0
                    ? ScanWindow.CreateScanWindowRectangular(20.0, -20.0, -20.0, 20.0)
                    : ScanWindow.CreateScanWindowRectangular(30.0, -30.0, -30.0, 30.0);
                scanHead.SetWindow(scanWindow);
                scanHead.Orientation = ScanHeadOrientation.CableIsUpstream;
                scanHead.SetAlignment(0, 0, 0);
            }

            // Now that the scan heads are configured, we'll connect to the heads.
            var scanHeadsThatFailedToConnect = scanSystem.Connect(TimeSpan.FromSeconds(3));
            foreach (var scanHead in scanHeadsThatFailedToConnect)
            {
                Console.WriteLine($"Failed to connect to scan head {scanHead.SerialNumber}.");
            }

            if (scanHeadsThatFailedToConnect.Count > 0)
            {
                return;
            }

            // For this example we will create a simple phase table with two phases, one
            // for each cameras. Each JS-50 WX will have Camera A in the first phase and
            // Camera B in the second phase.
            for (var camera = Camera.CameraA; camera < Camera.CameraB; camera++)
            {
                scanSystem.AddPhase();

                foreach (var scanHead in scanSystem.ScanHeads)
                {
                    scanSystem.AddPhaseElement(scanHead.ID, camera);
                }
            }

            // Once the phase table is created, we can then read the minimum scan period
            // of the scan system. This value depends on how many phases there are in the
            // phase table and each scan head's laser on time & window configuration.
            uint minScanPeriodUs = scanSystem.GetMinScanPeriod();
            Console.WriteLine($"The system has a min scan period of {minScanPeriodUs}µs.");

            // To begin scanning on all of the scan heads, all we need to do is
            // command the scan system to start scanning. This will cause all of the
            // scan heads associated with it to begin scanning at the specified rate
            // and data format.
            const DataFormat format = DataFormat.XYBrightnessFull;
            scanSystem.StartScanning(minScanPeriodUs, format);

            // We'll read out a small number of profiles for each scan head, servicing
            // each one in a round robin fashion until the requested number of profiles
            // have been obtained.
            const int maxProfiles = 10;
            var profiles = scanSystem.ScanHeads.ToDictionary(scanHead => scanHead, _ => new List<IProfile>());

            while (profiles.Values.Any(l => l.Count < maxProfiles))
            {
                foreach (var scanHead in scanSystem.ScanHeads)
                {
                    // Read out the next available profile and store it in the
                    // profile buffer to be processed at a later time.
                    if (!scanHead.TryTakeNextProfile(out var profile))
                    {
                        continue;
                    }

                    profiles[scanHead].Add(profile);
                }
            }

            // We've collected all of our data; time to stop scanning. Calling this
            // function will cause each scan head within the entire scan system to
            // stop scanning. Once we're done scanning, we'll process the data.
            scanSystem.StopScanning();
            foreach (var scanHead in scanSystem.ScanHeads)
            {
                var highestPoint = FindScanProfileHighestPoint(profiles[scanHead]);
                Console.WriteLine($"Highest point from scan head {scanHead.ID} is X: {highestPoint.X:F3}\tY: {highestPoint.Y:F3}\tBrightness: {highestPoint.Brightness}");
            }
        }

        /// <summary>
        /// This function is a small utility function used to explore profile
        /// data. In this case, it will iterate over the valid profile data and
        /// find the highest measurement in the Y axis.
        /// </summary>
        private static Point2D FindScanProfileHighestPoint(IEnumerable<IProfile> profiles)
        {
            var maxPoint = new Point2D();
            foreach (var profile in profiles)
            {
                var points = profile.GetValidXYPoints();
                foreach (var point in points)
                {
                    if (point.Y > maxPoint.Y)
                    {
                        maxPoint = point;
                    }
                }
            }

            return maxPoint;
        }
    }
}
