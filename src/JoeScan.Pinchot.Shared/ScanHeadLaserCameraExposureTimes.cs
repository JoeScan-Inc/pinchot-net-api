// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using Newtonsoft.Json;

namespace JoeScan.Pinchot
{
    internal class ScanHeadLaserCameraExposureTimes
    {
        [JsonProperty("camera0")]
        internal LaserCameraExposureTimes Camera0 { get; set; }

        [JsonProperty("camera1")]
        internal LaserCameraExposureTimes Camera1 { get; set; }
    }

    internal class LaserCameraExposureTimes
    {
        [JsonProperty("cameraStart")]
        internal uint CameraStartTime { get; set; }

        [JsonProperty("cameraEnd")]
        internal uint CameraEndTime { get; set; }

        [JsonProperty("laserStart")]
        internal uint LaserStartTime { get; set; }

        [JsonProperty("laserEnd")]
        internal uint LaserEndTime { get; set; }
    }
}