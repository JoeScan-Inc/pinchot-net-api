// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

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
        public List<DiscoveredScanSync> DiscoverScanSyncs()
        {
            // the ScanSync receiver is constantly listening for ScanSyncs, so no need
            // to do a "discovery" like the one done for scan heads
            var scanSyncs = scanSyncReceiver.GetScanSyncs().Values.OrderBy(ss => ss.SerialNumber);
            return scanSyncs.Select(ss => new DiscoveredScanSync(ss)).ToList();
        }

        /// <summary>
        /// Resets any <see cref="Encoder"/> to ScanSync mapping that has been set. Default behavior is to use the ScanSync
        /// with the lowest serial number as the <see cref="Encoder.Main"/> encoder. Further <see cref="Encoder"/> mappings
        /// are assigned to ScanSyncs in descending order of serial number.
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

            var scanSyncs = scanSyncReceiver.GetScanSyncs();

            if (!scanSyncs.ContainsKey(mainSerial))
            {
                throw new InvalidOperationException($"ScanSync {mainSerial} is not found on the network.");
            }

            if (aux1Serial.HasValue && !scanSyncs.ContainsKey(aux1Serial.Value))
            {
                throw new InvalidOperationException($"ScanSync {aux1Serial} is not found on the network.");
            }

            if (aux2Serial.HasValue && !scanSyncs.ContainsKey(aux2Serial.Value))
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
        /// the default mapping is used. Default behvaior is to use the ScanSync with the
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
                var orderedSerials = scanSyncReceiver.GetScanSyncs().Keys.OrderBy(s => s);

                // only map as many ScanSyncs as there are encoder enum values
                int count = Math.Min(orderedSerials.Count(), Enum.GetValues(typeof(Encoder)).Length);
                for (int e = 0; e < count; e++)
                {
                    mapping[(Encoder)e] = orderedSerials.ElementAt(e);
                }
            }

            return mapping;
        }

        #endregion
    }
}
