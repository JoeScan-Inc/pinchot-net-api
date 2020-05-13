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
        /// Camera 0 temperature sensor.
        /// </summary>
        Camera0 = 0,

        /// <summary>
        /// Camera 1 temperature sensor.
        /// </summary>
        Camera1,

        /// <summary>
        /// Motherboard temperature sensor.
        /// </summary>
        Motherboard,
    }
}