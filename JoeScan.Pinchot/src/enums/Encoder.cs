// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using joescan.schema.server;

namespace JoeScan.Pinchot
{
    /// <summary>
    /// Enumeration for identifying an encoder on a scan head.
    /// </summary>
    public enum Encoder
    {
        /// <summary>
        /// Main encoder.
        /// </summary>
        Main = EncoderRole.MAIN,

        /// <summary>
        /// Auxiliary encoder.
        /// </summary>
        Auxiliary1 = EncoderRole.AUX_1,

        /// <summary>
        /// Second auxiliary encoder.
        /// </summary>
        Auxiliary2 = EncoderRole.AUX_2
    }
}