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

const DataFormat format = DataFormat.XYBrightnessFull;

// To begin scanning on all of the scan heads, all we need to do is
// command the scan system to start scanning. This will cause all of the
// scan heads associated with it to begin scanning at the specified rate
// and data format. Note the `ScanningMode` enum to tell the system to
// use frame scanning.
scanSystem.StartScanning(minScanPeriodUs, format, ScanningMode.Frame);
Console.WriteLine("Scanning...");

// Create a receiver thread to allow the main thread to do other things.
// This is especially important in GUI applications as to not lock up the
// main UI thread.
var receiver = new Thread(() => Receiver())
{
    // For applications with heavy CPU load, it is advised to boost the priority
    // of the thread reading out the frame data. If the thread reading out the
    // scan data falls behind, data will be dropped, causing problems later on
    // when trying to analyze what was scanned.
    Priority = ThreadPriority.Highest
};

receiver.Start();

try
{
    // Wait for a time to allow receiver threads to
    // collect and process a number of frames.
    var scanTime = TimeSpan.FromSeconds(5);
    await Task.Delay(scanTime, token);
}
catch (TaskCanceledException) { }

// We've collected all of our data and now it's time to stop scanning.
// Calling this function will cause each scan head within the entire
// scan system to stop scanning.
scanSystem.StopScanning();
Console.WriteLine("Stop scanning");
receiver.Join();

/// <summary>
/// This function receives frame data from a scan system.
/// </summary>
void Receiver()
{
    int frameCount = 0;
    int profilesReceived = 0;
    int profilesExpected = 0;

    while (scanSystem.IsScanning)
    {
        // Note that taking a frame uses a `ScanSystem` object instead of a `ScanHead`.
        if (!scanSystem.TryTakeFrame(out IFrame frame, TimeSpan.FromSeconds(1), token))
        {
            continue;
        }

        frameCount++;

        // An `IFrame` object contains "slots" that hold either a valid profile or `null`. The number of
        // slots is determined by the total number of unique phaseable elements there are in the system.
        // For instance, a WX would contribute 2 slots (Camera A & B) to the system whereas an X6B would
        // contribute 6 (Laser 1-6). A system of 10 WXs would therefore have a frame size of 20 slots.
        profilesExpected += frame.Count;

        for (int i = 0; i < frame.Count; i++)
        {
            IProfile? profile = frame[i];

            // Check for invalid slots. A slot can be `null` if the corresponding element is not scheduled
            // in the phase table or if something happened that causes a profile to not be received by the
            // scan system in a timely fashion (network issues, CPU performance issues, etc).
            if (profile == null)
            {
                continue;
            }

            profilesReceived++;
        }
    }

    Console.WriteLine($"Frames received: {frameCount}");
    Console.WriteLine($"Profiles received: {profilesReceived}");
    Console.WriteLine($"Profiles expected: {profilesExpected}");
}
