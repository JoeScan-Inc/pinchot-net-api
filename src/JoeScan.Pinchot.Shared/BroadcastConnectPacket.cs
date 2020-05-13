// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using System;
using System.Linq;
using System.Net;

namespace JoeScan.Pinchot
{
    internal class BroadcastConnectPacket : PacketBase
    {
        private const int ExpectedLength = 17;

        internal BroadcastConnectPacket(byte sessionId, short port, uint serialNumber,
            ConnectionType connType = ConnectionType.Normal, IPAddress clientIpAddress = null)
        {
            raw = new byte[ExpectedLength];
            raw[0] = 0xFA;
            raw[1] = 0xCE;
            raw[2] = (byte)raw.Length;
            raw[3] = (byte)ScanPacketType.BroadcastConnect;
            if (clientIpAddress == null)
            {
                raw[4] = raw[5] = raw[6] = raw[7] = 0;
            }
            else
            {
                raw[4] = clientIpAddress.GetAddressBytes()[0];
                raw[5] = clientIpAddress.GetAddressBytes()[1];
                raw[6] = clientIpAddress.GetAddressBytes()[2];
                raw[7] = clientIpAddress.GetAddressBytes()[3];
            }

            var p = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(port));
            raw[8] = p[0];
            raw[9] = p[1];
            raw[10] = sessionId;
            raw[11] = 0;
            raw[12] = (byte)connType;
            raw[13] = BitConverter.GetBytes(serialNumber)[3];
            raw[14] = BitConverter.GetBytes(serialNumber)[2];
            raw[15] = BitConverter.GetBytes(serialNumber)[1];
            raw[16] = BitConverter.GetBytes(serialNumber)[0];
        }

        internal IPAddress ClientAddress
        {
            get { return new IPAddress(raw.Skip(4).Take(4).ToArray()); }
        }

        internal short ClientPort
        {
            get { return IPAddress.NetworkToHostOrder(BitConverter.ToInt16(raw, 8)); }
        }

        internal byte Sequence
        {
            get { return raw[10]; }
        }

        internal byte ScanHeadId
        {
            get { return raw[11]; }
        }

        public override string ToString()
        {
            return
                $"RequestConnectPacket: Command: {Command}, ClientAddress: {ClientAddress}, ClientPort: {ClientPort}, Sequence: {Sequence}, ScanHeadId: {ScanHeadId}";
        }
    }
}