// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using Newtonsoft.Json;
using System.Collections.Generic;

namespace JoeScan.Pinchot
{
    internal class ScanHeadTemperatureSensors
    {
        [JsonProperty("mainboardHumidity")]
        internal float MainboardHumidity { get; set; }

        [JsonProperty("camera")]
        internal List<float> Cameras { get; set; }

        [JsonProperty("mainboard")]
        internal float Mainboard { get; set; }

        [JsonProperty("ps")]
        internal float PS { get; set; }
    }
}