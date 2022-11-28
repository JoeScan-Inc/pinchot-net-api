// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace JoeScan.Pinchot
{
    [Flags]
    internal enum DataType : ushort
    {
        Invalid = 0, // Invalid
        LM = 1 << 0, // Brightness Data (LM, for Luminosity)
        XY = 1 << 1, // XY data (XY)
        PW = 1 << 2, // Width Data (PW, for Peak Width)
        VR = 1 << 3, // 2nd Moment Data (VR, for Variance)
        SP = 1 << 4, // camera coordinates
        IM = 1 << 5  // Image data
    }

    /// <summary>
    /// Use this class as a way to enumerate through all valid <see cref="DataType"/> values.
    /// </summary>
    internal static class DataTypeValues
    {
        internal static readonly IEnumerable<DataType> DataTypes =
            Enum.GetValues(typeof(DataType))
                .Cast<DataType>()
                .Where(d => d != DataType.Invalid)
                .ToArray();// finalize the lazy linq so it is executed only once
    }

    internal static class DataTypeExtensions
    {
        internal static int BitsSet(this DataType t)
        {
            int count = 0;
            while (t > 0)
            {
                t &= (t - 1);
                count++;
            }

            return count;
        }

        internal static IEnumerable<DataType> GetFlags(this DataType input)
        {
            foreach (var value in DataTypeValues.DataTypes)
            {
                if (input.HasFlag(value))
                {
                    yield return value;
                }
            }
        }

        internal static int Size(this DataType t)
        {
            switch (t)
            {
                case DataType.LM:
                case DataType.IM:
                    return 1;

                case DataType.XY:
                    return 4;

                case DataType.SP:
                case DataType.PW:
                case DataType.VR:
                    return 2;

                case DataType.Invalid:
                    throw new ArgumentException("Invalid data type", nameof(t));
                default:
                    throw new ArgumentException("Unknown data type", nameof(t));
            }
        }
    }
}