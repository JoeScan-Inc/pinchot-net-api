// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace JoeScan.Pinchot
{
    internal class CorrectionTableEntry
    {
        [JsonPropertyName("column")]
        public int Column { get; set; }

        [JsonPropertyName("offset")]
        public float Offset { get; set; }

        [JsonPropertyName("row")]
        public int Row { get; set; }
    }

    internal class RestDefectMap
    {
        [JsonPropertyName("correction_table")]
        public List<CorrectionTableEntry> CorrectionTable { get; set; }

        [JsonPropertyName("time")]
        public string Time { get; set; }

        [JsonPropertyName("uuid")]
        public string UUID { get; set; }

        [JsonPropertyName("camera_id")]
        public int CameraId { get; set; }

        [JsonPropertyName("camera_port")]
        public int CameraPort { get; set; }

        [JsonPropertyName("serial"), JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public int Serial { get; set; }

        [JsonPropertyName("version")]
        public float Version { get; set; }

        [JsonPropertyName("temperature_c")]
        public float TemperatureC { get; set; }
    }
}
