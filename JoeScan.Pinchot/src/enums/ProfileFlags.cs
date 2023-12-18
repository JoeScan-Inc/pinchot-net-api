// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using System;

namespace JoeScan.Pinchot
{
    /// <summary>
    /// Various conditions that can occur at the moment the profile was captured.
    /// </summary>
    [Flags]
    public enum ProfileFlags
    {
        /// <summary>
        /// No flags are present.
        /// </summary>
        None = 0,

        /// <summary>
        /// ScanSync encoder A+/A- input connection is faulty.
        /// </summary>
        EncoderMainFaultA = EncoderFlags.EncoderMainFaultA,

        /// <summary>
        /// ScanSync encoder B+/B- input connection is faulty.
        /// </summary>
        EncoderMainFaultB = EncoderFlags.EncoderMainFaultB,

        /// <summary>
        /// ScanSync aux Y+/Y- input connection is faulty.
        /// </summary>
        EncoderMainFaultY = EncoderFlags.EncoderMainFaultY,

        /// <summary>
        /// ScanSync index Z+/Z- input connection is faulty.
        /// </summary>
        EncoderMainFaultZ = EncoderFlags.EncoderMainFaultZ,

        /// <summary>
        /// ScanSync encoder data rate exceeds hardware capabilities.
        /// </summary>
        EncoderMainOverrun = EncoderFlags.EncoderMainOverrun,

        /// <summary>
        /// ScanSync termination resistor pairs installed.
        /// </summary>
        EncoderMainTerminationEnable = EncoderFlags.EncoderMainTerminationEnable,

        /// <summary>
        /// ScanSync index Z input is logic high.
        /// </summary>
        EncoderMainIndexZ = EncoderFlags.EncoderMainIndexZ,

        /// <summary>
        /// ScanSync sync input is logic high.
        /// </summary>
        EncoderMainSync = EncoderFlags.EncoderMainSync,
    }
}
