// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

// This example application demonstrates how to configure, connect, and
// disconnect from a single scan head. For configuring the scan head, functions
// and data structures from the Pinchot API will be introduced and utilized in
// a friendly manner. Following successful configuration, the application will
// connect to the scan head, print out its current status, and then finally
// disconnect.

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

// Now that we are connected, we can query the scan head to get its current status.
var status = scanHead.RequestStatus();
Console.WriteLine("Status");
Console.WriteLine($"\tGlobal time: {status.GlobalTimeNs} ns");
Console.WriteLine($"\tEncoder: {status.EncoderValues[Encoder.Main]}");
Console.WriteLine($"\tNumber of profiles sent: {status.ProfilesSentCount}");

// Display the capabilties of the scan head. This will print out some physical
// features and functional limits of the scan head.
var capabilities = scanHead.Capabilities;
Console.WriteLine("Capabilities");
Console.WriteLine($"\tNumber of cameras: {capabilities.NumCameras}");
Console.WriteLine($"\tNumber of lasers: {capabilities.NumLasers}");
Console.WriteLine($"\tCamera image height: {capabilities.MaxCameraImageHeight}");
Console.WriteLine($"\tCamera image width: {capabilities.MaxCameraImageWidth}");
Console.WriteLine($"\tMax scan period: {capabilities.MaxScanPeriodUs}");
Console.WriteLine($"\tMin scan period: {capabilities.MinScanPeriodUs}");
Console.WriteLine($"\tMax supported encoders: {capabilities.MaxSupportedEncoders}");
Console.WriteLine($"\tBrightness bit depth: {capabilities.CameraBrightnessBitDepth}");

// Once connected, this is the point where we could command the scan system
// to start scanning to obtain profile data from the scan heads associated
// with it. This will be the focus of a later example.

// We've accomplished what we set out to do for this example; now it's time
// to bring down our system.
scanSystem.Disconnect();