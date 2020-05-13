// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;

namespace JoeScan.Pinchot
{
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    internal class DataPacket : PacketBase
    {
        /// <summary>
        /// Source is a unique identifier for the combination of ScanHead, Camera and Laser.
        /// </summary>
        internal int Source { get; private set; }

        internal int ScanHead { get; private set; }
        internal Camera Camera { get; private set; }
        internal Laser Laser { get; private set; }

        /// <summary>
        /// This is the head's own timestamp, not the time this datagram was received or processed.
        /// </summary>
        internal long Timestamp { get; set; }

        /// <summary>
        /// Internal timestamp to keep track of timeouts and throughput.
        /// </summary>
        internal long Received { get; private set; }

        internal int PartNum { get; private set; }
        internal int NumParts { get; private set; }
        internal int PayloadLength { get; private set; }
        internal int NumEncoderVals { get; private set; }
        internal DataType Contents { get; private set; }
        internal int NumContentTypes { get; private set; }
        internal short StartColumn { get; set; }
        internal short EndColumn { get; set; }

        internal long[] EncoderVals { get; private set; }
        internal short LaserOnTime { get; set; }
        internal short ExposureTime { get; set; }

        internal Dictionary<DataType, FragmentLayout> FragmentLayouts { get; private set; }

        internal DataPacket(byte[] packet, long receivedTimeStamp)
        {
            raw = packet;
            Received = receivedTimeStamp;
            ScanHead = packet[4];
            Camera = (Camera)packet[5];
            Laser = (Laser)packet[6];
            ExposureTime = IPAddress.NetworkToHostOrder(BitConverter.ToInt16(packet, 2));
            Timestamp = IPAddress.NetworkToHostOrder(BitConverter.ToInt64(packet, 8));
            Source = packet[4] << 3 | packet[5] << 2 | packet[6];
            PartNum = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(packet, 24));
            NumParts = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(packet, 28));
            LaserOnTime = IPAddress.NetworkToHostOrder(BitConverter.ToInt16(packet, 16));
            Contents = (DataType)IPAddress.NetworkToHostOrder(BitConverter.ToInt16(packet, 18));
            PayloadLength = IPAddress.NetworkToHostOrder(BitConverter.ToInt16(packet, 20));
            NumContentTypes = Contents.BitsSet();
            NumEncoderVals = packet[22];
            EncoderVals = new long[NumEncoderVals];
            int encOffset = NumContentTypes * 2 + 4; // offset from the end of the header at byte 32
            for (var i = 0; i < NumEncoderVals; i++)
            {
                EncoderVals[i] = IPAddress.NetworkToHostOrder(BitConverter.ToInt64(packet, 32 + encOffset + i * 8));
            }

            // Contents now holds a bitfield for how many data types are present
            StartColumn = IPAddress.NetworkToHostOrder(BitConverter.ToInt16(packet, 32));
            EndColumn = IPAddress.NetworkToHostOrder(BitConverter.ToInt16(packet, 34));

            FragmentLayouts = new Dictionary<DataType, FragmentLayout>();
            var offset = 0;
            var dataOffset = 32 + 4 + NumEncoderVals * 8 + NumContentTypes * 2;
            foreach (DataType dt in Enum.GetValues(typeof(DataType)))
            {
                if ((Contents & dt) != 0)
                {
                    var step = IPAddress.NetworkToHostOrder(BitConverter.ToInt16(packet, 32 + 4 + offset));

                    int payloadsize;
                    int numVals;
                    if (dt != DataType.IM)
                    {
                        var numCols = (int)(EndColumn - StartColumn + 1);
                        numVals = numCols / (NumParts * step);
                        // If the data doesn't divide evenly into the DataPackets, each DataPacket starting
                        // from the first will have 1 additional value of the type in question.
                        if (((numCols / step) % NumParts) > PartNum)
                        {
                            numVals++;
                        }

                        payloadsize = DataSizes.GetSizeFor(dt) * numVals;
                    }
                    else
                    {
                        numVals = PayloadLength;
                        payloadsize = PayloadLength;
                    }

                    var fl = new FragmentLayout()
                    {
                        step = step, numVals = numVals, payloadsize = payloadsize, offset = dataOffset
                    };
                    dataOffset += fl.payloadsize;
                    FragmentLayouts[dt] = fl;
                    offset += 2;
                }
            }
        }

        internal struct FragmentLayout
        {
            internal int step;
            internal int numVals;
            internal int offset;
            internal int payloadsize;
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebuggerDisplay => $"ScanHead: {ScanHead}, Camera: {Camera}, Laser: {Laser}, Source: {Source}";
    }
}