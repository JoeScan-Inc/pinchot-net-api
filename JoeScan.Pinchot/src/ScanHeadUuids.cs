// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using Newtonsoft.Json;
using System.Collections.Generic;

namespace JoeScan.Pinchot
{
    internal class ScanHeadUuids
    {
        [JsonProperty("camera")]
        internal List<ulong> Camera { get; set; }

        [JsonProperty("mainboard")]
        internal ulong Mainboard { get; set; }
    }
}
