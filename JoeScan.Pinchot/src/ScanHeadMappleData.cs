// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

namespace JoeScan.Pinchot
{
    internal class ScanHeadMappleData
    {
        internal uint CameraPort { get; }
        internal uint LaserPort { get; }

        /// <summary>
        /// Should be accessed with index [row, col]
        /// </summary>
        internal short[,] XValues { get; }

        /// <summary>
        /// Should be accessed with index [row, col]
        /// </summary>
        internal short[,] YValues { get; }

        /// <summary>
        /// Should be accessed with index [row, col]
        /// </summary>
        internal bool[,] Window { get; }

        internal ScanHeadMappleData(uint cameraPort, uint laserPort, uint rows, uint columns)
        {
            CameraPort = cameraPort;
            LaserPort = laserPort;
            XValues = new short[rows, columns];
            YValues = new short[rows, columns];
            Window = new bool[rows, columns];
        }
    }
}
