// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using Newtonsoft.Json;

namespace JoeScan.Pinchot
{
    internal class ScanHeadPowerSensors
    {
        [JsonProperty("currentRail0")]
        internal float CurrentRail0 { get; set; }

        [JsonProperty("currentRail1")]
        internal float CurrentRail1 { get; set; }

        [JsonProperty("currentRail2")]
        internal float CurrentRail2 { get; set; }

        [JsonProperty("currentRail3")]
        internal float CurrentRail3 { get; set; }

        [JsonProperty("power")]
        internal float Power { get; set; }

        [JsonProperty("voltageRail0")]
        internal float VoltageRail0 { get; set; }

        [JsonProperty("voltageRail1")]
        internal float VoltageRail1 { get; set; }

        [JsonProperty("voltageRail2")]
        internal float VoltageRail2 { get; set; }

        [JsonProperty("voltageRail3")]
        internal float VoltageRail3 { get; set; }
    }
}