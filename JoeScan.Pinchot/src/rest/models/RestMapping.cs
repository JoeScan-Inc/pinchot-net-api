// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using Newtonsoft.Json;
using System.Collections.Generic;

namespace JoeScan.Pinchot
{
    /// <summary>
    /// Logical to physical mappings
    /// </summary>
    internal class RestMapping
    {
        /// <summary>
        /// Use the camera ID as an offset into this array to get
        /// the physical camera port mapping
        /// </summary>
        [JsonProperty("camera")]
        internal List<uint> Camera { get; set; }
    }
}
