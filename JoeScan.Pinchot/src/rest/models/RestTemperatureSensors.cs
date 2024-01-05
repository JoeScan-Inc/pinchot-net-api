// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace JoeScan.Pinchot
{
    internal class RestTemperatureSensors
    {
        [JsonPropertyName("mainboardHumidity")]
        public float MainboardHumidity { get; set; }

        [JsonPropertyName("camera")]
        public List<float> Cameras { get; set; }

        [JsonPropertyName("mainboard")]
        public float Mainboard { get; set; }

        [JsonPropertyName("ps")]
        public float PS { get; set; }
    }
}