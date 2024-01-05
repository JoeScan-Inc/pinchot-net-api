// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

namespace JoeScan.Pinchot
{
    internal class ScanWindowConstraint
    {
        internal Point2D P0 { get; }

        internal Point2D P1 { get; }

        internal ScanWindowConstraint(Point2D p0, Point2D p1)
        {
            P0 = p0;
            P1 = p1;
        }

        internal ScanWindowConstraint(double x0, double y0, double x1, double y1)
        {
            P0 = new Point2D(x0, y0);
            P1 = new Point2D(x1, y1);
        }

        internal void Deconstruct(out Point2D p0, out Point2D p1)
        {
            p0 = P0;
            p1 = P1;
        }
    }
}
