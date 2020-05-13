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
        /// Full geometric resolution, full laser line brightness resolution.
        /// </summary>
        XYFullLMFull = AllDataFormat.XYFullLMFull,

        /// <summary>
        /// Half geometric resolution, half laser line brightness resolution.
        /// </summary>
        XYHalfLMHalf = AllDataFormat.XYHalfLMHalf,

        /// <summary>
        /// Quarter geometric resolution, quarter laser line brightness resolution.
        /// </summary>
        XYQuarterLMQuarter = AllDataFormat.XYQuarterLMQuarter,

        /// <summary>
        /// Full geometric resolution.
        /// </summary>
        XYFull = AllDataFormat.XYFull,

        /// <summary>
        /// Half geometric resolution.
        /// </summary>
        XYHalf = AllDataFormat.XYHalf,

        /// <summary>
        /// Quarter geometric resolution.
        /// </summary>
        XYQuarter = AllDataFormat.XYQuarter,

        /// <summary>
        /// Full resolution image.
        /// </summary>
        Image = AllDataFormat.Image
    }

    internal enum SubpixelDataFormat
    {
        Subpixel = AllDataFormat.Subpixel,
        SubpixelFullLMFull = AllDataFormat.SubpixelFullLMFull
    }

    internal enum AllDataFormat
    {
        XYFullLMFull = 0,
        XYHalfLMFull,
        XYQuarterLMFull,
        XYFullLMHalf,
        XYHalfLMHalf,
        XYQuarterLMHalf,
        XYFullLMQuarter,
        XYHalfLMQuarter,
        XYQuarterLMQuarter,
        XYFull,
        XYHalf,
        XYQuarter,
        LMFull,
        LMHalf,
        LMQuarter,
        Image,
        Subpixel,
        SubpixelFullLMFull
    }
}