// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using System.Net;

namespace JoeScan.Pinchot
{
    /// <summary>
    /// Basic information of a ScanSync found on the network.
    /// </summary>
    public readonly struct DiscoveredScanSync
    {
        /// <summary>
        /// The serial number of the ScanSync.
        /// </summary>
        public uint SerialNumber { get; }

        /// <summary>
        /// The version of the ScanSync.
        /// </summary>
        /// <remarks>This is only valid with ScanSync firmware version 2.1.0 or greater.</remarks>
        public ScanSyncVersionInformation Version { get; }

        /// <summary>
        /// The IP address of the ScanSync.
        /// </summary>
        /// <remarks>This is only valid with ScanSync firmware version 2.1.0 or greater.</remarks>
        public IPAddress IpAddress { get; }

        internal DiscoveredScanSync(uint serialNumber, ScanSyncVersionInformation version, IPAddress ipAddress)
        {
            SerialNumber = serialNumber;
            Version = version;
            IpAddress = ipAddress;
        }

        internal DiscoveredScanSync(ScanSyncData data)
        {
            SerialNumber = data.SerialNumber;
            Version = data.Version;
            IpAddress = data.IpAddress;
        }
    }
}
