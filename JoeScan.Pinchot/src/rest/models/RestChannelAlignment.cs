// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace JoeScan.Pinchot
{
    internal class RestChannelAlignment
    {
        [JsonPropertyName("camera")]
        public List<ChannelAlignment> Cameras { get; set; }
    }

    internal class ChannelAlignment
    {
        [JsonPropertyName("channel")]
        public List<double> Channels { get; set; }
    }
}