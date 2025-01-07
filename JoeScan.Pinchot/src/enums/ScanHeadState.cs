// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using System.ComponentModel;
using Server = joescan.schema.server;

namespace JoeScan.Pinchot
{
    /// <summary>
    /// The current state of a <see cref="ScanHead"/>.
    /// </summary>
    public enum ScanHeadState
    {
        /// <summary>
        /// Invalid state.
        /// </summary>
        [Description("Invalid state")]
        Invalid = Server::ScanHeadState.INVALID,

        /// <summary>
        /// The <see cref="ScanHead"/> is not connected nor scanning.
        /// </summary>
        [Description("Scan head is in standby (not connected, not scanning)")]
        Standby = Server::ScanHeadState.STANDBY,

        /// <summary>
        /// The <see cref="ScanHead"/> is connected."/>
        /// </summary>
        [Description("Scan head is connected")]
        Connected = Server::ScanHeadState.CONNECTED,

        /// <summary>
        /// The <see cref="ScanHead"/> is scanning.
        /// </summary>
        [Description("Scan head is scanning")]
        Scanning = Server::ScanHeadState.SCANNING,

        /// <summary>
        /// The <see cref="ScanHead"/> is scanning at the idle rate.
        /// </summary>
        /// <seealso cref="StartScanningOptions.IdlePeriodUs"/>
        [Description("Scan head is in an idle scanning state")]
        IdleScanning = Server::ScanHeadState.SCANNING_IDLE,
    }
}
