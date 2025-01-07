using System;
using System.Collections.Generic;
using System.Linq;
using Client = joescan.schema.client;

namespace JoeScan.Pinchot
{
    /// <summary>
    /// Enums for a strobe on a <see cref="Phaser"/>.
    /// </summary>
    public enum Strobe
    {
        /// <summary>
        /// Invalid strobe.
        /// </summary>
        Invalid = 0,

        /// <summary>
        /// Strobe 1.
        /// </summary>
        Strobe1,

        /// <summary>
        /// Strobe 2.
        /// </summary>
        Strobe2,

        /// <summary>
        /// Strobe 3.
        /// </summary>
        Strobe3,

        /// <summary>
        /// Strobe 4.
        /// </summary>
        Strobe4,

        /// <summary>
        /// Strobe 5.
        /// </summary>
        Strobe5,

        /// <summary>
        /// Strobe 6.
        /// </summary>
        Strobe6,

        /// <summary>
        /// Strobe 7.
        /// </summary>
        Strobe7,

        /// <summary>
        /// Strobe 8.
        /// </summary>
        Strobe8,
    }

    /// <summary>
    /// Enums for the polarity of a strobe.
    /// </summary>
    public enum StrobePolarity
    {
        /// <summary>
        /// The strobe is active low.
        /// </summary>
        ActiveLow = Client::StrobePolarity.ACTIVE_LOW,

        /// <summary>
        /// The strobe is active high.
        /// </summary>
        ActiveHigh = Client::StrobePolarity.ACTIVE_HIGH,
    }

    // TODO: Easiest way to implement Phaser is to extend ScanHead,
    // but this may not be the best design. Consider refactoring by
    // adding a common interface/base class.
    /// <summary>
    /// An interface to a physical Phaser.
    /// </summary>
    /// <remarks>
    /// The Phaser class provides an interface to a physical Phaser by providing properties
    /// and methods for strobe configuration, status retrieval, and phasing. A Phaser
    /// must belong to a <see cref="ScanSystem"/> and is created using <see cref="ScanSystem.CreatePhaser"/>.
    /// </remarks>
    public class Phaser : ScanHead
    {
        internal Dictionary<Strobe, StrobeConfiguration> StrobeConfigurations { get; } = new Dictionary<Strobe, StrobeConfiguration>();

        /// <summary>
        /// The number of complete frames that occur between each strobe. This is inclusive, i.e., a value of 1
        /// means the strobe is on every frame. The first strobe will always happen on the first frame of the scan.
        /// </summary>
        public uint FramesPerStrobe { get; set; } = 1;

        internal Phaser(ScanSystem scanSystem, DiscoveredDevice device, uint serialNumber, uint id)
            : base(scanSystem, device, serialNumber, id)
        { }

        /// <summary>
        /// Configures the strobe with the given configuration.
        /// </summary>
        /// <param name="strobe">The strobe to configure.</param>
        /// <param name="configuration">The strobe configuration.</param>
        public void ConfigureStrobe(Strobe strobe, StrobeConfiguration configuration)
        {
            if (strobe == Strobe.Invalid)
            {
                throw new InvalidOperationException("Invalid strobe.");
            }

            StrobeConfigurations[strobe] = configuration;
            FlagDirty(ScanHeadDirtyStateFlags.Configuration);
        }

        /// <summary>
        /// Retrieves the configuration of the strobe.
        /// </summary>
        /// <param name="strobe">The <see cref="Strobe"/> to look for.</param>
        /// <param name="conf">The <see cref="StrobeConfiguration"/> for the <paramref name="strobe"/>.</param>
        /// <returns>
        /// <see langword="true"/> if the <paramref name="strobe"/> has
        /// been configured, else <see langword="false"/>.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the <paramref name="strobe"/> is <see cref="Strobe.Invalid"/>.
        /// </exception>
        public bool TryGetStrobeConfiguration(Strobe strobe, out StrobeConfiguration conf)
        {
            if (strobe == Strobe.Invalid)
            {
                throw new InvalidOperationException("Invalid strobe.");
            }

            return StrobeConfigurations.TryGetValue(strobe, out conf);
        }
    }

