// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using Newtonsoft.Json;
using System.Collections.Generic;

namespace JoeScan.Pinchot
{
    internal class ScanHeadPowerSensors
    {
        [JsonProperty("voltageRail")]
        internal List<float> VoltageRails { get; set; }

        [JsonProperty("currentRail")]
        internal List<float> CurrentRails { get; set; }

        [JsonProperty("power")]
        internal float Power { get; set; }
    }
}