// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using joescan.schema.client;
using System.ComponentModel;

namespace JoeScan.Pinchot
{
    /// <summary>
    /// Enumeration for configuring the image data type when getting a diagnostic image.
    /// </summary>
    /// <seealso cref="ScanHead.GetDiagnosticCameraImage(Camera, DiagnosticImageType)"/>
    public enum DiagnosticImageType
    {
        /// <summary>
        /// Image with mask merged in
        /// </summary>
        [Description("Masked Image")]
        Masked = ImageDataType.MERGED_MASK_IMAGE,

        /// <summary>
        /// Raw image without mask
        /// </summary>
        [Description("Raw Image")]
        Raw = ImageDataType.RAW_IMAGE
    }
}
