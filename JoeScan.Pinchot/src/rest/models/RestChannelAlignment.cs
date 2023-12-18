// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using Newtonsoft.Json;
using System.Collections.Generic;

namespace JoeScan.Pinchot
{
    internal class RestChannelAlignment
    {
        [JsonProperty("camera")]
        internal List<ChannelAlignment> Cameras { get; set; }
    }

    internal class ChannelAlignment
    {
        [JsonProperty("channel")]
        internal List<double> Channels;
    }
}