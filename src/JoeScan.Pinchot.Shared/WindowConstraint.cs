// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

namespace JoeScan.Pinchot
{
    internal class WindowConstraint
    {
        public double X1 { get; }
        public double Y1 { get; }

        public double X2 { get; }
        public double Y2 { get; }

        public WindowConstraint(double x1, double y1, double x2, double y2)
        {
            X1 = x1;
            Y1 = y1;
            X2 = x2;
            Y2 = y2;
        }
    }
}