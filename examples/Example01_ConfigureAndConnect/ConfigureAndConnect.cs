// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

// This example application provides a basic example of setting up a scan for
// scanning using functions and data structures from the Pinchot API. In the
// following order, the application will:
//
//    1. Create scan system and scan head
//    2. Print out the scan head's capabilities
//    3. Set the configuration for the scan head
//    4. Build a basic phase table
//    5. Connect to the scan head
//    6. Print out the scan head's current status
//    7. Disconnect from the scan head.
//
// Further information regarding  features demonstrated in this application can
// be found online:
//
//    http://api.joescan.com/doc/v16/articles/js50-configuration.html
//    http://api.joescan.com/doc/v16/articles/phase-table.html

using JoeScan.Pinchot;

Console.WriteLine($"Pinchot API version: {VersionInformation.Version}");

// Grab the serial number of the scan head from the command line.
if (args.Length != 1)
{
    Console.WriteLine("Must provide a scan head serial number as argument.");
    return;
}

if (!uint.TryParse(args[0], out uint serialNumber))
{
    Console.WriteLine($"Argument {args[0]} cannot be parsed as a uint.");
    return;
}

// One of the first calls to the API should be to create a scan system.
// This object will be used to manage groupings of scan heads and to
// perform system-level functions such as starting and stopping scanning.
using var scanSystem = new ScanSystem(ScanSystemUnits.Inches);

// Create a scan head for the user's specified serial number and associate
// it with the scan system we just created. We'll also assign it a user
// defined ID that can be used within the application as an optional
// identifier if preferred over the serial number. Note that at this point
// we haven't connected with the physical scan head yet, but it must be
// on the network for this command to complete successfully.
var scanHead = scanSystem.CreateScanHead(serialNumber, id: 1);

// After a scan head has been created, we can query its firmware version
Console.WriteLine($"Scan head version: {scanHead.Version}");

// Many of the settings directly related to the operation of the cameras
// and lasers can be found in the `ScanHeadConfiguration` class. Refer
// to the API documentation for specific details regarding each field. For
// this example, we'll use some generic values not specifically set for any
// particular scenario.
var configuration = new ScanHeadConfiguration();
configuration.SetLaserOnTime(100, 100, 1000);
scanHead.Configure(configuration);

// Proper window selection can be crucial to successful scanning as it
// allows users to limit the region of interest for scanning which can
// filter out other sources of light that could complicate scanning.
// It is worth noting that there is an inverse relationship with the
// scan window and the overall scan rate a system can run at. Using
// larger scan windows will reduce the maximum scan rate of a system,
// whereas using a smaller scan window will increase the maximum scan rate.
var scanWindow = ScanWindow.CreateScanWindowRectangular(30.0, -30.0, -30.0, 30.0);
scanHead.SetWindow(scanWindow);

// Setting the alignment through the following function can help to correct
// for any mounting issues with a scan head that could affect the 3D measurement.
// For this example, we'll assume that the scan head is mounted perfectly such
// that the laser is pointed directly at the scan target.
scanHead.SetAlignment(0, 0, 0);

// The orientation of the scan head flips the data around the X axis in the
// event that a given JS-50 scanning device is oriented 180 degrees differently
// than other JS-50s within a system. This commonly happens if the orientation
// changes to facilitate better cable routing within a mill environment.
scanHead.Orientation = ScanHeadOrientation.CableIsUpstream;

// For this example we will create a basic phase table that utilizes all of
// the phasable elements of the scan head. Depending on the type of scan
// head, we will need to either schedule its cameras or its lasers. The
// bellow `switch` statement shows this process for each type of scan head.
switch (scanHead.Type)
{
    // camera-driven scan heads
    case ProductType.JS50WX or ProductType.JS50WSC or ProductType.JS50MX:
        // example phase table for an WX:
        //   Phase | Laser | Camera
        //     1   |   1   |   A
        //     2   |   1   |   B
        foreach (var camera in scanHead.Cameras)
        {
            scanSystem.AddPhase();
            scanSystem.AddPhaseElement(scanHead.ID, camera);
        }
        break;

    // laser-driven scan heads
    // for laser-driven scan heads, make sure to not schedule lasers
    // that coorespond to the same camera in back-to-back phases as
    // to not incur a throughput penalty
    case ProductType.JS50X6B20 or ProductType.JS50X6B30 or ProductType.JS50Z820 or ProductType.JS50Z830:
        // example phase table for an X6B:
        //   Phase | Laser | Camera
        //     1   |   1   |   B
        //     2   |   4   |   A
        //     3   |   2   |   B
        //     4   |   5   |   A
        //     5   |   3   |   B
        //     6   |   6   |   A
        int numLasers = scanHead.Lasers.Count();
        for (int i = 0; i < numLasers / 2; i++)
        {
            var laser = Laser.Laser1 + i;
            scanSystem.AddPhase();
            scanSystem.AddPhaseElement(scanHead.ID, laser);
            scanSystem.AddPhase();
            scanSystem.AddPhaseElement(scanHead.ID, laser + (numLasers / 2));
        }
    break;

    default:
        throw new InvalidOperationException($"Invalid scan head type {scanHead.Type}");
}

// We've now successfully configured the scan head. Now comes the time to
// connect to the physical scanner and transmit the configuration values
// we previously set up.
var connectTimeout = TimeSpan.FromSeconds(3);
var scanHeadsThatFailedToConnect = scanSystem.Connect(connectTimeout);
if (scanHeadsThatFailedToConnect.Count > 0)
{
    Console.WriteLine("Failed to connect to scan system.");
    return;
}

Console.WriteLine($"Connected to {scanHead.SerialNumber}");

// Now that we are connected, we can query the scan head to get its current status.
var status = scanHead.RequestStatus();
Console.WriteLine("Status");
Console.WriteLine($"\tGlobal time: {status.GlobalTimeNs} ns");
Console.WriteLine($"\tEncoder: {status.EncoderValues[Encoder.Main]}");

// The minimum scan period indicates the fastest that the scan system can
// obtain profiles. This value is dependent upon the laser on time, the
// size of the scan window, and the phase table. For this example with only
// one scan head, only its configuration affects the minimum scan period.
// With more scan heads in the scan system, they will collectively affect
// this value.
var minPeriodUs = scanSystem.GetMinScanPeriod();
Console.WriteLine($"Min scan period: {minPeriodUs} us");

// Once connected, this is the point where we could command the scan system
// to start scanning to obtain profile data from the scan heads associated
// with it. This will be the focus of a later example.

// We've accomplished what we set out to do for this example; now it's time
// to bring down our system.
scanSystem.Disconnect();