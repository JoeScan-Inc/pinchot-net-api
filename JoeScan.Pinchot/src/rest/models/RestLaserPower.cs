// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace JoeScan.Pinchot
{
    internal class LaserPowerInfo
    {
        [JsonPropertyName("laser")]
        public uint Laser { get; set; }

        [JsonPropertyName("duty_percent")]
        public uint DutyPercent { get; set; }
    }

    internal class RestLaserPower
    {
        [JsonPropertyName("period_ns")]
        public uint PeriodNs { get; set; }

        [JsonPropertyName("lasers")]
        public List<LaserPowerInfo> Lasers { get; set; } = new List<LaserPowerInfo>();

        public RestLaserPower(uint periodNs, IEnumerable<uint> laserPorts, uint dutyPercent)
        {
            PeriodNs = periodNs;
            Lasers = laserPorts.Select(port => new LaserPowerInfo { Laser = port, DutyPercent = dutyPercent }).ToList();
        }
    }
}
