// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using System;

namespace JoeScan.Pinchot
{
    internal struct PacketHeader
    {
        internal ushort Magic;
        internal byte Size;
        internal ScanPacketType Type;

        internal PacketHeader(byte[] raw)
        {
            if (raw.Length < 4)
            {
                throw new ArgumentException("Invalid size for packet header");
            }

            Magic = (ushort)((raw[0] << 8) + raw[1]);
            Size = raw[2];
            Type = (ScanPacketType)raw[3];
        }
    }
}