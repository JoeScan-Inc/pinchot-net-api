// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using System;
using System.Net;

namespace JoeScan.Pinchot
{
    internal class StatusPacket : PacketBase
    {
        internal StatusPacket(byte[] rawData, IPAddress from)
            : base(rawData, from)
        {
            if (rawData.Length < ScanHeadStatus.MinimumValidPacketSize)
            {
                throw new ArgumentException(
                    $"Wrong data length ({rawData.Length}) for StatusPacket, need >{ScanHeadStatus.MinimumValidPacketSize}");
            }
        }

        internal ScanHeadStatus ScanHeadStatus => new ScanHeadStatus(raw);
    }
}