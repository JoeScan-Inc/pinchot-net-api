// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

namespace JoeScan.Pinchot
{
    /// <summary>
    /// Enumeration for identifying scan data format.
    /// </summary>
    public enum DataFormat
    {
        /// <summary>
        /// Geometry and laser line brightness at full resolution.
        /// </summary>
        XYBrightnessFull = AllDataFormat.XYBrightnessFull,

        /// <summary>
        /// Geometry and laser line brightness at half resolution.
        /// </summary>
        XYBrightnessHalf = AllDataFormat.XYBrightnessHalf,

        /// <summary>
        /// Geometry and laser line brightness at quarter resolution.
        /// </summary>
        XYBrightnessQuarter = AllDataFormat.XYBrightnessQuarter,

        /// <summary>
        /// Geometry at full resolution, no laser line brightness.
        /// </summary>
        XYFull = AllDataFormat.XYFull,

        /// <summary>
        /// Geometry at half resolution, no laser line brightness.
        /// </summary>
        XYHalf = AllDataFormat.XYHalf,

        /// <summary>
        /// Geometry at quarter resolution, no laser line brightness.
        /// </summary>
        XYQuarter = AllDataFormat.XYQuarter,
    }

    internal enum SubpixelDataFormat
    {
        Subpixel = AllDataFormat.Subpixel,
        SubpixelBrightnessFull = AllDataFormat.SubpixelBrightnessFull
    }

    internal enum AllDataFormat
    {
        XYBrightnessFull = 0,
        XYBrightnessHalf,
        XYBrightnessQuarter,
        XYFull,
        XYHalf,
        XYQuarter,
        Subpixel,
        SubpixelBrightnessFull
    }
}