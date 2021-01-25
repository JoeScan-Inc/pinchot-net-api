// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using System;

namespace JoeScan.Pinchot
{
    /// <summary>
    /// Enumeration for identifying a camera on a scan head.
    /// </summary>
    public enum Camera
    {
        /// <summary>
        /// Camera A.
        /// </summary>
        CameraA = 0,

        /// <summary>
        /// Camera B.
        /// </summary>
        CameraB,

        /// <summary>
        /// Camera 0 (Deprecated. Use <see cref="CameraA"/> instead).
        /// </summary>
        [Obsolete("Use `CameraA` instead")]
        Camera0 = CameraA,

        /// <summary>
        /// Camera 1 (Deprecated. Use <see cref="CameraB"/> instead).
        /// </summary>
        [Obsolete("Use `CameraB` instead")]
        Camera1 = CameraB,

        /// <summary>
        /// Helper enum for when a configuration
        /// should be applied to all cameras
        /// </summary>
        AllCameras
    }
}