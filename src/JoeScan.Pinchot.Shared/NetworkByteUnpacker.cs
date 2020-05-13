// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using System;
using System.Net;

namespace JoeScan.Pinchot
{
    internal static class NetworkByteUnpacker
    {
        /// <summary>
        /// Returns the network-to-host conversion of the contents of <paramref name="buf"/>
        /// at <paramref name="idx"/> and increments <paramref name="idx"/> by the
        /// size of the returned type
        /// </summary>
        /// <param name="buf">The raw network data buffer</param>
        /// <param name="idx">The index of where to extract the data from</param>
        /// <returns>
        /// The network-to-host byte conversion of <paramref name="buf"/> at <paramref name="idx"/>
        /// </returns>
        internal static byte ExtractByteFromNetworkBuffer(byte[] buf, ref int idx)
        {
            byte data = buf[idx];
            idx += sizeof(byte);
            return data;
        }

        /// <summary>
        /// Returns the network-to-host conversion of the contents of <paramref name="buf"/>
        /// at <paramref name="idx"/> and increments <paramref name="idx"/> by the
        /// size of the returned type
        /// </summary>
        /// <param name="buf">The raw network data buffer</param>
        /// <param name="idx">The index of where to extract the data from</param>
        /// <returns>
        /// The network-to-host short conversion of <paramref name="buf"/> at <paramref name="idx"/>
        /// </returns>
        internal static short ExtractShortFromNetworkBuffer(byte[] buf, ref int idx)
        {
            short data = IPAddress.NetworkToHostOrder(BitConverter.ToInt16(buf, idx));
            idx += sizeof(short);
            return data;
        }

        /// <summary>
        /// Returns the network-to-host conversion of the contents of <paramref name="buf"/>
        /// at <paramref name="idx"/> and increments <paramref name="idx"/> by the
        /// size of the returned type
        /// </summary>
        /// <param name="buf">The raw network data buffer</param>
        /// <param name="idx">The index of where to extract the data from</param>
        /// <returns>
        /// The network-to-host unsigned short conversion of <paramref name="buf"/> at <paramref name="idx"/>
        /// </returns>
        internal static ushort ExtractUShortFromNetworkBuffer(byte[] buf, ref int idx)
        {
            return (ushort)ExtractShortFromNetworkBuffer(buf, ref idx);
        }

        /// <summary>
        /// Returns the network-to-host conversion of the contents of <paramref name="buf"/>
        /// at <paramref name="idx"/> and increments <paramref name="idx"/> by the
        /// size of the returned type
        /// </summary>
        /// <param name="buf">The raw network data buffer</param>
        /// <param name="idx">The index of where to extract the data from</param>
        /// <returns>
        /// The network-to-host integer conversion of <paramref name="buf"/> at <paramref name="idx"/>
        /// </returns>
        internal static int ExtractIntFromNetworkBuffer(byte[] buf, ref int idx)
        {
            int data = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(buf, idx));
            idx += sizeof(int);
            return data;
        }

        /// <summary>
        /// Returns the network-to-host conversion of the contents of <paramref name="buf"/>
        /// at <paramref name="idx"/> and increments <paramref name="idx"/> by the
        /// size of the returned type
        /// </summary>
        /// <param name="buf">The raw network data buffer</param>
        /// <param name="idx">The index of where to extract the data from</param>
        /// <returns>
        /// The network-to-host unsigned int conversion of <paramref name="buf"/> at <paramref name="idx"/>
        /// </returns>
        internal static uint ExtractUIntFromNetworkBuffer(byte[] buf, ref int idx)
        {
            return (uint)ExtractIntFromNetworkBuffer(buf, ref idx);
        }

        /// <summary>
        /// Returns the network-to-host conversion of the contents of <paramref name="buf"/>
        /// at <paramref name="idx"/> and increments <paramref name="idx"/> by the
        /// size of the returned type
        /// </summary>
        /// <param name="buf">The raw network data buffer</param>
        /// <param name="idx">The index of where to extract the data from</param>
        /// <returns>
        /// The network-to-host float conversion of <paramref name="buf"/> at <paramref name="idx"/>
        /// </returns>
        internal static float ExtractFloatFromNetworkBuffer(byte[] buf, ref int idx)
        {
            return ExtractIntFromNetworkBuffer(buf, ref idx);
        }

        /// <summary>
        /// Returns the network-to-host conversion of the contents of <paramref name="buf"/>
        /// at <paramref name="idx"/> and increments <paramref name="idx"/> by the
        /// size of the returned type
        /// </summary>
        /// <param name="buf">The raw network data buffer</param>
        /// <param name="idx">The index of where to extract the data from</param>
        /// <returns>
        /// The network-to-host long conversion of <paramref name="buf"/> at <paramref name="idx"/>
        /// </returns>
        internal static long ExtractLongFromNetworkBuffer(byte[] buf, ref int idx)
        {
            long data = IPAddress.NetworkToHostOrder(BitConverter.ToInt64(buf, idx));
            idx += sizeof(long);
            return data;
        }

        /// <summary>
        /// Returns the network-to-host conversion of the contents of <paramref name="buf"/>
        /// at <paramref name="idx"/> and increments <paramref name="idx"/> by the
        /// size of the returned type
        /// </summary>
        /// <param name="buf">The raw network data buffer</param>
        /// <param name="idx">The index of where to extract the data from</param>
        /// <returns>
        /// The network-to-host IPAddress conversion of <paramref name="buf"/> at <paramref name="idx"/>
        /// </returns>
        internal static IPAddress ExtractIPAddressFromNetworkBuffer(byte[] buf, ref int idx)
        {
            var ipAddress = new IPAddress(BitConverter.ToUInt32(buf, idx));
            idx += sizeof(uint);
            return ipAddress;
        }
    }
}