    /// <summary>
    /// The configuration for a strobe.
    /// </summary>
    public class StrobeConfiguration
    {
        /// <summary>
        /// This value can be used to apply an offset to the start of the strobe relative
        /// to the start of the phase. This can be a negative value to start the strobe
        /// before the phase starts.
        /// </summary>
        public double StrobeStartOffsetUs { get; set; }

        /// <summary>
        /// This value can be used to apply an offset to the end of the strobe relative
        /// to the phase end time. This can be a negative value to end the strobe
        /// before the phase ends.
        /// </summary>
        public double StrobeEndOffsetUs { get; set; }

        /// <summary>
        /// The polarity of the strobe.
        /// </summary>
        public StrobePolarity Polarity { get; set; }
    }

    public partial class ScanHead
    {
        internal CameraLaserPair GetPair(Strobe strobe)
        {
            // TODO: Define Phaser Specification. Phaser is currently using Z8 spec,
            // we have to use "Laser" instead of "Strobe" to get the pair.
            if (Specification.ConfigurationGroupPrimary != Client.ConfigurationGroupPrimary.LASER)
            {
                throw new ArgumentException("Scan head has not been programmed as a Phaser! Contact JoeScan.");
            }

            var groups = Specification.ConfigurationGroups;
            uint laserPort = LaserIdToPort((Laser)strobe);
            uint cameraPort = groups.Single(g => g.LaserPort == laserPort).CameraPort;
            var camera = CameraPortToId(cameraPort);
            return new CameraLaserPair(camera, (Laser)strobe);
        }

        internal uint StrobeIdToPort(Strobe strobe)
        {
            Laser laser = (Laser)strobe;
            int port = Specification.LaserPortToId.FindIndex(p => p == (uint)laser);
            if (port == -1)
            {
                throw new ArgumentException($"{strobe} does not exist for scan head {ID}.", nameof(strobe));
            }

            return (uint)port;
        }
    }

    internal partial class ScanHeadSenderReceiver
    {
        internal void SendPhaserConfiguration()
        {
            byte[] phaserConfReq = CreatePhaserConfigurationRequest();
            TcpSend(phaserConfReq, TcpControlStream);
        }

        private byte[] CreatePhaserConfigurationRequest()
        {
            if (!(scanHead is Phaser phaser))
            {
                throw new InvalidOperationException("Scan head is not a phaser.");
            }

            var message = new Client::MessageClientT
            {
                Type = Client.MessageType.PHASER_CONFIGURATION,
                Data = new Client::MessageDataUnion
                {
                    Type = Client.MessageData.PhaserConfigurationData,
                    Value = new Client::PhaserConfigurationDataT
                    {
                        FramesPerStrobe = phaser.FramesPerStrobe,
                        StrobeConfiguration = phaser.StrobeConfigurations.Select(sc =>
                            new Client::StrobeConfigurationT
                            {
                                Port = phaser.StrobeIdToPort(sc.Key),
                                StartOffsetNs = (long)(sc.Value.StrobeStartOffsetUs * 1000),
                                EndOffsetNs = (long)(sc.Value.StrobeEndOffsetUs * 1000),
                                Polarity = (Client::StrobePolarity)sc.Value.Polarity
                            }).ToList()
                    },
                }
            };

            return message.SerializeToBinary();
        }
    }

