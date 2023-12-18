// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

// This application shows the fundamentals of how to stream profile data
// from scan heads up through the client API and into your own code. Each scan
// head will be initially configured before scanning using generous settings
// that should guarantee that valid profile data is obtained. Following
// configuration, a limited number of profiles will be collected before halting
// the scan and disconnecting from the scan heads.

using JoeScan.Pinchot;

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

// For this example application, we'll just use the same configuration
// settings we made use of in the "ConfigureAndConnect" example. The
// only real difference here is that we will be applying this configuration
// to multiple scan heads, using a "foreach" loop to configure each scan head
// one after the other.
var configuration = new ScanHeadConfiguration();
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

// Create a dictionary with scan head IDs as keys and profile lists as
// values to gather all the profiles into one data structure.
var allProfiles = scanSystem.ScanHeads.ToDictionary(scanHead => scanHead.ID, _ => new List<IProfile>());

// We'll read out a small number of profiles for each scan head, servicing
// each one in a round robin fashion until the requested number of profiles
// have been obtained.
const int maxProfiles = 10;

do
{
    foreach (var scanHead in scanSystem.ScanHeads)
    {
        // Read out the next available profile and store it in the
        // profile buffer to be processed at a later time.
        if (!scanHead.TryTakeNextProfile(out var profile))
        {
            continue;
        }

        allProfiles[scanHead.ID].Add(profile);
    }
}
while (!allProfiles.All(p => p.Value.Count > maxProfiles));

// We've collected all of our data; time to stop scanning. Calling this
// function will cause each scan head within the entire scan system to
// stop scanning. Once we're done scanning, we'll process the data.
scanSystem.StopScanning();

foreach (var scanHead in scanSystem.ScanHeads)
{
    var maxPoint = new Point2D();
    foreach (var profile in allProfiles[scanHead.ID])
    {
        foreach (var point in profile.GetValidXYPoints())
        {
            if (point.Y > maxPoint.Y)
            {
                maxPoint = point;
            }
        }
    }

    Console.WriteLine($"Highest point from scan head {scanHead.ID}");
    Console.WriteLine($"\tX: {maxPoint.X:F3}");
    Console.WriteLine($"\tY: {maxPoint.Y:F3}");
    Console.WriteLine($"\tBrightness: {maxPoint.Brightness}");
}
