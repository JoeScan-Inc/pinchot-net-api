// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using System.Net;
using Server = joescan.schema.server;

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

        internal DiscoveredScanSync(Server::ScanSyncStatusT scanSyncStatus)
        {
            SerialNumber = scanSyncStatus.Serial;
            Version = new ScanSyncVersionInformation
            {
                Major = scanSyncStatus.FirmwareVersionMajor,
                Minor = scanSyncStatus.FirmwareVersionMinor,
                Patch = scanSyncStatus.FirmwareVersionPatch
            };
            IpAddress = new IPAddress(scanSyncStatus.IpAddr);
        }
    }
}
