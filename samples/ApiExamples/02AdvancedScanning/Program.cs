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

namespace _02AdvancedScanning
{
    class Program
    {
        private static ScanSystem _scanSystem;
        private static readonly List<uint> ScanHeadSerialNumbers = new List<uint>();
        private static readonly CancellationTokenSource StopTakingProfilesTokenSource = new CancellationTokenSource();
        private static readonly CancellationToken StopTakingProfilesToken = StopTakingProfilesTokenSource.Token;

        private static int _profilesCount = 0;

        private static void Main(string[] args)
        {
            // Display the API version to console output for visual confirmation as to
            // the version being used for this example.
            Console.WriteLine($"{nameof(JoeScan.Pinchot)}: {VersionInformation.Version}");

            // Grab the serial number of the scan head from the command line.
            if (args.Length == 0)
            {
                Console.WriteLine("Must provide a scan head serial number as argument.");
                return;
            }

            foreach (var argument in args)
            {
                if (!uint.TryParse(argument, out var scanHeadSerialNumber))
                {
                    Console.WriteLine($"Argument {argument} cannot be parsed as a uint.");
                    return;
                }

                ScanHeadSerialNumbers.Add(scanHeadSerialNumber);
            }

            // First step is to create a scan manager to manage the scan heads.
            _scanSystem = new ScanSystem();

            // Create a scan head for each serial number passed in on the command line
            // and configure each one with the same parameters. Note that there is
            // nothing stopping users from configuring each scan head independently.
            var id = 0U;
            foreach (var serialNumber in ScanHeadSerialNumbers)
            {
                _scanSystem.CreateScanHead(serialNumber, id++);
            }

            var configuration = new ScanHeadConfiguration
            {
                ScanPhaseOffset = 0,
                LaserDetectionThreshold = 120,
                SaturationThreshold = 800,
                SaturatedPercentage = 30
            };
            configuration.SetCameraExposureTime(10000, 47000, 900000);
            configuration.SetLaserOnTime(100, 100, 1000);

            var scanWindow = ScanWindow.CreateScanWindowRectangular(20.0, -20.0, -20.0, 20.0);

            foreach (var scanHead in _scanSystem.ScanHeads)
            {
                scanHead.Configure(configuration);
                scanHead.SetWindow(scanWindow);
                scanHead.SetAlignment(0, 0, 0, ScanHeadOrientation.CableIsUpstream);
            }

            // Now that the scan heads are configured, we'll connect to the heads.
            var scanHeadsThatFailedToConnect = _scanSystem.Connect(TimeSpan.FromSeconds(3));
            foreach (var scanHead in scanHeadsThatFailedToConnect)
            {
                Console.WriteLine($"Failed to connect to scan head {scanHead.SerialNumber}.");
            }

            if (scanHeadsThatFailedToConnect.Count > 0) return;

            // Once configured, we can then read the status from the scan head. If
            // each scan head was configured with a different scan window, they'll
            // each have a different maximum scan rate.
            foreach (var scanHead in _scanSystem.ScanHeads)
            {
                Console.WriteLine($"{scanHead.ID} has maximum scan rate of {scanHead.Status.MaxScanRate}Hz.");
            }

            // To begin scanning on all of the scan heads, all we need to do is
            // command the scan system to start scanning. This will cause all of the
            // scan heads associated with it to begin scanning at the specified rate
            // and data format.
            const double rate = 900;
            const DataFormat format = DataFormat.XYFullLMFull;
            _scanSystem.StartScanning(rate, format);

            // In order to achieve a performant application, we'll create a thread
            // for each scan head. This allows the CPU load of reading out profiles
            // to be distributed across all the cores available on the system rather
            // than keeping the heavy lifting in an application within a single process.
            var threads = new List<Thread>();
            foreach (var scanHead in _scanSystem.ScanHeads)
            {
                var thread = new Thread(() => Receiver(scanHead));
                thread.Start();
                threads.Add(thread);
            }

            // Put this thread to sleep until the total scan time is done.
            Thread.Sleep(1000);

            // Calling `ScanSystem.StopScanning()` will return immediately rather than
            // blocking until the scan heads have fully stopped. As a consequence, we
            // will need to add a small delay before the scan heads begin sending
            // new status updates.
            _scanSystem.StopScanning();
            Thread.Sleep(2000);

            // We can verify that we received all of the profiles sent by the scan
            // heads by reading each scan head's status message and summing up the
            // number of profiles that were sent. If everything went well and the
            // CPU load didn't exceed what the system can manage, this value should
            // be equal to the number of profiles we received in this application.
            var expectedProfilesCount = _scanSystem.ScanHeads.Sum(scanHead => scanHead.Status.ProfilesSentCount);
            Console.WriteLine(
                $"Number of profiles received: {_profilesCount}, number of profiles expected: {expectedProfilesCount}");

            // Wait for each thread to terminate before disposing resources.
            foreach (var thread in threads)
            {
                thread.Join();
            }

            // Free resources.
            _scanSystem.Dispose();
        }

        /// <summary>
        /// This function is a small utility function used to explore profile
        /// data. In this case, it will iterate over the valid profile data and
        /// find the highest measurement in the Y axis.
        /// </summary>
        /// <param name="profiles">An <see cref="IEnumerable{T}"/> of
        /// <see cref="Profile"/>s.</param>
        /// <returns>The <see cref="Point2D"/> corresponding to the highest
        /// point.</returns>
        private static Point2D FindScanProfileHighestPoint(IEnumerable<Profile> profiles)
        {
            var p = new Point2D();
            foreach (var profile in profiles)
            {
                foreach (var point in profile.RawPoints)
                {
                    if (point.Y > p.Y)
                    {
                        p = point;
                    }
                }
            }

            return p;
        }

        /// <summary>
        /// This function receives profile data from a given scan head. We start
        /// a thread for each scan head to pull out the data as fast as possible.
        /// </summary>
        /// <param name="scanHead">The <see cref="ScanHead"/> to receive profile
        /// data from.</param>
        private static void Receiver(ScanHead scanHead)
        {
            var profiles = new List<Profile>();
            try
            {
                while (scanHead.TryTakeNextProfile(out var profile, TimeSpan.FromMilliseconds(5000), StopTakingProfilesToken))
                {
                    Interlocked.Increment(ref _profilesCount);
                    profiles.Add(profile);

                    if (profiles.Count < 100) continue;

                    // For this example, we'll grab some profiles and then act on the data before
                    // repeating this process again. Note that for high performance applications,
                    // printing to the console while receiving data should be avoided as it
                    // can add significant latency. This example only prints to the console to
                    // provide some illustrative feedback to the user, indicating that data
                    // is actively being worked on in multiple threads.
                    var highestPoint = FindScanProfileHighestPoint(profiles);
                    Console.WriteLine(
                        $"Highest point from scan head {scanHead.ID} is X: {highestPoint.X}\tY: {highestPoint.Y}\tBrightness: {highestPoint.Brightness}");
                    profiles = new List<Profile>();
                }
            }
            catch (OperationCanceledException)
            {
                // Thrown by BlockingCollection.TryTake when cancelled. No need to handle.
            }
        }
    }
}

