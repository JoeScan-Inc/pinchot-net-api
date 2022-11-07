// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using System;
using System.Buffers.Binary;
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
        /// The network-to-host <see cref="byte"/> conversion of <paramref name="buf"/> at <paramref name="idx"/>
        /// </returns>
        internal static byte ExtractByte(Span<byte> buf, ref int idx)
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
        /// The network-to-host <see cref="short"/> conversion of <paramref name="buf"/> at <paramref name="idx"/>
        /// </returns>
        internal static short ExtractShort(Span<byte> buf, ref int idx)
        {
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
            short data = BinaryPrimitives.ReverseEndianness(BitConverter.ToInt16(buf[idx..]));
#else
            short data = BinaryPrimitives.ReverseEndianness(BitConverter.ToInt16(buf.ToArray(), idx));
#endif
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
        /// The network-to-host <see cref="ushort"/> conversion of <paramref name="buf"/> at <paramref name="idx"/>
        /// </returns>
        internal static ushort ExtractUShort(Span<byte> buf, ref int idx)
        {
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
            ushort data = BinaryPrimitives.ReverseEndianness(BitConverter.ToUInt16(buf[idx..]));
#else
            ushort data = BinaryPrimitives.ReverseEndianness(BitConverter.ToUInt16(buf.ToArray(), idx));
#endif
            idx += sizeof(ushort);
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
        /// The network-to-host <see cref="int"/> conversion of <paramref name="buf"/> at <paramref name="idx"/>
        /// </returns>
        internal static int ExtractInt(Span<byte> buf, ref int idx)
        {
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
            int data = BinaryPrimitives.ReverseEndianness(BitConverter.ToInt32(buf[idx..]));
#else
            int data = BinaryPrimitives.ReverseEndianness(BitConverter.ToInt32(buf.ToArray(), idx));
#endif
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
        /// The network-to-host <see cref="uint"/> conversion of <paramref name="buf"/> at <paramref name="idx"/>
        /// </returns>
        internal static uint ExtractUInt(Span<byte> buf, ref int idx)
        {
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
            uint data = BinaryPrimitives.ReverseEndianness(BitConverter.ToUInt32(buf[idx..]));
#else
            uint data = BinaryPrimitives.ReverseEndianness(BitConverter.ToUInt32(buf.ToArray(), idx));
#endif
            idx += sizeof(uint);
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
        /// The network-to-host <see cref="long"/> conversion of <paramref name="buf"/> at <paramref name="idx"/>
        /// </returns>
        internal static long ExtractLong(Span<byte> buf, ref int idx)
        {
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
            long data = BinaryPrimitives.ReverseEndianness(BitConverter.ToInt64(buf[idx..]));
#else
            long data = BinaryPrimitives.ReverseEndianness(BitConverter.ToInt64(buf.ToArray(), idx));
#endif
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
        /// The network-to-host <see cref="ulong"/> conversion of <paramref name="buf"/> at <paramref name="idx"/>
        /// </returns>
        internal static ulong ExtractULong(Span<byte> buf, ref int idx)
        {
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
            ulong data = BinaryPrimitives.ReverseEndianness(BitConverter.ToUInt64(buf[idx..]));
#else
            ulong data = BinaryPrimitives.ReverseEndianness(BitConverter.ToUInt64(buf.ToArray(), idx));
#endif
            idx += sizeof(ulong);
            return data;
        }
    }
}