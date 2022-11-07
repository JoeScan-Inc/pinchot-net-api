// This application shows how one can stream profile data from multiple scan
// heads in a manner that allows for real time processing of the data. To
// accomplish this, multiple threads are created to break up the work of
// reading in new profile data and acting upon it. Configuration of the scan
// heads will mostly be identical to previous examples although some values
// may be changed to allow for faster streaming of data.

using JoeScan.Pinchot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Examples
{
    public class AdvancedScanningJS50X6B
    {
        private static int _profilesCount;

        private static void Main(string[] args)
        {
            // Display the API version to console output for visual confirmation as to
            // the version being used for this example.
            Console.WriteLine($"Pinchot API version: {VersionInformation.Version}");

            if (args.Length == 0)
            {
                Console.WriteLine("Must provide a scan head serial number as argument.");
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
                SaturationPercentage = 30
            };
            configuration.SetCameraExposureTime(10000, 47000, 900000);
            configuration.SetLaserOnTime(100, 100, 1000);

            var scanWindow = ScanWindow.CreateScanWindowRectangular(20.0, -20.0, -20.0, 20.0);

            foreach (var scanHead in scanSystem.ScanHeads)
            {
                scanHead.Configure(configuration);
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

            // For this example we will create a phase table that interleaves lasers
            // seen by Camera A and Camera B. This allows fast and efficient scanning
            // by allowing one camera to scan while the other has the scan data read out
            // & processed; if the same camera is used back to back, a time penalty
            // will be incurred to ensure scan data isn't overwritten.
            //
            // The phase table will end up looking like the following.
            // Phase | Laser | Camera
            //   1   |   1   |   B
            //   2   |   4   |   A
            //   3   |   2   |   B
            //   4   |   5   |   A
            //   5   |   3   |   B
            //   6   |   6   |   A
            for (int n = 0; n < 3; n++)
            {
                // Lasers associated with Camera B
                scanSystem.AddPhase();
                Laser laser = Laser.Laser1 + n;
                foreach (var scanHead in scanSystem.ScanHeads)
                {
                    scanSystem.AddPhaseElement(scanHead.ID, laser);
                }

                // Lasers associated with Camera A
                scanSystem.AddPhase();
                laser = Laser.Laser4 + n;
                foreach (var scanHead in scanSystem.ScanHeads)
                {
                    scanSystem.AddPhaseElement(scanHead.ID, laser);
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

            // In order to achieve a performant application, we'll create a thread
            // for each scan head. This allows the CPU load of reading out profiles
            // to be distributed across all the cores available on the system rather
            // than keeping the heavy lifting in an application within a single process.
            var threads = new List<Thread>();
            foreach (var scanHead in scanSystem.ScanHeads)
            {
                var thread = new Thread(() => Receiver(scanHead));
                thread.Start();
                threads.Add(thread);
            }

            // Put this thread to sleep until the total scan time is done.
            Thread.Sleep(TimeSpan.FromSeconds(10));

            // We've collected all of our data; time to stop scanning. Calling this
            // function will cause each scan head within the entire scan system to
            // stop scanning. Once we're done scanning, we'll process the data.
            scanSystem.StopScanning();
            threads.ForEach(thread => thread.Join());

            // We can verify that we received all of the profiles sent by the scan
            // heads by reading each scan head's status message and summing up the
            // number of profiles that were sent. If everything went well and the
            // CPU load didn't exceed what the system can manage, this value should
            // be equal to the number of profiles we received in this application.
            long expectedProfilesCount = scanSystem.ScanHeads.Sum(scanHead => scanHead.RequestStatus().ProfilesSentCount);
            Console.WriteLine(
                $"Number of profiles received: {_profilesCount}, number of profiles expected: {expectedProfilesCount}");
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

        /// <summary>
        /// This function receives profile data from a given scan head. We start
        /// a thread for each scan head to pull out the data as fast as possible.
        /// </summary>
        private static void Receiver(ScanHead scanHead)
        {
            try
            {
                var profiles = new List<IProfile>();
                while (scanHead.TryTakeNextProfile(out var profile, TimeSpan.FromSeconds(1), CancellationToken.None))
                {
                    Interlocked.Increment(ref _profilesCount);
                    profiles.Add(profile);

                    if (profiles.Count < 100)
                    {
                        continue;
                    }

                    // For this example, we'll grab some profiles and then act on the data before
                    // repeating this process again. Note that for high performance applications,
                    // printing to the console while receiving data should be avoided as it
                    // can add significant latency. This example only prints to the console to
                    // provide some illustrative feedback to the user, indicating that data
                    // is actively being worked on in multiple threads.
                    var highestPoint = FindScanProfileHighestPoint(profiles);
                    Console.WriteLine(
                        $"Highest point from scan head {scanHead.ID} is X: {highestPoint.X:F3}\tY: {highestPoint.Y:F3}\tBrightness: {highestPoint.Brightness}");
                    profiles.Clear();
                }
            }
            catch (OperationCanceledException)
            {
                // Thrown by BlockingCollection.TryTake when cancelled. No need to handle.
            }
        }
    }
}

