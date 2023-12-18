// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

// This application shows how one can stream profile data from multiple scan
// heads in a manner that allows for real time processing of the data. To
// accomplish this, multiple threads are created to break up the work of
// reading in new profile data and acting upon it. Configuration of the scan
// heads will mostly be identical to previous examples although some values
// may be changed to allow for faster streaming of data.

using JoeScan.Pinchot;

// Global variable that will contain the total count
// of profiles received from all heads.
int totalProfiles = 0;

// Set up a token and bind it it Ctrl+C to allow stopping the application early.
using CancellationTokenSource cts = new();
var token = cts.Token;
Console.CancelKeyPress += (_, e) =>
{
    Console.WriteLine("Cancelled");
    cts.Cancel();
    e.Cancel = true;
};

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
    if (!uint.TryParse(argument, out uint serial))
    {
        Console.WriteLine($"Argument {argument} cannot be parsed as a uint.");
        return;
    }

    serialNumbers.Add(serial);
}

// First step is to create a scan system to manage the scan heads.
using var scanSystem = new ScanSystem(ScanSystemUnits.Inches);

// Create a scan head for each serial number passed in through the command line.
// We'll assign each one a unique ID (starting at zero) and use this as the
// index for associating profile data with a given scan head.
uint id = 0;
foreach (uint serialNumber in serialNumbers)
{
    var scanHead = scanSystem.CreateScanHead(serialNumber, id++);
    Console.WriteLine($"{serialNumber} version: {scanHead.Version}");
}

// Configure all the heads with the same settings and window.
var configuration = new ScanHeadConfiguration();
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
var connectTimeout = TimeSpan.FromSeconds(3);
var scanHeadsThatFailedToConnect = scanSystem.Connect(connectTimeout);
if (scanHeadsThatFailedToConnect.Count > 0)
{
    foreach (var scanHead in scanHeadsThatFailedToConnect)
    {
        Console.WriteLine($"Failed to connect to scan head {scanHead.SerialNumber}.");
    }

    return;
}

// For this example we will create a simple phase table where each camera or laser
// has its own phase in the phase table. Note that certain scan head types are
// "camera driven" and use Camera types as arguments whereas others are "laser driven"
// and use Laser types.
foreach (var scanHead in scanSystem.ScanHeads)
{
    if (scanHead.Type is ProductType.JS50WX or ProductType.JS50WSC or ProductType.JS50MX)
    {
        foreach (var camera in scanHead.Cameras)
        {
            scanSystem.AddPhase();
            scanSystem.AddPhaseElement(scanHead.ID, camera);
        }
    }
    else if (scanHead.Type is ProductType.JS50X6B20 or ProductType.JS50X6B30 or ProductType.JS50Z820 or ProductType.JS50Z830)
    {
        foreach (var laser in scanHead.Lasers)
        {
            scanSystem.AddPhase();
            scanSystem.AddPhaseElement(scanHead.ID, laser);
        }
    }
    else
    {
        throw new InvalidOperationException($"Invalid scan head type {scanHead.Type}");
    }
}

// Once the phase table is created, we can then read the minimum scan period
// of the scan system. This value depends on how many phases there are in the
// phase table and each scan head's laser on time and window configuration.
uint minScanPeriodUs = scanSystem.GetMinScanPeriod();
Console.WriteLine($"Min scan period of {minScanPeriodUs} µs");

// To begin scanning on all of the scan heads, all we need to do is
// command the scan system to start scanning. This will cause all of the
// scan heads associated with it to begin scanning at the specified rate
// and data format.
const DataFormat format = DataFormat.XYBrightnessFull;
scanSystem.StartScanning(minScanPeriodUs, format);
Console.WriteLine("Scanning...");

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

// The current thread can now go on to do other things while the receiver
// threads gather profiles. This is especially important in GUI applications
// as to not lock up the main UI thread.

try
{
    // Wait for a time to allow receiver threads to
    // collect and process a number of profiles.
    var scanTime = TimeSpan.FromSeconds(5);
    await Task.Delay(scanTime, token);
}
catch (TaskCanceledException) { }

// We've collected all of our data and now it's time to stop scanning.
// Calling this function will cause each scan head within the entire
// scan system to stop scanning.
scanSystem.StopScanning();
Console.WriteLine("Stop scanning");
threads.ForEach(thread => thread.Join());

// We can verify that we received all of the profiles sent by the scan
// heads by reading each scan head's status message and summing up the
// number of profiles that were sent. If everything went well and the
// CPU load didn't exceed what the system can manage, this value should
// be equal to the number of profiles we received in this application.
long expectedProfilesCount = scanSystem.ScanHeads.Sum(sh => sh.RequestStatus().ProfilesSentCount);
Console.WriteLine($"Number of profiles received: {totalProfiles}");
Console.WriteLine($"Number of profiles expected: {expectedProfilesCount}");

/// <summary>
/// This function receives profile data from a given scan head. We start
/// a thread for each scan head to pull out the data as fast as possible.
/// </summary>
void Receiver(ScanHead scanHead)
{
    try
    {
        var profiles = new List<IProfile>();
        while (scanSystem.IsScanning || scanHead.NumberOfProfilesAvailable > 0)
        {
            if (!scanHead.TryTakeNextProfile(out IProfile profile, TimeSpan.FromSeconds(1), token))
            {
                continue;
            }

            profiles.Add(profile);
            Interlocked.Increment(ref totalProfiles);

            // Wait for 100 profiles before processing.
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
            var maxPoint = new Point2D();
            foreach (var p in profiles)
            {
                // Not all points in a profile contain valid data so filter
                // out the bad ones with this convenience function.
                foreach (var point in p.GetValidXYPoints())
                {
                    if (point.Y > maxPoint.Y)
                    {
                        maxPoint = point;
                    }
                }
            }

            Console.WriteLine($"Scan head {scanHead.ID}: [{maxPoint.X:F3}, {maxPoint.Y:F3}]");
            profiles.Clear();
        }
    }
    catch (OperationCanceledException)
    {
        // Thrown by TryTakeNextProfile when the token cancelled, gracefully exit
    }
}
