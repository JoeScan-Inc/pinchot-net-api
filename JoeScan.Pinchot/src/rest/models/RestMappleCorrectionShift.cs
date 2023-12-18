// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using Newtonsoft.Json;

namespace JoeScan.Pinchot
{
    internal class RestMappleCorrectionShift
    {
        [JsonProperty("enabled")]
        internal bool Enabled { get; set; }
    }
}
