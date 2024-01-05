// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace JoeScan.Pinchot
{
    internal class MappleCorrections
    {
        [JsonPropertyName("camera_port")]
        public int CameraPort { get; set; }

        [JsonPropertyName("fit_error")]
        public double FitError { get; set; }

        [JsonPropertyName("laser_port")]
        public int LaserPort { get; set; }

        [JsonPropertyName("notes")]
        public List<string> Notes { get; set; }

        [JsonPropertyName("roll")]
        public double Roll { get; set; }

        [JsonPropertyName("timestamp_s")]
        public long TimestampS { get; set; }

        [JsonPropertyName("x_offset")]
        public double XOffset { get; set; }

        [JsonPropertyName("y_offset")]
        public double YOffset { get; set; }
    }

    internal class RestMappleCorrections
    {
        [JsonPropertyName("applied_corrections")]
        public List<MappleCorrections> AppliedCorrections { get; set; }

        [JsonPropertyName("past_corrections")]
        public List<MappleCorrections> PastCorrections { get; set; }
    }
}