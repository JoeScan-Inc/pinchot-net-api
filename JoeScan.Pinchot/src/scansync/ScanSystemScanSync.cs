// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace JoeScan.Pinchot
{
    public partial class ScanSystem
    {
        #region Private Fields

        private readonly ScanSyncReceiver scanSyncReceiver = new ScanSyncReceiver();
        private readonly Dictionary<Encoder, uint> encoderToScanSyncMapping = new Dictionary<Encoder, uint>();

        #endregion

        #region Events

        /// <summary>
        /// This event can be used to listen for ScanSync updates for diagnostic purposes.
        /// It will be raised for every 1000 ScanSync updates or roughly once every second.
        /// </summary>
        public event EventHandler<ScanSyncUpdateEvent> ScanSyncUpdateEvent
        {
            add => scanSyncReceiver.ScanSyncUpdate += value;
            remove => scanSyncReceiver.ScanSyncUpdate -= value;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Listens for ScanSyncs on the network and report some basic information about them.
        /// This function should only be used to get a quick overview of the ScanSyncs on the network.
        /// To get more detailed information, such as encoder count and flags, subscribe to <see cref="ScanSyncUpdateEvent"/>.
        /// </summary>
        /// <returns>A list of all discovered ScanSyncs on the network.</returns>
        /// <remarks>
        /// This function will only report ScanSyncs that all the <see cref="ScanHead"/>s in the system can see.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// <see cref="IsConnected"/> is <see langword="false"/>.
        /// </exception>
        /// <exception cref="VersionCompatibilityException">
        /// This exception will be thrown if any <see cref="ScanHead"/> in the system isn't version 16.3.0 or greater.
        /// </exception>
        public List<DiscoveredScanSync> DiscoverScanSyncs()
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("Cannot discover ScanSyncs while not connected.");
            }

            foreach (var sh in ScanHeads)
            {
                sh.ThrowIfNotVersionCompatible(16, 3, 0);
            }

            // updates scan heads' cache of ScanSyncs
            Parallel.ForEach(ScanHeads, sh => _ = sh.RequestScanSyncs());

            return GetValidScanSyncs();
        }

        /// <summary>
        /// Resets any <see cref="Encoder"/> to ScanSync mapping that has been set. Default behavior is to use the ScanSync
        /// with the lowest serial number as the <see cref="Encoder.Main"/> encoder. Further <see cref="Encoder"/> mappings
        /// are assigned to ScanSyncs in ascending order of serial number.
        /// </summary>
        /// <exception cref="VersionCompatibilityException">
        /// This exception will be thrown if any <see cref="ScanHead"/> in the system isn't version 16.3.0 or greater.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// <see cref="IsConnected"/> is <see langword="false"/>.
        /// </exception>
        /// <seealso cref="SetScanSyncMapping(uint, uint?, uint?)"/>
        public void SetDefaultScanSyncMapping()
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("Cannot set ScanSync mapping while disconnected.");
            }

            foreach (var sh in ScanHeads)
            {
                sh.ThrowIfNotVersionCompatible(16, 3, 0);
            }

            encoderToScanSyncMapping.Clear();
            FlagDirty(ScanSystemDirtyStateFlags.ScanSyncMapping);
        }

        /// <summary>
        /// Sets the <see cref="Encoder"/> to ScanSync mapping.
        /// </summary>
        /// <param name="mainSerial">The serial that should be mapped to <see cref="Encoder.Main"/>.</param>
        /// <param name="aux1Serial">The serial that should be mapped to <see cref="Encoder.Auxiliary1"/>.</param>
        /// <param name="aux2Serial">The serial that should be mapped to <see cref="Encoder.Auxiliary2"/>.</param>
        /// <exception cref="ArgumentException">
        /// Any of the serial numbers are 0.<br/>
        /// -or-<br/>
        /// Any of the main and aux serial numbers are the same.<br/>
        /// -or-<br/>
        /// Aux 2 is mapped to an encoder without mapping aux 1.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// <see cref="IsConnected"/> is <see langword="false"/>.<br/>
        /// -or-<br/>
        /// A ScanSync with the supplied serial isn't found on the network.
        /// </exception>
        /// <exception cref="VersionCompatibilityException">
        /// This exception will be thrown if any <see cref="ScanHead"/> in the system isn't version 16.3.0 or greater.
        /// </exception>
        /// <seealso cref="DiscoverScanSyncs"/>
        /// <seealso cref="GetScanSyncMapping"/>
        public void SetScanSyncMapping(uint mainSerial, uint? aux1Serial = null, uint? aux2Serial = null)
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("Cannot set ScanSync mapping while disconnected.");
            }

            foreach (var sh in ScanHeads)
            {
                sh.ThrowIfNotVersionCompatible(16, 3, 0);
            }

            if (mainSerial == 0 || aux1Serial == 0 || aux2Serial == 0)
            {
                throw new ArgumentException("Invalid ScanSync serial.");
            }

            if (aux1Serial.HasValue && aux1Serial == mainSerial)
            {
                throw new ArgumentException("Main and aux 1 serial numbers must be different.");
            }

            if (aux2Serial.HasValue && aux2Serial == mainSerial)
            {
                throw new ArgumentException("Main and aux 2 serial numbers must be different.");
            }

            if (aux1Serial.HasValue && aux2Serial.HasValue && aux1Serial == aux2Serial)
            {
                throw new ArgumentException("Aux 1 and aux 2 serial numbers must be different.");
            }

            if (!aux1Serial.HasValue && aux2Serial.HasValue)
            {
                throw new ArgumentException("Can't map aux 2 to an encoder without mapping aux 1.");
            }

            var validSerials = DiscoverScanSyncs().Select(ss => ss.SerialNumber);

            if (!validSerials.Contains(mainSerial))
            {
                throw new InvalidOperationException($"ScanSync {mainSerial} is not found on the network.");
            }

            if (aux1Serial.HasValue && !validSerials.Contains(aux1Serial.Value))
            {
                throw new InvalidOperationException($"ScanSync {aux1Serial} is not found on the network.");
            }

            if (aux2Serial.HasValue && !validSerials.Contains(aux2Serial.Value))
            {
                throw new InvalidOperationException($"ScanSync {aux2Serial} is not found on the network.");
            }

            encoderToScanSyncMapping[Encoder.Main] = mainSerial;

            if (aux1Serial.HasValue)
            {
                encoderToScanSyncMapping[Encoder.Auxiliary1] = aux1Serial.Value;
            }

            if (aux2Serial.HasValue)
            {
                encoderToScanSyncMapping[Encoder.Auxiliary2] = aux2Serial.Value;
            }

            FlagDirty(ScanSystemDirtyStateFlags.ScanSyncMapping);
        }

        /// <summary>
        /// Gets the <see cref="Encoder"/> to ScanSync mapping.
        /// If <see cref="SetScanSyncMapping(uint, uint?, uint?)"/> hasn't been called,
        /// the default mapping is used. Default behavior is to use the ScanSync with the
        /// lowest serial number as the <see cref="Encoder.Main"/> encoder.
        /// Further <see cref="Encoder"/> mappings are assigned to ScanSyncs in ascending
        /// order of serial number.
        /// </summary>
        /// <returns>A dictionary representing the <see cref="Encoder"/> to ScanSync serial mapping.</returns>
        /// <exception cref="InvalidOperationException">
        /// <see cref="IsConnected"/> is <see langword="false"/>.
        /// </exception>
        /// <exception cref="VersionCompatibilityException">
        /// This exception will be thrown if any <see cref="ScanHead"/> in the system isn't version 16.3.0 or greater.
        /// </exception>
        public Dictionary<Encoder, uint> GetScanSyncMapping()
        {
            foreach (var sh in ScanHeads)
            {
                sh.ThrowIfNotVersionCompatible(16, 3, 0);
            }

            if (!IsConnected)
            {
                throw new InvalidOperationException("Cannot get ScanSync mapping while disconnected.");
            }

            // if user set a mapping, return it
            if (encoderToScanSyncMapping.Count > 0)
            {
                // make copy to avoid modifying the original
                return encoderToScanSyncMapping.ToDictionary(map => map.Key,
                                                             map => map.Value);
            }

            var validScanSyncs = GetValidScanSyncs();

            // only map as many ScanSyncs as there are encoder enum values
            int count = Math.Min(validScanSyncs.Count, Enum.GetValues(typeof(Encoder)).Length);
            var mapping = new Dictionary<Encoder, uint>(count);
            for (int e = 0; e < count; e++)
            {
                mapping[(Encoder)e] = validScanSyncs[e].SerialNumber;
            }

            return mapping;
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Gets a list of ScanSyncs that are valid for the current system.
        /// This means that the ScanSyncs are seen by all <see cref="ScanHead"/>s
        /// in the system and that they are also seen by the API.
        /// </summary>
        /// <returns>A list of valid ScanSyncs in ascending order by serial.</returns>
        internal List<DiscoveredScanSync> GetValidScanSyncs()
        {
            // get all ScanSyncs seen by each scan head
            // filter out `null` from scan heads without ScanSyncs or that just haven't been queried yet
            var allScanHeadScanSyncs = ScanHeads.Select(sh => sh.CachedScanSyncs).Where(css => css != null);

            // if no ScanSyncs are seen, don't bother with the rest
            if (!allScanHeadScanSyncs.Any())
            {
                return new List<DiscoveredScanSync>();
            }

            // get only ScanSyncs seen by every head
            var scanHeadScanSyncs = allScanHeadScanSyncs.Aggregate((l1, l2) => l1.Intersect(l2, new DiscoveredScanSyncSerialComparer()));

            // returns only ScanSyncs seen by both the scan heads and the API
            var apiSerials = scanSyncReceiver.GetScanSyncs().Keys;
            var validScanSyncs = scanHeadScanSyncs.Where(ss => apiSerials.Contains(ss.SerialNumber));

            // order by serial number in ascending order to ensure that the
            // Main ScanSync is the first one if a mapping hasn't been set
            return validScanSyncs.OrderBy(s => s.SerialNumber).ToList();
        }

        /// <summary>
        /// Gets the most recent timestamp from the <see cref="Encoder.Main"/> ScanSync.
        /// This takes into account the firmware version of the scan heads where pre-16.3.0
        /// firmware uses whatever the API can find on the network, while post-16.3.0 uses
        /// the ScanSync discovery cache.
        /// </summary>
        /// <returns>
        /// The most recent timestamp from the <see cref="Encoder.Main"/> ScanSync or 0
        /// if the scan head should determine its own start time.
        /// </returns>
        internal ulong GetMainTimestamp()
        {
            ulong lastTimestampNs = 0;

            // If any heads have firwmare lower than 16.3.0, we need to get the main ScanSync encoder
            // timestamp the old way. This is due to the ScanSync mapping feature requiring a new TCP
            // message, which is not available in older firmware.
            if (ScanHeads.Any(sh => !sh.IsVersionCompatible(16, 3, 0)))
            {
                // Get the ScanSyncs found on the network
                var activeScanSyncs = scanSyncReceiver.GetScanSyncs();

                // If ScanSyncs are found, use the Main ScanSync to get the latest timestamp
                if (activeScanSyncs.Count != 0)
                {
                    // Use the lowest serial number as Main if multiple ScanSyncs are found
                    var mainScanSync = activeScanSyncs.OrderBy(s => s.Key).First();
                    var scanSyncData = mainScanSync.Value;
                    lastTimestampNs = scanSyncData.EncoderTimestampNs;
                }
            }
            else
            {
                var mapping = GetScanSyncMapping();

                // if there is a Main ScanSync, get the most recent timestamp from it
                if (mapping.TryGetValue(Encoder.Main, out uint mainSerial))
                {
                    if (scanSyncReceiver.TryGetScanSyncData(mainSerial, out var data))
                    {
                        lastTimestampNs = data.EncoderTimestampNs;
                    }
                    else
                    {
                        ThrowInvalidOperationException($"ScanSync {mainSerial} is not found on the network.");
                    }
                }
            }

            return lastTimestampNs;
        }

        #endregion
    }
}
