// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using System.ComponentModel;

namespace JoeScan.Pinchot
{
    /// <summary>
    /// Enumeration for identifying a camera on a scan head.
    /// </summary>
    public enum Camera
    {
        /// <summary>
        /// Invalid camera.
        /// </summary>
        [Description("Invalid Camera")]

        Invalid = 0,

        /// <summary>
        /// Camera A.
        /// </summary>
        [Description("Camera A")]
        CameraA,

        /// <summary>
        /// Camera B.
        /// </summary>
        [Description("Camera B")]
        CameraB,
    }
}