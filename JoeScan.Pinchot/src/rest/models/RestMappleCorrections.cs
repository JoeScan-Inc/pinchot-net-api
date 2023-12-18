// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using Newtonsoft.Json;
using System.Collections.Generic;

namespace JoeScan.Pinchot
{
    internal class MappleCorrections
    {
        [JsonProperty("camera_port")]
        internal int CameraPort { get; set; }

        [JsonProperty("fit_error")]
        internal double FitError { get; set; }

        [JsonProperty("laser_port")]
        internal int LaserPort { get; set; }

        [JsonProperty("notes")]
        internal List<string> Notes { get; set; }

        [JsonProperty("roll")]
        internal double Roll { get; set; }

        [JsonProperty("timestamp_s")]
        internal long TimestampS { get; set; }

        [JsonProperty("x_offset")]
        internal double XOffset { get; set; }

        [JsonProperty("y_offset")]
        internal double YOffset { get; set; }
    }

    internal class RestMappleCorrections
    {
        [JsonProperty("applied_corrections")]
        internal List<MappleCorrections> AppliedCorrections { get; set; }

        [JsonProperty("past_corrections")]
        internal List<MappleCorrections> PastCorrections { get; set; }
    }
}