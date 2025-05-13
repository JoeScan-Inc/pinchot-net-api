// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using System.Collections.Generic;

namespace JoeScan.Pinchot
{
    internal class DiscoveredScanSyncSerialComparer : IEqualityComparer<DiscoveredScanSync>
    {
        public bool Equals(DiscoveredScanSync x, DiscoveredScanSync y)
        {
            return x.SerialNumber == y.SerialNumber;
        }

        public int GetHashCode(DiscoveredScanSync obj)
        {
            return obj.SerialNumber.GetHashCode();
        }
    }
}
