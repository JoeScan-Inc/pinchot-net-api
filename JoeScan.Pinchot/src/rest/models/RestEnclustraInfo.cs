// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using System.ComponentModel;
using System.Text.Json.Serialization;

namespace JoeScan.Pinchot
{
    internal class RestEnclustraInfo
    {
        [JsonPropertyName("serial_number")]
        public int SerialNumber { get; set; }

        [JsonPropertyName("product_family")]
        public int ProductFamily { get; set; }

        [JsonPropertyName("board_revision")]
        public int BoardRevision { get; set; }

        [JsonPropertyName("soc_part_name")]
        public string SocPartName { get; set; }

        [JsonPropertyName("gem_count")]
        public int GemCount { get; set; }

        [JsonPropertyName("usb_port_count")]
        public int UsbPortCount { get; set; }

        [JsonPropertyName("ram_size")]
        public string RamSize { get; set; }

        [JsonPropertyName("qspi_size")]
        public string QspiSize { get; set; }

        [JsonPropertyName("mmc_size")]
        public string MmcSize { get; set; }
    }
}
