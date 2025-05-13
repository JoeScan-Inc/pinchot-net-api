// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using System;
using System.Collections.Concurrent;
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
        public List<DiscoveredScanSync> DiscoverScanSyncs()
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("Cannot discover ScanSyncs while not connected.");
            }

            // gather all ScanSyncs seen by each head
            var allScanHeadScanSyncs = new ConcurrentBag<IEnumerable<DiscoveredScanSync>>();
            Parallel.ForEach(ScanHeads, sh =>
            {
                var ss = sh.RequestScanSyncs();
                allScanHeadScanSyncs.Add(ss);
            });

            // gets only ScanSyncs seen by every head
            var scanHeadScanSyncs = allScanHeadScanSyncs.Aggregate((l1, l2) => l1.Intersect(l2, new DiscoveredScanSyncSerialComparer()));

            // gets ScanSyncs serials seen by the API
            var apiSerials = scanSyncReceiver.GetScanSyncs().Keys;

            // returns only ScanSyncs seen by both the scan heads and the API
            return scanHeadScanSyncs.Where(ss => apiSerials.Contains(ss.SerialNumber)).ToList();
        }

        /// <summary>
        /// Resets any <see cref="Encoder"/> to ScanSync mapping that has been set. Default behavior is to use the ScanSync
        /// with the lowest serial number as the <see cref="Encoder.Main"/> encoder. Further <see cref="Encoder"/> mappings
        /// are assigned to ScanSyncs in ascending order of serial number.
        /// </summary>
        /// <exception cref="VersionCompatibilityException">
        /// This exception will be thrown if any <see cref="ScanHead"/> in the system isn't version 16.3.0 or greater.
        /// </exception>
        /// <seealso cref="SetScanSyncMapping(uint, uint?, uint?)"/>
        public void SetDefaultScanSyncMapping()
        {
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
            foreach (var sh in ScanHeads)
            {
                sh.ThrowIfNotVersionCompatible(16, 3, 0);
            }

            if (!IsConnected)
            {
                throw new InvalidOperationException("Cannot set ScanSync mapping while disconnected.");
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
        /// Further <see cref="Encoder"/> mappings are assigned to ScanSyncs in descending
        /// order of serial number.
        /// </summary>
        /// <returns>A dictionary representing the <see cref="Encoder"/> to ScanSync serial mapping.</returns>
        public Dictionary<Encoder, uint> GetScanSyncMapping()
        {
            var mapping = encoderToScanSyncMapping.ToDictionary(map => map.Key,
                                                                map => map.Value);

            // user hasn't set mapping, use default behavior
            if (mapping.Count == 0)
            {
                // default behavior is to order by serial number in ascending order
                var orderedSerials = DiscoverScanSyncs().OrderBy(s => s.SerialNumber);

                // only map as many ScanSyncs as there are encoder enum values
                int count = Math.Min(orderedSerials.Count(), Enum.GetValues(typeof(Encoder)).Length);
                for (int e = 0; e < count; e++)
                {
                    mapping[(Encoder)e] = orderedSerials.ElementAt(e).SerialNumber;
                }
            }

            return mapping;
        }

        #endregion
    }
}
