// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using System;

namespace JoeScan.Pinchot
{
    /// <summary>
    /// Globally available variables.
    /// </summary>
    internal static class Globals
    {
        #region Public Fields

        /// <summary>
        /// The raw profile data length.
        /// </summary>
        public const int RawProfileDataLength = 1456;

        /// <summary>
        /// Defined value an XY point will be assigned if it is invalid.
        /// </summary>
        public const double ProfileDataInvalidXY = double.NaN;

        /// <summary>
        /// The defined value for a brightness measurement if it is invalid.
        /// </summary>
        public const int ProfileDataInvalidBrightness = 0;

        #endregion

        #region Internal Fields

        /// <summary>
        /// Defined value an XY point will be assigned if it is invalid from the server.
        /// </summary>
        internal const int ServerProfileDataInvalidXY = short.MinValue;

        /// <summary>
        /// The defined value for a subpixel point will be assigned if it is invalid from the server.
        /// </summary>
        internal const short ServerProfileDataInvalidSubpixel = short.MaxValue;

        /// <summary>
        /// The number that a subpixel row value should be divided by in order to
        /// get the "real" value as it comes from the FPGA without conversion
        /// </summary>
        internal const double SubpixelToCoordinateConversion = 32.0;

        /// <summary>
        /// Unique identifier for data packet
        /// </summary>
        internal const ushort DataMagic = 0xFACD;

        /// <summary>
        /// TCP control messages to/from scan server
        /// </summary>
        internal const int ScanServerControlPort = 12346;

        /// <summary>
        /// Broadcast discovery destination port
        /// </summary>
        internal const int ScanServerDiscoveryPort = 12347;

        /// <summary>
        /// TCP data from scan server
        /// </summary>
        internal const int ScanServerDataPort = 12348;

        /// <summary>
        /// ScanSync UDP destination port
        /// </summary>
        internal const int ScanSyncClientPort = 11234;

        /// <summary>
        /// ScanSync UDP server origin port
        /// </summary>
        internal const int ScanSyncServerPort = 62510;

        /// <summary>
        /// Buffer size for receiving data packets from scan server
        /// </summary>
        internal const int ReceiveDataBufferSize = 0x10000000;

        /// <summary>
        /// Profile buffer size (per scan head)
        /// </summary>
        internal const int ProfileQueueSize = 1000;

        #endregion
    }
}
