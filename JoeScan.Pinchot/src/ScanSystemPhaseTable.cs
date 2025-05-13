// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace JoeScan.Pinchot
{
    /// <summary>
    /// An entry in a phase within a phase table.
    /// </summary>
    internal partial class PhaseElement
    {
        internal ScanHead ScanHead;
        internal uint CameraPort;
        internal uint LaserPort;
        internal ScanHeadConfiguration Configuration; // TODO: Change this to a nullable type with ticket #1176

        // Gets the max laser on time from the scan head unless a custom one was set
        internal uint MaxLaserOnTimeUs =>
                Configuration == null
                    ? ScanHead.Configuration.MaxLaserOnTimeUs
                    : Configuration.MaxLaserOnTimeUs;
    }

    /// <summary>
    /// A phase within a phase table. Contains all the <see cref="PhaseElement"/>s
    /// that should finish exposing at the same time.
    /// </summary>
    internal partial class Phase
    {
        internal List<PhaseElement> Elements { get; } = new List<PhaseElement>();

        /// <summary>
        /// Adds an element to the current phase. Designates the camera/laser pair
        /// of a scan head that should expose together. If <paramref name="configuration"/>
        /// is not null, then that configuration will be used instead of the scan head's defaults.
        /// </summary>
        internal void AddElement(ScanHead scanHead, CameraLaserPair pair, ScanHeadConfiguration configuration = null)
        {
            uint cameraPort = scanHead.CameraIdToPort(pair.Camera);
            uint laserPort = scanHead.LaserIdToPort(pair.Laser);

            if (Elements.Any(e => e.ScanHead == scanHead && e.CameraPort == cameraPort))
            {
                throw new InvalidOperationException("Camera cannot be used twice in the same phase.");
            }

            var pe = new PhaseElement
            {
                ScanHead = scanHead,
                CameraPort = cameraPort,
                LaserPort = laserPort,
                Configuration = configuration,
            };

            Elements.Add(pe);
        }
    }

    public partial class ScanSystem
    {
        private readonly List<Phase> phaseTable = new List<Phase>();

        internal int NumberOfPhases => phaseTable.Count;

        internal int NumberOfPhaseElements => phaseTable.Sum(p => p.Elements.Count);

        /// <summary>
        /// Clears all phases and elements added to the phase table.
        /// </summary>
        public void ClearPhaseTable()
        {
            phaseTable.Clear();
            FlagDirty(ScanSystemDirtyStateFlags.PhaseTable);
        }

        /// <summary>
        /// Adds an empty phase to the end of the phase table. Elements can be added to this
        /// newly created phase by calling <see cref="AddPhaseElement(uint, Camera)"/> or
        /// <see cref="AddPhaseElement(uint, Laser)"/> after this function has been called.
        /// </summary>
        public void AddPhase()
        {
            phaseTable.Add(new Phase());
            FlagDirty(ScanSystemDirtyStateFlags.PhaseTable);
        }

        /// <summary>
        /// Adds a <paramref name="camera"/> from the <see cref="ScanHead"/> with <paramref name="id"/> to the current phase.
        /// </summary>
        /// <param name="id">The <see cref="ScanHead"/> ID.</param>
        /// <param name="camera">The <see cref="Camera"/> that should expose.</param>
        /// <exception cref="InvalidOperationException">
        /// The <see cref="ScanHead"/> ID doesn't exist in the scan system.<br/>
        /// -or-<br/>
        /// The <see cref="ScanHead"/> is laser driven, and
        /// <see cref="AddPhaseElement(uint, Laser)"/> should be used instead<br/>
        /// -or-<br/>
        /// A phase hasn't been created yet with <see cref="AddPhase"/><br/>
        /// -or-<br/>
        /// The <paramref name="camera"/> has already been used in the current phase.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// The <paramref name="camera"/> specified does not exist on the scan head.
        /// </exception>
        public void AddPhaseElement(uint id, Camera camera)
        {
            if (!idToScanHead.TryGetValue(id, out var scanHead))
            {
                throw new InvalidOperationException($"Scan head with id {id} doesn't exist.");
            }

            var pair = scanHead.GetPair(camera);
            AddPhaseElement(scanHead, pair);
        }

        /// <summary>
        /// Adds a <paramref name="camera"/> from the <see cref="ScanHead"/> with <paramref name="id"/> to the current phase.
        /// Overwrites the <see cref="ScanHeadConfiguration"/> that was applied with <see cref="ScanHead.Configure(ScanHeadConfiguration)"/>
        /// with <paramref name="configuration"/> instead.
        /// </summary>
        /// <param name="id">The <see cref="ScanHead"/> ID.</param>
        /// <param name="camera">The <see cref="Camera"/> that should expose.</param>
        /// <param name="configuration">The <see cref="ScanHeadConfiguration"/> that will be applied to the element for current phase.</param>
        /// <remarks>
        /// Only the <see cref="ScanHeadConfiguration.MaxLaserOnTimeUs"/>, <see cref="ScanHeadConfiguration.DefaultLaserOnTimeUs"/>, and
        /// <see cref="ScanHeadConfiguration.MinLaserOnTimeUs"/> are overwritten.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// The <see cref="ScanHead"/> ID doesn't exist in the scan system.<br/>
        /// -or-<br/>
        /// The <see cref="ScanHead"/> is laser driven, and
        /// <see cref="AddPhaseElement(uint, Laser)"/> should be used instead<br/>
        /// -or-<br/>
        /// A phase hasn't been created yet with <see cref="AddPhase"/><br/>
        /// -or-<br/>
        /// The <paramref name="camera"/> has already been used in the current phase.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// The <paramref name="camera"/> specified does not exist on the scan head.
        /// </exception>
        public void AddPhaseElement(uint id, Camera camera, ScanHeadConfiguration configuration)
        {
            if (!idToScanHead.TryGetValue(id, out var scanHead))
            {
                throw new InvalidOperationException($"Scan head with id {id} doesn't exist.");
            }

            var pair = scanHead.GetPair(camera);
            var conf = configuration.Clone() as ScanHeadConfiguration;
            AddPhaseElement(scanHead, pair, conf);
        }

        /// <summary>
        /// Adds a <paramref name="laser"/> from the <see cref="ScanHead"/> with <paramref name="id"/> to the current phase.
        /// </summary>
        /// <param name="id">The <see cref="ScanHead"/> ID.</param>
        /// <param name="laser">The <see cref="Laser"/> that should be on during exposure.</param>
        /// <exception cref="InvalidOperationException">
        /// The <see cref="ScanHead"/> ID doesn't exist in the scan system.<br/>
        /// -or-<br/>
        /// The <see cref="ScanHead"/> is camera driven, and
        /// <see cref="AddPhaseElement(uint, Camera)"/> should be used instead<br/>
        /// -or-<br/>
        /// A phase hasn't been created yet with <see cref="AddPhase"/><br/>
        /// -or-<br/>
        /// The camera that is paired with <paramref name="laser"/> has already been used
        /// in the current phase.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// The <paramref name="laser"/> specified does not exist on the scan head.
        /// </exception>
        public void AddPhaseElement(uint id, Laser laser)
        {
            if (!idToScanHead.TryGetValue(id, out var scanHead))
            {
                throw new InvalidOperationException($"Scan head with id {id} doesn't exist.");
            }

            var pair = scanHead.GetPair(laser);
            AddPhaseElement(scanHead, pair);
        }

        /// <summary>
        /// Adds a <paramref name="laser"/> from the <see cref="ScanHead"/> with <paramref name="id"/> to the current phase.
        /// Overwrites the <see cref="ScanHeadConfiguration"/> that was applied with <see cref="ScanHead.Configure(ScanHeadConfiguration)"/>
        /// with <paramref name="configuration"/> instead.
        /// </summary>
        /// <param name="id">The <see cref="ScanHead"/> ID.</param>
        /// <param name="laser">The <see cref="Laser"/> that should be on during exposure.</param>
        /// <param name="configuration">The <see cref="ScanHeadConfiguration"/> that will be applied to the element for current phase.</param>
        /// <remarks>
        /// Only the <see cref="ScanHeadConfiguration.MaxLaserOnTimeUs"/>, <see cref="ScanHeadConfiguration.DefaultLaserOnTimeUs"/>, and
        /// <see cref="ScanHeadConfiguration.MinLaserOnTimeUs"/> are overwritten.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// The <see cref="ScanHead"/> ID doesn't exist in the scan system.<br/>
        /// -or-<br/>
        /// The <see cref="ScanHead"/> is camera driven, and
        /// <see cref="AddPhaseElement(uint, Camera)"/> should be used instead<br/>
        /// -or-<br/>
        /// A phase hasn't been created yet with <see cref="AddPhase"/><br/>
        /// -or-<br/>
        /// The camera that is paired with <paramref name="laser"/> has already been used
        /// in the current phase.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// The <paramref name="laser"/> specified does not exist on the scan head.
        /// </exception>
        public void AddPhaseElement(uint id, Laser laser, ScanHeadConfiguration configuration)
        {
            if (!idToScanHead.TryGetValue(id, out var scanHead))
            {
                throw new InvalidOperationException($"Scan head with id {id} doesn't exist.");
            }

            var pair = scanHead.GetPair(laser);
            var conf = configuration.Clone() as ScanHeadConfiguration;
            AddPhaseElement(scanHead, pair, conf);
        }

        /// <summary>
        /// Adds a <see cref="ScanHead"/>'s <paramref name="pair"/> to the current phase.
        /// If <paramref name="configuration"/> is set, then the element will use that configuration instead of the default one.
        /// </summary>
        private void AddPhaseElement(ScanHead scanHead, CameraLaserPair pair, ScanHeadConfiguration configuration = null)
        {
            if (phaseTable.Count == 0)
            {
                throw new InvalidOperationException("Cannot add phase element without adding a phase first.");
            }

            // Add up the number of times a scan head has appeared in the phase table and check against max groups
            int groupCount = phaseTable.Sum(p => p.Elements.Count(e => e.ScanHead == scanHead));
            uint maxGroups = scanHead.Specification.MaxConfigurationGroups;
            if (groupCount >= maxGroups)
            {
                throw new InvalidOperationException($"Cannot add scan head {scanHead.ID} more than {maxGroups} times into the phase table");
            }

            var phase = phaseTable.Last();
            phase.AddElement(scanHead, pair, configuration);
            FlagDirty(ScanSystemDirtyStateFlags.PhaseTable);
        }

        /// <summary>
        /// Calculates and returns a list of durations in µs that correspond to each phase. Takes into account
        /// the max laser on time of each element in a given phase as well as ensuring no camera's minimum
        /// scan period is violated.
        /// </summary>
        internal List<uint> CalculatePhaseDurations()
        {
            // Initialize phase durations to the element with the largest max laser on time
            var durations = new List<uint>();
            bool firstPhase = true;
            foreach (var phase in phaseTable)
            {
                uint durationNs = 0;
                foreach (var element in phase.Elements)
                {
                    uint maxOnTimeNs = element.IsStrobe
                        ? element.StrobeDurationNs
                        // account for the camera early start offset in the first
                        // phase so they aren't scheduled before the server tick
                        : (element.MaxLaserOnTimeUs * 1000) + (firstPhase ? CameraStartEarlyOffsetNs : 0);

                    if (maxOnTimeNs > durationNs)
                    {
                        durationNs = maxOnTimeNs;
                    }
                }

                durations.Add(durationNs);
                firstPhase = false;
            }

            // Calculate the frame overhead time to check for violations. The start of a given camera's
            // exposure needs to be at least frame overhead time after the end of the previous exposure
            const uint RowTimeNs = 3210;
            const uint OverheadRows = 42;
            const uint safetyMarginRows = 3;
            uint frameOverheadTimeNs = RowTimeNs * (4 + OverheadRows + safetyMarginRows);

            // Accumulator that tracks the duration between exposures for a scan head/camera port pair
            var accum = new Dictionary<Tuple<ScanHead, uint>, uint>();

            // Calculate timings to ensure that there are violations. Need to loop through phase
            // table twice to account for cameras that appear at the beginning and end of the table
            const int calcIterations = 2;
            for (int loop = 0; loop < calcIterations; ++loop)
            {
                foreach ((var phase, int phaseNumber) in phaseTable.Select((p, it) => (p, it)))
                {
                    // Bump accumulators by phase length before doing any checking
                    // since cameras finish their exposure at the end of the phase
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
                    foreach (var key in accum.Keys)
#else
                    foreach (var key in accum.Keys.ToList())
#endif
                    {
                        accum[key] += durations[phaseNumber];
                    }

                    foreach (var element in phase.Elements)
                    {
                        var sh = element.ScanHead;
                        var shCamera = new Tuple<ScanHead, uint>(sh, element.CameraPort);

                        // Check for violations, but skip strobes since they
                        // don't have the same timing requirements
                        if (accum.ContainsKey(shCamera) && !element.IsStrobe)
                        {
                            // How long it has been since the end of the camera's last exposure
                            uint lastSeenNs = accum[shCamera];

                            // Minimum scan period violation
                            uint minScanPeriodNs = sh.MinScanPeriod() * 1000;
                            int minPeriodAdjNs = (int)(minScanPeriodNs - lastSeenNs);

                            // Frame overhead time violation
                            uint maxLaserOnNs = element.MaxLaserOnTimeUs * 1000;
                            int fotAdjNs = (int)(frameOverheadTimeNs - (lastSeenNs - maxLaserOnNs));

                            // Adjust the durations if any violation occured
                            int adj = Math.Max(minPeriodAdjNs, fotAdjNs);
                            if (adj > 0)
                            {
                                durations[phaseNumber] += (uint)adj;

                                // Add time to all current accumulators since a phase that
                                // had already been accounted for grew in length
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
                                foreach (var key in accum.Keys)
#else
                                foreach (var key in accum.Keys.ToList())
#endif
                                {
                                    accum[key] += (uint)adj;
                                }
                            }
                        }

                        // Reset any element accumulators that were in this phase
                        // since the element was seen and has been checked
                        accum[shCamera] = 0;
                    }
                }
            }

            // Get the element count of the scan head with the most elements in the table
            int maxElements = ScanHeads.Max(sh => phaseTable.SelectMany(p => p.Elements.Where(e => e.ScanHead == sh && !e.IsStrobe)).Count());

            // Use the max element count to get the minimum allowed duration of the phase table
            int minDurationNs = Globals.MinScanPeriodPerElementUs * maxElements * 1000;

            // Find the calculated scan period
            int totalDurationNs = (int)durations.Sum(d => d);

            // If needed, correct durations for maximum throughput violation
            // by distributing the time delta equally across all phases
            if (totalDurationNs < minDurationNs)
            {
                int diff = minDurationNs - totalDurationNs;
                int size = durations.Count;
                int offset = (diff + size - 1) / size; // division + ceiling

                durations = durations.ConvertAll(d => d + (uint)offset);
            }

            return durations;
        }
    }
}
