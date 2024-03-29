// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.
using System.Text.Json.Serialization;

namespace JoeScan.Pinchot
{
    internal class IntrinsicCorrection
    {
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; }
    }
}
