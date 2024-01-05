// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using System.Text.Json.Serialization;

namespace JoeScan.Pinchot
{
    internal class RestLaserCameraExposureTimes
    {
        [JsonPropertyName("autoexpose")]
        public bool AutoexposeEnables { get; set; }

        [JsonPropertyName("cameraStart")]
        public uint CameraStartTime { get; set; }

        [JsonPropertyName("cameraEnd")]
        public uint CameraEndTime { get; set; }

        [JsonPropertyName("laserStart")]
        public uint LaserStartTime { get; set; }

        [JsonPropertyName("laserEnd")]
        public uint LaserEndTime { get; set; }
    }
}