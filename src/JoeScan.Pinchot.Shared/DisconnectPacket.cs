// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

namespace JoeScan.Pinchot
{
    internal class DisconnectPacket : PacketBase
    {
        internal DisconnectPacket()
        {
            raw = new byte[4];
            raw[0] = 0xFA;
            raw[1] = 0xCE;
            raw[2] = 4;
            raw[3] = (byte)ScanPacketType.Disconnect;
        }
    }
}