    public partial class ScanSystem
    {
        /// <summary>
        /// Create a <see cref="Phaser"/> with the given <paramref name="id"/>.
        /// </summary>
        /// <param name="serialNumber">The serial number of the phaser.</param>
        /// <param name="id">The ID of the phaser.</param>
        public Phaser CreatePhaser(uint serialNumber, uint id)
        {
            if (IsConnected)
            {
                throw new InvalidOperationException("Can not add scan head while connected.");
            }

            if (IsScanning)
            {
                throw new InvalidOperationException("Can not add scan head while scanning.");
            }

            if (idToScanHead.Values.Any(sh => sh.SerialNumber == serialNumber))
            {
                throw new ArgumentException($"Scan head with serial number \"{serialNumber}\" is already managed.");
            }

            if (idToScanHead.ContainsKey(id))
            {
                throw new ArgumentException("ID is already assigned to another scan head.");
            }

            if (!discoveries.ContainsKey(serialNumber))
            {
                // Scan head was not found on initial discovery, try again
                DiscoverDevices();
                if (!discoveries.ContainsKey(serialNumber))
                {
                    throw new ArgumentException($"Scan head {serialNumber} cannot be found on network.");
                }
            }

            var discovery = discoveries[serialNumber];
            var phaser = new Phaser(this, discovery, serialNumber, id);
            idToScanHead[id] = phaser;
            profileBuffers = idToScanHead.Values.Select(sh => sh.Profiles).ToArray();
            return phaser;
        }

        /// <summary>
        /// Adds a <paramref name="strobe"/> from the <see cref="Phaser"/> with <paramref name="id"/> to the current phase.
        /// </summary>
        /// <param name="id">The <see cref="Phaser"/> ID.</param>
        /// <param name="strobe">The <see cref="Strobe"/> to trigger.</param>
        /// <param name="strobeTimeUs">The strobe duration in microseconds.</param>
        public void AddPhaseElement(uint id, Strobe strobe, double strobeTimeUs)
        {
            if (strobe == Strobe.Invalid)
            {
                throw new InvalidOperationException("Invalid strobe.");
            }

            if (!idToScanHead.TryGetValue(id, out var scanHead))
            {
                throw new InvalidOperationException($"Phaser with id {id} doesn't exist.");
            }

            if (!(scanHead is Phaser phaser))
            {
                throw new InvalidOperationException($"ID {id} is not a phaser.");
            }

            if (phaseTable.Count == 0)
            {
                throw new InvalidOperationException("Cannot add phase element without adding a phase first.");
            }

            if (strobeTimeUs <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(strobeTimeUs), "Strobe duration must be greater than 0.");
            }

            // Add up the number of times a scan head has appeared in the phase table and check against max groups
            int groupCount = phaseTable.Sum(p => p.Elements.Count(e => e.ScanHead == phaser));
            uint maxGroups = phaser.Specification.MaxConfigurationGroups;
            if (groupCount >= maxGroups)
            {
                throw new InvalidOperationException($"Cannot add phaser {phaser.ID} more than {maxGroups} times into the phase table");
            }

            if (!phaser.TryGetStrobeConfiguration(strobe, out var conf))
            {
                conf = new StrobeConfiguration();
            }

            var phase = phaseTable.Last();
            phase.AddStrobe(phaser, strobe, conf, strobeTimeUs);

            FlagDirty(ScanSystemDirtyStateFlags.PhaseTable);
        }
    }

    internal partial class PhaseElement
    {
        internal StrobeConfiguration StrobeConfiguration;
        internal uint StrobeDurationNs;
        internal bool IsStrobe => StrobeConfiguration != null;
    }

    internal partial class Phase
    {
        internal void AddStrobe(Phaser phaser, Strobe strobe, StrobeConfiguration conf, double durationUs)
        {
            if (durationUs <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(durationUs), "Strobe duration must be greater than 0.");
            }

            if (durationUs * 1000 > uint.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(durationUs), "Strobe duration is too large.");
            }

            int strobeCounts = Elements.Count(e => e.ScanHead == phaser && e.IsStrobe);
            if (strobeCounts >= 2)
            {
                throw new InvalidOperationException("Can only add 2 strobes per phaser per phase.");
            }

            var pair = phaser.GetPair(strobe);
            uint cameraPort = phaser.CameraIdToPort(pair.Camera);
            uint laserPort = phaser.LaserIdToPort(pair.Laser);

            var pe = new PhaseElement
            {
                ScanHead = phaser,
                CameraPort = cameraPort,
                LaserPort = laserPort,
                StrobeConfiguration = conf,
                StrobeDurationNs = (uint)(durationUs * 1000),
            };

            Elements.Add(pe);
        }
    }
}
