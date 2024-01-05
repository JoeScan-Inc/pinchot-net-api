// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace JoeScan.Pinchot
{
    internal class RestPowerSensors
    {
        [JsonPropertyName("voltageRail")]
        public List<float> VoltageRails { get; set; }

        [JsonPropertyName("currentRail")]
        public List<float> CurrentRails { get; set; }

        [JsonPropertyName("power")]
        public float Power { get; set; }
    }
}