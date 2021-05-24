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
        LM = 0x1,  // Brightness Data (LM, for Luminosity)
        XY = 0x2,  // XY data (XY)
        PW = 0x4,  // Width Data (PW, for Peak Width)
        VR = 0x8,  // 2nd Moment Data (VR, for Variance)
        SP = 0x10, // camera coordinates
        IM = 0x20  // Image data
    }

    /// <summary>
    /// Use this class as a way to enumerate through all <see cref="DataType"/> values.
    /// </summary>
    internal static class DataTypeValues
    {
        internal static readonly IEnumerable<DataType> DataTypes = Enum.GetValues(typeof(DataType)).Cast<DataType>();
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
            foreach (DataType value in DataTypeValues.DataTypes)
                if (input.HasFlag(value))
                    yield return value;
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
                default:
                    return 1;
            }
        }
    }
}