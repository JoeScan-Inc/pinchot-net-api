// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using System;
using System.Net;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace JoeScan.Pinchot
{
    internal static class IpAddressExtensions
    {
        internal struct MacIpPair
        {
            public string MacAddress;
            public string IpAddress;
        }

        internal static IPAddress GetBroadcastAddress(this IPAddress address, IPAddress subnetMask)
        {
            byte[] ipAdressBytes = address.GetAddressBytes();
            byte[] subnetMaskBytes = subnetMask.GetAddressBytes();

            if (ipAdressBytes.Length != subnetMaskBytes.Length)
            {
                throw new ArgumentException("Lengths of IP address and subnet mask do not match.");
            }

            byte[] broadcastAddress = new byte[ipAdressBytes.Length];
            for (int i = 0; i < broadcastAddress.Length; i++)
            {
                broadcastAddress[i] = (byte)(ipAdressBytes[i] | (subnetMaskBytes[i] ^ 255));
            }

            return new IPAddress(broadcastAddress);
        }

        internal static IPAddress GetNetworkAddress(this IPAddress address, IPAddress subnetMask)
        {
            byte[] ipAdressBytes = address.GetAddressBytes();
            byte[] subnetMaskBytes = subnetMask.GetAddressBytes();

            if (ipAdressBytes.Length != subnetMaskBytes.Length)
            {
                throw new ArgumentException("Lengths of IP address and subnet mask do not match.");
            }

            byte[] broadcastAddress = new byte[ipAdressBytes.Length];
            for (int i = 0; i < broadcastAddress.Length; i++)
            {
                broadcastAddress[i] = (byte)(ipAdressBytes[i] & (subnetMaskBytes[i]));
            }

            return new IPAddress(broadcastAddress);
        }

        internal static bool IsInSameSubnet(this IPAddress address2, IPAddress address, IPAddress subnetMask)
        {
            var network1 = address.GetNetworkAddress(subnetMask);
            var network2 = address2.GetNetworkAddress(subnetMask);

            return network1.Equals(network2);
        }

        internal static bool IsIPv4LinkLocal(this IPAddress address)
        {
            return address.ToString().StartsWith("169.254", StringComparison.Ordinal);
        }

        internal static string GetMac(this IPAddress ipAddress)
        {
            var macIpPairs = GetAllMacAddressesAndIpPairs();
            int index = macIpPairs.FindIndex(x => x.IpAddress == ipAddress.ToString());
            return index >= 0 ? macIpPairs[index].MacAddress.ToUpper().Replace("-", ":") : null;
        }

        private static List<MacIpPair> GetAllMacAddressesAndIpPairs()
        {
            var mip = new List<MacIpPair>();
            var pProcess = new Process();
            pProcess.StartInfo.FileName = "arp";
            pProcess.StartInfo.Arguments = "-a ";
            pProcess.StartInfo.UseShellExecute = false;
            pProcess.StartInfo.RedirectStandardOutput = true;
            pProcess.StartInfo.CreateNoWindow = true;
            pProcess.Start();
            string cmdOutput = pProcess.StandardOutput.ReadToEnd();
            const string pattern = @"(?<ip>([0-9]{1,3}\.?){4})\s*(?<mac>([a-f0-9]{2}-?){6})";

            var ms = Regex.Matches(cmdOutput, pattern, RegexOptions.IgnoreCase);
            for (int index = 0; index < ms.Count; index++)
            {
                var m = ms[index];
                mip.Add(new MacIpPair() { MacAddress = m.Groups["mac"].Value, IpAddress = m.Groups["ip"].Value });
            }

            return mip;
        }
    }
}
