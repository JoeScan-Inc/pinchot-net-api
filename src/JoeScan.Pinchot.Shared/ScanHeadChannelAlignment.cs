// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using Newtonsoft.Json;

namespace JoeScan.Pinchot
{
    internal class ScanHeadChannelAlignment
    {
        [JsonProperty("camera0")]
        internal float[] Camera0 { get; set; }

        [JsonProperty("camera1")]
        internal float[] Camera1 { get; set; }
    }
}