// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

namespace JoeScan.Pinchot
{
    /// <summary>
    /// Enumeration for identifying a temperature sensor on a scan head.
    /// </summary>
    public enum TemperatureSensor
    {
        /// <summary>
        /// Camera A temperature sensor.
        /// </summary>
        CameraA = 0,

        /// <summary>
        /// Camera B temperature sensor.
        /// </summary>
        CameraB,

        /// <summary>
        /// Motherboard temperature sensor.
        /// </summary>
        Motherboard,
        
        /// <summary>
        /// Temperature for the Processor on SoC
        /// </summary>
        PS,
    }
}