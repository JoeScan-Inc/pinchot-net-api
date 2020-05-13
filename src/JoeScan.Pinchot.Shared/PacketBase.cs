// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using System.Net;

namespace JoeScan.Pinchot
{
    internal class PacketBase
    {
        protected byte[] raw;

        internal ScanPacketType Command
        {
            get { return (ScanPacketType)raw[3]; }
        }

        internal byte[] Raw
        {
            get { return raw; }
        }

        internal IPAddress From { get; set; }

        protected PacketBase(byte[] raw, IPAddress from)
        {
            this.raw = raw;
            From = from;
        }

        protected PacketBase()
        {
        }
    }
}