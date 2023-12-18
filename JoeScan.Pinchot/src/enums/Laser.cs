// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using System.ComponentModel;

namespace JoeScan.Pinchot
{
    /// <summary>
    /// Enumeration for identifying a laser on a scan head.
    /// </summary>
    public enum Laser
    {
        /// <summary>
        /// Invalid laser.
        /// </summary>
        [Description("Invalid Laser")]
        Invalid = 0,

        /// <summary>
        /// Laser 1.
        /// </summary>
        [Description("Laser 1")]
        Laser1,

        /// <summary>
        /// Laser 2.
        /// </summary>
        [Description("Laser 2")]
        Laser2,

        /// <summary>
        /// Laser 3.
        /// </summary>
        [Description("Laser 3")]
        Laser3,

        /// <summary>
        /// Laser 4.
        /// </summary>
        [Description("Laser 4")]
        Laser4,

        /// <summary>
        /// Laser 5.
        /// </summary>
        [Description("Laser 5")]
        Laser5,

        /// <summary>
        /// Laser 6.
        /// </summary>
        [Description("Laser 6")]
        Laser6,

        /// <summary>
        /// Laser 7.
        /// </summary>
        [Description("Laser 7")]
        Laser7,

        /// <summary>
        /// Laser 8.
        /// </summary>
        [Description("Laser 8")]
        Laser8,
    }
}