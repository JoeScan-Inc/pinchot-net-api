// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

namespace JoeScan.Pinchot
{
    /// <summary>
    /// Globally available variables.
    /// </summary>
    internal static class Globals
    {
        /// <summary>
        /// The raw profile data length.
        /// </summary>
        public const int RawProfileDataLength = 1456;

        /// <summary>
        /// The number of columns in an image taken from the scan head.
        /// </summary>
        public const int CameraImageDataMaxWidth = 1456;

        /// <summary>
        /// The number of rows in an image taken from the scan head.
        /// </summary>
        public const int CameraImageDataMaxHeight = 1088;

        /// <summary>
        /// The length of data from an image profile.
        /// </summary>
        public const int ImageProfileDataLength = CameraImageDataMaxHeight * CameraImageDataMaxWidth;

        /// <summary>
        /// Defined value a XY point will be assigned if it is invalid.
        /// </summary>
        public const int ProfileDataInvalidXY = -32768;

        /// <summary>
        /// The defined value for a brightness measurement if it is invalid.
        /// </summary>
        public const int ProfileDataInvalidBrightness = 0;

        // The defined value for a subpixel point will be assigned if it is invalid.
        internal const short ProfileDataInvalidSubpixel = short.MaxValue;

        // The number that a subpixel row value should be divided by in order to
        // get the "real" value as it comes from the FPGA without conversion
        internal const double SubpixelToCoordinateConversion = 32.0;

        // the maximum payload of an ethernet frame is 1500 bytes.
        // Since we want to limit our datagrams to be contained in a single ethernet frame, 
        // we split all data into datagrams with a maximum of 1500 octets/bytes.  Reserve 28
        // bytes for the IP & UDP headers.
        internal const int MaxFramePayload = 1472;

        // unique identifier for data packet
        internal const ushort MagicNumberPacketHeader = 0xFACE;

        // All UDP traffic FROM Scan Server will come from this port
        internal const int ScanServerDataPort = 12346;

        // All UDP traffic FROM Scan Sync will come to this port 
        internal const int ScanSyncClientPort = 11234;

        // All UDP traffic FROM Scan Sync will come to this port 
        internal const int ScanSyncServerPort = 62510;

        // set the buffer allocated to the UDP receiver 
        internal const int DefaultUdpBufferSize = 0x10000000;

        // profile buffer size (per scan head)
        internal const int ProfileBufferSize = 1000;
    }
}