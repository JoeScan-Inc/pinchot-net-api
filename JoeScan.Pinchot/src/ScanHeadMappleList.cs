// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using Newtonsoft.Json;
using System.Collections.Generic;

namespace JoeScan.Pinchot
{
    internal class ScanHeadMappleList
    {
        [JsonProperty("mapples")]
        internal List<string> Mapples { get; set; }
    }
}
