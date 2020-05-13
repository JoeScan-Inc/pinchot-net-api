// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using System;
using System.Net;

namespace JoeScan.Pinchot
{
    internal class ScanSyncPacket
    {
        private readonly byte[] raw;

        internal ScanSyncPacket(byte[] raw)
        {
            if (raw.Length != 32)
            {
                throw new ArgumentException("Raw ScanSync Packet invalid.");
            }

            this.raw = raw;
        }

        internal ScanSyncData ScanSyncData =>
            new ScanSyncData()
            {
                SerialNumber = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(raw, 0)),
                Sequence = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(raw, 4)),
                EncoderTimeStampSeconds = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(raw, 8)),
                EncoderTimeStampNanoseconds = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(raw, 12)),
                LastTimeStampSeconds = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(raw, 16)),
                LastTimeStampNanoseconds = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(raw, 20)),
                EncoderValue = IPAddress.NetworkToHostOrder(BitConverter.ToInt64(raw, 24))
            };
    }
}