﻿// This application shows the fundamentals of how to stream profile data
// from scan heads up through the client API and into your own code. Each scan
// head will be initially configured before scanning using generous settings
// that should guarantee that valid profile data is obtained. Following
// configuration, a limited number of profiles will be collected before halting
// the scan and disconnecting from the scan heads.

using JoeScan.Pinchot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace _01BasicScanning
{
    class Program
    {
        private static ScanSystem _scanSystem;
        private static readonly List<uint> ScanHeadSerialNumbers = new List<uint>();

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

            // Create a scan head software object for each serial number passed in
            // through the command line. We'll assign each one a unique ID starting at
            // zero; we'll use this as an easy index for associating profile data with
            // a given scan head.
            var id = 0U;
            foreach (var serialNumber in ScanHeadSerialNumbers)
            {
                _scanSystem.CreateScanHead(serialNumber, id++);
            }

            // For this example application, we'll just use the same configuration
            // settings we made use of in the "Configure and Connect" example. The
            // only real difference here is that we will be applying this configuration
            // to multiple scan heads, using a "for" loop to configure each scan head
            // one after the other.
            var configuration = new ScanHeadConfiguration
            {
                ScanPhaseOffset = 0,
                LaserDetectionThreshold = 120,
                SaturationThreshold = 800,
                SaturatedPercentage = 30
            };
            configuration.SetCameraExposureTime(10000, 47000, 900000);
            configuration.SetLaserOnTime(100, 100, 1000);

            foreach (var scanHead in _scanSystem.ScanHeads)
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
                scanHead.SetAlignment(0, 0, 0, ScanHeadOrientation.CableIsUpstream);
            }

            // Now that the scan heads are configured, we'll connect to the heads.
            var scanHeadsThatFailedToConnect = _scanSystem.Connect(TimeSpan.FromSeconds(3));
            foreach (var scanHead in scanHeadsThatFailedToConnect)
            {
                Console.WriteLine($"Failed to connect to scan head {scanHead.SerialNumber}.");
            }

            if (scanHeadsThatFailedToConnect.Count > 0) return;

            // Once configured, we can then read the maximum scan rate of the scan
            // system. This rate is governed by the scan head with the lowest scan
            // rate based off window size and exposure values.
            double maxScanRate = _scanSystem.GetMaxScanRate();
            Console.WriteLine($"The system has a maximum scan rate of {maxScanRate}Hz.");

            // To begin scanning on all of the scan heads, all we need to do is
            // command the scan system to start scanning. This will cause all of the
            // scan heads associated with it to begin scanning at the specified rate
            // and data format.
            const double rate = 400;
            const DataFormat format = DataFormat.XYFullLMFull;
            _scanSystem.StartScanning(rate, format);

            // We'll read out a small number of profiles for each scan head, servicing
            // each one in a round robin fashion until the requested number of profiles
            // have been obtained.
            const int maxProfiles = 10;
            var timeout = TimeSpan.FromSeconds(10);
            var stopwatch = Stopwatch.StartNew();
            var profiles = _scanSystem.ScanHeads.ToDictionary(scanHead => scanHead, scanHead => new List<Profile>());

            while (profiles.Values.Any(l => l.Count < maxProfiles))
            {
                foreach (var scanHead in _scanSystem.ScanHeads)
                {
                    scanHead.TryTakeNextProfile(out var profile, TimeSpan.FromMilliseconds(100),
                        new CancellationToken());

                    if (profile is null) continue;

                    profiles[scanHead].Add(profile);
                }

                if (stopwatch.Elapsed > timeout)
                {
                    Console.WriteLine($"Timed-out waiting to collect {maxProfiles} profiles.");
                    return;
                }
            }

            // We've collected all of our data; time to stop scanning. Calling this
            // function will cause each scan head within the entire scan system to
            // stop scanning. Once we're done scanning, we'll process the data.
            _scanSystem.StopScanning();
            foreach (var scanHead in _scanSystem.ScanHeads)
            {
                var highestPoint = FindScanProfileHighestPoint(profiles[scanHead]);
                Console.WriteLine($"Highest point from scan head {scanHead.ID} is X: {highestPoint.X:F3}\tY: {highestPoint.Y:F3}\tBrightness: {highestPoint.Brightness}");
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
                foreach (var point in profile.GetValidXYPoints())
                {
                    if (point.Y > p.Y)
                    {
                        p = point;
                    }
                }
            }

            return p;
        }
    }
}
