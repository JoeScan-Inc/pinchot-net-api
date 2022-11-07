// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using System;
using System.Collections.Generic;

namespace JoeScan.Pinchot
{
    internal struct FragmentLayout
    {
        internal int step;
        internal int numVals;
        internal int offset;
        internal int payloadsize;
    }

    internal class DataPacketHeader
    {
        internal ushort Magic { get; }
        internal ushort ExposureTimeUs { get; }
        internal byte ScanHeadId { get; }
        internal uint CameraPort { get; }
        internal uint LaserPort { get; }
        internal byte Flags { get; }
        internal ulong TimestampNs { get; }
        internal ushort LaserOnTimeUs { get; }
        internal DataType DataType { get; }
        internal ushort DataLength { get; }
        internal byte NumberEncoders { get; }
        internal uint DatagramPosition { get; }
        internal uint NumberDatagrams { get; }
        internal ushort StartColumn { get; }
        internal ushort EndColumn { get; }
        internal uint SequenceNumber { get; }
        internal Dictionary<Encoder, long> Encoders { get; }
        internal Dictionary<DataType, FragmentLayout> FragmentLayouts { get; }

        internal int Source => ScanHeadId << 3 | (byte)CameraPort << 2 | (byte)LaserPort;

        internal int Length { get; }

        internal DataPacketHeader(Span<byte> buf)
        {
            int idx = 0;
            Magic = NetworkByteUnpacker.ExtractUShort(buf, ref idx);
            ExposureTimeUs = NetworkByteUnpacker.ExtractUShort(buf, ref idx);
            ScanHeadId = NetworkByteUnpacker.ExtractByte(buf, ref idx);
            CameraPort = NetworkByteUnpacker.ExtractByte(buf, ref idx);
            LaserPort = NetworkByteUnpacker.ExtractByte(buf, ref idx);
            Flags = NetworkByteUnpacker.ExtractByte(buf, ref idx);
            TimestampNs = NetworkByteUnpacker.ExtractULong(buf, ref idx);
            LaserOnTimeUs = NetworkByteUnpacker.ExtractUShort(buf, ref idx);
            DataType = (DataType)NetworkByteUnpacker.ExtractUShort(buf, ref idx);
            DataLength = NetworkByteUnpacker.ExtractUShort(buf, ref idx);
            NumberEncoders = NetworkByteUnpacker.ExtractByte(buf, ref idx);
            ++idx; // deprecated byte field
            DatagramPosition = NetworkByteUnpacker.ExtractUInt(buf, ref idx);
            NumberDatagrams = NetworkByteUnpacker.ExtractUInt(buf, ref idx);
            StartColumn = NetworkByteUnpacker.ExtractUShort(buf, ref idx);
            EndColumn = NetworkByteUnpacker.ExtractUShort(buf, ref idx);
            SequenceNumber = NetworkByteUnpacker.ExtractUInt(buf, ref idx);

            int numContentTypes = DataType.BitsSet();
            ushort[] steps = new ushort[numContentTypes];
            for (int i = 0; i < numContentTypes; ++i)
            {
                steps[i] = NetworkByteUnpacker.ExtractUShort(buf, ref idx);
            }

            Encoders = new Dictionary<Encoder, long>(NumberEncoders);
            for (int i = 0; i < NumberEncoders; ++i)
            {
                Encoder e = Encoder.Main + i;
                Encoders[e] = NetworkByteUnpacker.ExtractLong(buf, ref idx);
            }

            int stepOffset = 0;
            int numCols = EndColumn - StartColumn + 1;
            FragmentLayouts = new Dictionary<DataType, FragmentLayout>(numContentTypes);
            foreach (var dt in DataType.GetFlags())
            {
                ushort step = steps[stepOffset++];
                int numVals = (int)(numCols / (NumberDatagrams * step));

                // If the data doesn't divide evenly into the DataPackets, each DataPacket starting
                // from the first will have 1 additional value of the type in question.
                if (((numCols / step) % NumberDatagrams) > DatagramPosition)
                {
                    numVals++;
                }

                int payloadsize = dt.Size() * numVals;
                var fl = new FragmentLayout()
                {
                    step = step,
                    numVals = numVals,
                    payloadsize = payloadsize,
                    offset = idx
                };

                idx += payloadsize;
                FragmentLayouts[dt] = fl;
            }

            Length = idx;
        }
    }
}