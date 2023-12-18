// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Client = joescan.schema.client;
using Server = joescan.schema.server;

namespace JoeScan.Pinchot
{
    /// <summary>
    /// Information of a scan head found on the network.
    /// </summary>
    public class DiscoveredDevice
    {
        /// <summary>
        /// Gets the serial number of the scan head.
        /// </summary>
        /// <value>The serial number of the scan head.</value>
        public uint SerialNumber { get; internal set; }

        /// <summary>
        /// Gets the version of the scan head.
        /// </summary>
        /// <value>The version of the scan head.</value>
        public ScanHeadVersionInformation Version { get; internal set; }

        /// <summary>
        /// Gets the detailed name of the scan head.
        /// </summary>
        /// <value>The detailed name of the scan head.</value>
        public string ProductName { get; internal set; }

        /// <summary>
        /// Gets the <see cref="IPAddress"/> of the scan head.
        /// </summary>
        /// <value>The <see cref="IPAddress"/> of the scan head.</value>
        public IPAddress IpAddress { get; internal set; }

        /// <summary>
        /// Gets the Ethernet link speed of the scan head.
        /// </summary>
        /// <value>The Ethernet link speed of the scan head</value>
        public uint LinkSpeedMbps { get; internal set; }

        /// <summary>
        /// Gets the current state of the scan head.
        /// </summary>
        /// <value>The current state of the scan head.</value>
        public string State { get; internal set; }

        /// <summary>
        /// Checks if the scan head is compatible with the current API version.
        /// </summary>
        /// <returns>
        /// Returns <see langword="true"/> if the scan head is
        /// compatible with the API, else <see langword="false"/>.
        /// </returns>
        public bool IsCompatibleWithApi()
        {
            // Minor versions can differ, but differences in major versions are considered to be incompatible
            return Version.Major == VersionInformation.Major;
        }

        /// <summary>
        /// This IP address of the client that requested the discovery.
        /// </summary>
        internal IPAddress ClientIpAddress { get; set; }
    }

    public partial class ScanSystem
    {
        private Dictionary<uint, DiscoveredDevice> discoveries = new Dictionary<uint, DiscoveredDevice>();

        /// <summary>
        /// Performs a network discovery to determine what scan heads are on the network.
        /// </summary>
        /// <returns>A dictionary of all discovered scan heads where the key is the serial number.</returns>
        public Dictionary<uint, DiscoveredDevice> DiscoverDevices()
        {
            discoveries = Discover().GetAwaiter().GetResult();
            return discoveries;
        }

        /// <summary>
        /// Performs a network discovery to determine what scan heads are on the network.
        /// </summary>
        /// <returns>A dictionary of all discovered scan heads where the key is the serial number.</returns>
        public async Task<Dictionary<uint, DiscoveredDevice>> DiscoverDevicesAsync()
        {
            discoveries = await Discover();
            return discoveries;
        }

        /// <summary>
        /// Internal static discovery. This should not be exposed to the
        /// public to ensure the non-static version is used which has the
        /// benefit of caching.
        /// </summary>
        internal static async Task<Dictionary<uint, DiscoveredDevice>> Discover()
        {
            byte[] message = new Client::MessageClientDiscoveryT
            {
                VersionApiMajor = (uint)VersionInformation.Major,
                VersionApiMinor = (uint)VersionInformation.Minor,
                VersionApiPatch = (uint)VersionInformation.Patch
            }.SerializeToBinary();

            var activeClients = new List<UdpClient>();

            // TODO: Make the list of NICs user configurable
            foreach (var ip in GetNicIpAddresses())
            {
                var client = new UdpClient(new IPEndPoint(ip, 0));
                var serverEp = new IPEndPoint(IPAddress.Broadcast, Globals.ScanServerDiscoveryPort);
                await client.SendAsync(message, message.Length, serverEp).ConfigureAwait(false);
                activeClients.Add(client);
            }

            // TODO: Should we make this configurable?
            await Task.Delay(200).ConfigureAwait(false);

            var discoveries = new Dictionary<uint, DiscoveredDevice>();
            foreach (var client in activeClients)
            {
                while (client.Client.Available > 0)
                {
                    var res = await client.ReceiveAsync().ConfigureAwait(false);
                    var rsp = Server::MessageServerDiscoveryT.DeserializeFromBinary(res.Buffer);
                    var discovery = new DiscoveredDevice
                    {
                        IpAddress = IPAddress.Parse(rsp.IpServer.ToString()),
                        ClientIpAddress = IPAddress.Parse(rsp.IpClient.ToString()),
                        ProductName = rsp.TypeStr,
                        SerialNumber = rsp.SerialNumber,
                        LinkSpeedMbps = rsp.LinkSpeedMbps,
                        State = rsp.State.ToString(),
                        Version = new ScanHeadVersionInformation
                        {
                            Major = rsp.VersionMajor,
                            Minor = rsp.VersionMinor,
                            Patch = rsp.VersionPatch,
                            Commit = rsp.VersionCommit,
                            Flags = (ScanHeadVersionFlags)rsp.VersionFlags,
                            Type = (ProductType)rsp.Type,
                        }
                    };

                    discoveries[discovery.SerialNumber] = discovery;
                }

                client.Dispose();
            }

            return discoveries;
        }
    }
}
