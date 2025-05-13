// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using System;
using Server = joescan.schema.server;

namespace JoeScan.Pinchot
{
    /// <summary>
    /// Various input and error conditions of the ScanSync.
    /// </summary>
    [Flags]
    public enum EncoderFlags : uint
    {
        /// <summary>
        /// No flags are present.
        /// </summary>
        None = 0,

        /// <summary>
        /// ScanSync encoder A+/A- input connection is faulty.
        /// </summary>
        EncoderMainFaultA = Server::ScanSyncFlags.FAULT_A,

        /// <summary>
        /// ScanSync encoder B+/B- input connection is faulty.
        /// </summary>
        EncoderMainFaultB = Server::ScanSyncFlags.FAULT_B,

        /// <summary>
        /// ScanSync aux Y+/Y- input connection is faulty.
        /// </summary>
        EncoderMainFaultY = Server::ScanSyncFlags.FAULT_Y,

        /// <summary>
        /// ScanSync index Z+/Z- input connection is faulty.
        /// </summary>
        EncoderMainFaultZ = Server::ScanSyncFlags.FAULT_Z,

        /// <summary>
        /// ScanSync encoder data rate exceeds hardware capabilities.
        /// </summary>
        EncoderMainOverrun = Server::ScanSyncFlags.OVERRUN,

        /// <summary>
        /// ScanSync termination resistor pairs installed.
        /// </summary>
        EncoderMainTerminationEnable = Server::ScanSyncFlags.TERMINATION_ENABLE,

        /// <summary>
        /// ScanSync index Z input is logic high.
        /// </summary>
        EncoderMainIndexZ = Server::ScanSyncFlags.INDEX_Z,

        /// <summary>
        /// ScanSync sync input is logic high.
        /// </summary>
        EncoderMainSync = Server::ScanSyncFlags.SYNC,

        /// <summary>
        /// ScanSync Aux Y is logic high.
        /// </summary>
        EncoderMainAuxY = Server::ScanSyncFlags.AUX_Y,

        /// <summary>
        /// ScanSync sync input connection is faulty.
        /// </summary>
        EncoderMainFaultSync = Server::ScanSyncFlags.FAULT_SYNC,

        /// <summary>
        /// ScanSync laser disable is logic high.
        /// </summary>
        EncoderMainLaserDisable = Server::ScanSyncFlags.LASER_DISABLE,

        /// <summary>
        /// ScanSync laser disable input connection is faulty.
        /// </summary>
        EncoderMainFaultLaserDisable = Server::ScanSyncFlags.FAULT_LASER_DISABLE,
    }
}
