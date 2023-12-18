// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using Newtonsoft.Json;
using System.Collections.Generic;

namespace JoeScan.Pinchot
{
    internal class CorrectionTableEntry
    {
        [JsonProperty("column")]
        internal int Column { get; set; }

        [JsonProperty("offset")]
        internal float Offset { get; set; }

        [JsonProperty("row")]
        internal int Row { get; set; }
    }
    
    internal class RestDefectMap
    {
        [JsonProperty("correction_table")]
        internal List<CorrectionTableEntry> CorrectionTable { get; set; }

        [JsonProperty("time")]
        internal string Time { get; set; }

        [JsonProperty("uuid")]
        internal string UUID { get; set; }

        [JsonProperty("camera_id")]
        internal int CameraId { get; set; }

        [JsonProperty("camera_port")]
        internal int CameraPort { get; set; }

        [JsonProperty("serial")]
        internal int Serial { get; set; }

        [JsonProperty("version")]
        internal float Version { get; set; }

        [JsonProperty("temperature_c")]
        internal float TemperatureC { get; set; }
    }
}
