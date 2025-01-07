// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using System;
using System.Net;

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
        internal const int ScanSyncPacketV3ByteSize = 76; // Uses the reserved fields from V2
        internal const int ScanSyncPacketV4ByteSize = 76; // Uses the reserved fields from V3

        /// <summary>
        /// The IP address of the ScanSync.
        /// </summary>
        public IPAddress IpAddress { get; }

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

        // -- Version 3 packet data --

        /// <summary>
        /// The version of the packet data.
        /// </summary>
        internal ushort PacketVersion { get; }

        /// <summary>
        /// The version of the ScanSync.
        /// </summary>
        public ScanSyncVersionInformation Version { get; }

        // -- Version 4 packet data --

        /// <summary>
        /// Timestamp of when signal for laser disable input last went high.
        /// </summary>
        public ulong LaserDisableTimestampNs { get; }

        /// <summary>
        /// Users of this function should check that <see cref="IsValidPacketSize(Span{byte})"/>
        /// is <see langword="true"/> prior to calling this constructor.
        /// </summary>
        /// <param name="data">The raw network-ordered data.</param>
        /// <param name="ip">The IP address of the ScanSync.</param>
        /// <exception cref="ArgumentException"><paramref name="data"/> is not a valid packet size.</exception>
        internal ScanSyncData(Span<byte> data, IPAddress ip) : this()
        {
            if (data.Length < ScanSyncPacketV1ByteSize)
            {
                throw new ArgumentException($"Expected at least {ScanSyncPacketV1ByteSize} bytes of data.");
            }

            IpAddress = ip;

            int idx = 0;

            if (data.Length >= ScanSyncPacketV1ByteSize)
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
                PacketVersion = 1;
            }

            // V2 and V3 packet data is the same size
            if (data.Length >= ScanSyncPacketV2ByteSize)
            {
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

                // V2, V3, and V4 have the same data size, but V3 and V4 uses the fields that were
                // marked as "reserved" in V2. We can check the unpacked data to determine the packet
                // version since V2 used special values for the reserved fields
                ushort packetVersion = NetworkByteUnpacker.ExtractUShort(data, ref idx);
                PacketVersion = packetVersion == 0xAAAA ? (ushort)2 : packetVersion;

                if (PacketVersion >= 3)
                {
                    ushort major = NetworkByteUnpacker.ExtractUShort(data, ref idx);
                    ushort minor = NetworkByteUnpacker.ExtractUShort(data, ref idx);
                    ushort patch = NetworkByteUnpacker.ExtractUShort(data, ref idx);
                    Version = new ScanSyncVersionInformation
                    {
                        Major = major,
                        Minor = minor,
                        Patch = patch
                    };
                }

                if (PacketVersion >= 4)
                {
                    ulong lds = NetworkByteUnpacker.ExtractUInt(data, ref idx);
                    ulong ldns = NetworkByteUnpacker.ExtractUInt(data, ref idx);
                    LaserDisableTimestampNs = (lds * 1_000_000_000) + ldns;
                }
            }
        }

        internal static bool IsValidPacketSize(Span<byte> data)
        {
            return data.Length >= ScanSyncPacketV1ByteSize;
        }
    }
}