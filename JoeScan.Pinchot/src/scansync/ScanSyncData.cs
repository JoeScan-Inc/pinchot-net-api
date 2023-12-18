// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using System;

namespace JoeScan.Pinchot
{
    /// <summary>
    /// Global time synchronization and encoder information sent from the ScanSync to all <see cref="ScanHead"/>s.
    /// This data should only be used for diagnostic purposes. Any relevant optimization information relating to
    /// timing or the encoder will be in the <see cref="IProfile"/>.
    /// </summary>
    public readonly struct ScanSyncData
    {
        internal const int ScanSyncPacketV1ByteSize = 32;
        internal const int ScanSyncPacketV2ByteSize = 76;

        // -- Version 1 packet data --

        /// <summary>
        /// The serial number of the ScanSync.
        /// </summary>
        public uint SerialNumber { get; }

        /// <summary>
        /// A monotonically increasing number that gets incremented for each global ScanSync update.
        /// </summary>
        public uint Sequence { get; }

        /// <summary>
        /// The time the encoder was sampled.
        /// </summary>
        public ulong EncoderTimestampNs { get; }

        /// <summary>
        /// The time the last packet was sent from the ScanSync.
        /// </summary>
        internal ulong LastTimestampNs { get; }

        /// <summary>
        /// The value of the encoder.
        /// </summary>
        public long EncoderValue { get; }

        // -- Version 2 packet data --

        /// <summary>
        /// Encoder inputs and faults.
        /// </summary>
        public EncoderFlags Flags { get; }

        /// <summary>
        /// Timestamp of when signal for aux Y input last went high.
        /// </summary>
        public ulong AuxYTimestampNs { get; }

        /// <summary>
        /// Timestamp of when signal for index Z input last went high.
        /// </summary>
        public ulong IndexZTimestampNs { get; }

        /// <summary>
        /// Timestamp of when signal for sync input last went high.
        /// </summary>
        public ulong SyncTimestampNs { get; }

        internal uint Reserved0 { get; }
        internal uint Reserved1 { get; }
        internal uint Reserved2 { get; }
        internal uint Reserved3 { get; }

        internal ScanSyncData(Span<byte> data)
        {
            int idx = 0;

            if (data.Length == ScanSyncPacketV2ByteSize)
            {
                SerialNumber = NetworkByteUnpacker.ExtractUInt(data, ref idx);
                Sequence = NetworkByteUnpacker.ExtractUInt(data, ref idx);
                ulong ets = NetworkByteUnpacker.ExtractUInt(data, ref idx);
                ulong etns = NetworkByteUnpacker.ExtractUInt(data, ref idx);
                EncoderTimestampNs = (ets * 1_000_000_000) + etns;
                ulong lts = NetworkByteUnpacker.ExtractUInt(data, ref idx);
                ulong ltns = NetworkByteUnpacker.ExtractUInt(data, ref idx);
                LastTimestampNs = (lts * 1_000_000_000) + ltns;
                EncoderValue = NetworkByteUnpacker.ExtractLong(data, ref idx);
                Flags = (EncoderFlags)NetworkByteUnpacker.ExtractUInt(data, ref idx);
                ulong auxys = NetworkByteUnpacker.ExtractUInt(data, ref idx);
                ulong auxyns = NetworkByteUnpacker.ExtractUInt(data, ref idx);
                AuxYTimestampNs = (auxys * 1_000_000_000) + auxyns;
                ulong indzs = NetworkByteUnpacker.ExtractUInt(data, ref idx);
                ulong indzns = NetworkByteUnpacker.ExtractUInt(data, ref idx);
                IndexZTimestampNs = (indzs * 1_000_000_000) + indzns;
                ulong syncs = NetworkByteUnpacker.ExtractUInt(data, ref idx);
                ulong syncns = NetworkByteUnpacker.ExtractUInt(data, ref idx);
                SyncTimestampNs = (syncs * 1_000_000_000) + syncns;
            }
            else if (data.Length == ScanSyncPacketV1ByteSize)
            {
                SerialNumber = NetworkByteUnpacker.ExtractUInt(data, ref idx);
                Sequence = NetworkByteUnpacker.ExtractUInt(data, ref idx);
                ulong ets = NetworkByteUnpacker.ExtractUInt(data, ref idx);
                ulong etns = NetworkByteUnpacker.ExtractUInt(data, ref idx);
                EncoderTimestampNs = (ets * 1_000_000_000) + etns;
                ulong lts = NetworkByteUnpacker.ExtractUInt(data, ref idx);
                ulong ltns = NetworkByteUnpacker.ExtractUInt(data, ref idx);
                LastTimestampNs = (lts * 1_000_000_000) + ltns;
                EncoderValue = NetworkByteUnpacker.ExtractLong(data, ref idx);
                Flags = EncoderFlags.None;
                AuxYTimestampNs = 0;
                IndexZTimestampNs = 0;
                SyncTimestampNs = 0;
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(data), $"Invalid ScanSync packet length {data.Length}.");
            }

            Reserved0 = 0;
            Reserved1 = 0;
            Reserved2 = 0;
            Reserved3 = 0;
        }

        internal static bool IsValidPacketSize(Span<byte> data)
        {
            return data.Length == ScanSyncPacketV1ByteSize || data.Length == ScanSyncPacketV2ByteSize;
        }
    }
}