// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

// This example application shows how to use the discovery function
// to probe the network for JS-50 scan heads.

using JoeScan.Pinchot;

Console.WriteLine($"Pinchot API version: {VersionInformation.Version}");

// One of the first calls to the API should be to create a scan system.
using var scanSystem = new ScanSystem(ScanSystemUnits.Inches);

// Scan heads can be manually discovered. This is not necessary for normal
// scanning operations and should only be used as a diagnostic tool.
var discovered = scanSystem.DiscoverDevices();

Console.WriteLine($"Found {discovered.Count} devices on the network");

foreach (var (serial, device) in discovered)
{
    Console.WriteLine($"{serial}");
    Console.WriteLine($"\tProduct = {device.ProductName}");
    Console.WriteLine($"\tVersion = {device.Version}");
    Console.WriteLine($"\tIP = {device.IpAddress}");
    Console.WriteLine($"\tLink speed = {device.LinkSpeedMbps} Mbps");
}
