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
        EncoderMainFaultA = 1 << 0,

        /// <summary>
        /// ScanSync encoder B+/B- input connection is faulty.
        /// </summary>
        EncoderMainFaultB = 1 << 1,

        /// <summary>
        /// ScanSync aux Y+/Y- input connection is faulty.
        /// </summary>
        EncoderMainFaultY = 1 << 2,

        /// <summary>
        /// ScanSync index Z+/Z- input connection is faulty.
        /// </summary>
        EncoderMainFaultZ = 1 << 3,

        /// <summary>
        /// ScanSync encoder data rate exceeds hardware capabilities.
        /// </summary>
        EncoderMainOverrun = 1 << 4,

        /// <summary>
        /// ScanSync termination resistor pairs installed.
        /// </summary>
        EncoderMainTerminationEnable = 1 << 5,

        /// <summary>
        /// ScanSync index Z input is logic high.
        /// </summary>
        EncoderMainIndexZ = 1 << 6,

        /// <summary>
        /// ScanSync sync input is logic high.
        /// </summary>
        EncoderMainSync = 1 << 7,
    }
}
