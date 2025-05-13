// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using JoeScan.Pinchot.Beta;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Client = joescan.schema.client;
using UpdateClient = joescan.schema.update.client;
using UpdateServer = joescan.schema.update.server;

namespace JoeScan.Pinchot
{
    /// <summary>
    /// An interface to a physical scan head.
    /// </summary>
    /// <remarks>
    /// The scan head class provides an interface to a physical scan head by providing properties
    /// and methods for configuration, status retrieval, and profile data retrieval. A scan head
    /// must belong to a <see cref="ScanSystem"/> and is created using <see cref="ScanSystem.CreateScanHead"/>.
    /// </remarks>
    public partial class ScanHead : IDisposable
    {
        #region Private Fields

        private ScanSystem scanSystem;
        private ScanHeadSenderReceiver senderReceiver;
        private bool disposed;
        private ProductType type;
        private ScanHeadOrientation orientation;
        private ScanHeadDirtyStateFlags dirtyState;

        #endregion

        #region Public Properties

        /// <summary>
        /// Gets the product type of the scan head.
        /// </summary>
        /// <value>The product type of the scan head.</value>
        public ProductType Type
        {
            get => type;
            private set
            {
                type = value;
                Specification = GetSpecification(value);
                Name = Specification.ProductStr;
                Capabilities = new ScanHeadCapabilities(Specification);
            }
        }

        /// <summary>
        /// Gets the product name of the scan head.
        /// </summary>
        /// <value>The product name of the scan head.</value>
        public string Name { get; private set; }

        /// <summary>
        /// Gets the serial number of the scan head.
        /// </summary>
        /// <value>The serial number of the scan head.</value>
        public uint SerialNumber { get; }

        /// <summary>
        /// Gets the unique ID of the scan head.
        /// </summary>
        /// <value>The unique ID of the scan head.</value>
        public uint ID { get; }

        /// <summary>
        /// Gets the object that defines the various physical limits and features of a scan head.
        /// </summary>
        /// <value>The object that defines the various physical limits and features of a scan head.</value>
        public ScanHeadCapabilities Capabilities { get; private set; }

        /// <summary>
        /// Gets the <see cref="IPAddress"/> of the scan head.
        /// </summary>
        /// <value>The <see cref="IPAddress"/> of the scan head.</value>
        public IPAddress IpAddress { get; internal set; }

        /// <summary>
        /// Gets the <see cref="IPAddress"/> of the client machine's NIC that received the broadcast.
        /// </summary>
        /// <value>The client machine's <see cref="IPAddress"/> which discovered the scan head.</value>
        /// <remarks>
        /// The property is used when making a TCP connection between the client machine and a scan head.
        /// This is particularly important on computers that have multiple NICs or a NIC with dual ports.
        /// Whichever NIC discovered the scan head should be responsible for making the connection.
        /// </remarks>
        public IPAddress ClientIpAddress { get; internal set; }

        /// <summary>
        /// Gets a value indicating whether the network connection to the scan head is established.
        /// </summary>
        /// <value>A value indicating whether the network connection to the scan head is established.</value>
        public bool IsConnected => senderReceiver?.IsConnected ?? false;

        /// <summary>
        /// Gets a value indicating whether the scan head is actively scanning.
        /// </summary>
        /// <value>A value indicating whether the scan head is actively scanning.</value>
        public bool IsScanning => senderReceiver?.IsScanning ?? false;

        /// <summary>
        /// Gets or sets the value indicating the orientation of the scan head.
        /// </summary>
        /// <value>The <see cref="ScanHeadOrientation"/> of the scan head.</value>
        public ScanHeadOrientation Orientation
        {
            get => orientation;
            set
            {
                orientation = value;
                foreach (var alignment in Alignments)
                {
                    alignment.Value.Orientation = value;
                }

                FlagDirty(ScanHeadDirtyStateFlags.Window);
            }
        }

        /// <summary>
        /// Gets the <see cref="ScanHeadVersionInformation"/> of the firmware on the scan head.
        /// </summary>
        /// <value>The <see cref="ScanHeadVersionInformation"/> of the firmware on the scan head.</value>
        public ScanHeadVersionInformation Version { get; internal set; }

        /// <summary>
        /// Gets the number of <see cref="IProfile"/>s available in the local buffer for the scan head.
        /// </summary>
        /// <remarks>
        /// All existing <see cref="IProfile"/>s are cleared from the local buffer when <see cref="ScanSystem.StartScanning(uint, DataFormat, ScanningMode)"/>
        /// is called successfully.
        /// <br/>
        /// This value means nothing when scanning with <see cref="ScanningMode.Frame"/>.
        /// </remarks>
        /// <value>The number of <see cref="IProfile"/>s available in the local buffer for the scan head.</value>
        public int NumberOfProfilesAvailable => Profiles.Count;

        /// <summary>
        /// Gets a value indicating whether the scan head profile buffer overflowed.
        /// </summary>
        /// <remarks>Resets to <see langword="false"/> when <see cref="ScanSystem.StartScanning(uint, DataFormat, ScanningMode)"/> is called successfully.</remarks>
        /// <value>A value indicating whether the scan head profile buffer overflowed.</value>
        public bool ProfileBufferOverflowed => senderReceiver.ProfileBufferOverflowed || QueueManager.FrameQueueOverflowed;

        /// <summary>
        /// Gets an <see cref="IEnumerable{T}"/> that can be used to iterate over all valid cameras.
        /// </summary>
        /// <value>An <see cref="IEnumerable{T}"/> that can be used to iterate over all valid cameras.</value>
        public IEnumerable<Camera> Cameras
        {
            get
            {
                const Camera c = Camera.CameraA;
                for (int i = 0; i < Specification.NumberOfCameras; i++)
                {
                    yield return c + i;
                }
            }
        }

        /// <summary>
        /// Gets an <see cref="IEnumerable{T}"/> that can be used to iterate over all valid lasers.
        /// </summary>
        /// <value>An <see cref="IEnumerable{T}"/> that can be used to iterate over all valid lasers.</value>
        public IEnumerable<Laser> Lasers
        {
            get
            {
                const Laser l = Laser.Laser1;
                for (int i = 0; i < Specification.NumberOfLasers; i++)
                {
                    yield return l + i;
                }
            }
        }

        #endregion

        #region Internal Properties

        /// <summary>
        /// The most recent <see cref="ScanHeadStatus"/> received from a call to <see cref="RequestStatus"/>.
        /// </summary>
        /// <value>The most recent <see cref="ScanHeadStatus"/> received from the scan head.</value>
        internal ScanHeadStatus CachedStatus { get; private set; }

        /// <summary>
        /// Gets the <see cref="ScanHeadConfiguration"/> used to configure the scan head. Use
        /// <see cref="Configure"/> to set.
        /// </summary>
        /// <seealso cref="Configure"/>
        /// <value>The <see cref="ScanHeadConfiguration"/> used to configure the scan head.</value>
        internal ScanHeadConfiguration Configuration { get; private set; }
            = new ScanHeadConfiguration();

        /// <summary>
        /// Gets the thread safe collection of profiles received from the scan head.
        /// </summary>
        internal BlockingCollection<IProfile> Profiles { get; }
            = new BlockingCollection<IProfile>(new ConcurrentQueue<IProfile>(), Globals.ProfileQueueSize);

        /// <summary>
        /// Gets the <see cref="FrameQueueManager"/> to read/write profile frames.
        /// </summary>
        internal FrameQueueManager QueueManager { get; private set; } = new FrameQueueManager();

        /// <summary>
        /// Gets the spatial transformation parameters of the scan head required to
        /// transform the data from a camera based coordinate system to one based on
        /// mill placement. Read Only. Use SetAlignment to set all alignment values.
        /// </summary>
        internal Dictionary<CameraLaserPair, AlignmentParameters> Alignments { get; }
            = new Dictionary<CameraLaserPair, AlignmentParameters>();

        internal Dictionary<CameraLaserPair, ScanWindow> Windows { get; }
            = new Dictionary<CameraLaserPair, ScanWindow>();

        internal Dictionary<CameraLaserPair, ExclusionMask> ExclusionMasks { get; }
            = new Dictionary<CameraLaserPair, ExclusionMask>();

        internal Dictionary<CameraLaserPair, BrightnessCorrection> BrightnessCorrections { get; }
            = new Dictionary<CameraLaserPair, BrightnessCorrection>();

        internal ScanSystemUnits Units { get; }

        internal double CameraToMillScale => Units == ScanSystemUnits.Millimeters ? 25.4 : 1.0;

        internal long CompleteProfilesReceivedCount => senderReceiver.CompleteProfilesReceivedCount;

        internal long IncompleteProfilesReceivedCount => senderReceiver.IncompleteProfilesReceivedCount;

        internal long BadPacketsCount => senderReceiver.BadPacketsCount;

        internal Client::ScanHeadSpecificationT Specification { get; private set; }

        internal List<Client::CameraLaserConfigurationT> CameraLaserConfigurations { get; }
            = new List<Client.CameraLaserConfigurationT>();

        internal IEnumerable<CameraLaserPair> CameraLaserPairs
        {
            get
            {
                foreach (var group in Specification.ConfigurationGroups)
                {
                    var camera = CameraPortToId(group.CameraPort);
                    var laser = LaserPortToId(group.LaserPort);
                    yield return new CameraLaserPair(camera, laser);
                }
            }
        }

        internal bool IsLaserDriven =>
            Specification.ConfigurationGroupPrimary == Client::ConfigurationGroupPrimary.LASER;

        internal bool IsCameraDriven =>
            Specification.ConfigurationGroupPrimary == Client::ConfigurationGroupPrimary.CAMERA;


        #endregion

        #region Lifecycle

        internal ScanHead()
        {
        }

        internal ScanHead(ScanSystem scanSystem, DiscoveredDevice device, uint serialNumber, uint id)
            : this(device.Version.Type, serialNumber, id, scanSystem.Units)
        {
            this.scanSystem = scanSystem;
            IpAddress = device.IpAddress;
            ClientIpAddress = device.ClientIpAddress;
            Version = device.Version;
        }

        internal ScanHead(ProductType type, uint serialNumber, uint id, ScanSystemUnits units)
        {
            if (units != ScanSystemUnits.Millimeters && units != ScanSystemUnits.Inches)
            {
                throw new ArgumentException("Invalid units type.", nameof(units));
            }

            Units = units;
            Type = type;
            SerialNumber = serialNumber;
            ID = id;

            foreach (var pair in CameraLaserPairs)
            {
                Windows[pair] = ScanWindow.CreateScanWindowUnconstrained();
                Alignments[pair] = new AlignmentParameters(CameraToMillScale);
                ExclusionMasks[pair] = new ExclusionMask(this);
                BrightnessCorrections[pair] = new BrightnessCorrection(this);
            }

            FlagDirty(ScanHeadDirtyStateFlags.AllDirty);
        }

        /// <summary>
        /// Releases the managed and unmanaged resources used by the scan head
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases the unmanaged resources used by the scan head and optionally releases the managed resources.
        /// </summary>
        /// <param name="disposing">Whether being disposed explicitly (true) or due to a finalizer (false).</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }

            if (disposing)
            {
                senderReceiver?.Dispose();
                Profiles?.Dispose();
            }

            disposed = true;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Configures the scan head.
        /// </summary>
        /// <remarks>
        /// The <see cref="ScanHeadConfiguration"/> parameters are only sent to the scan head when
        /// <see cref="ScanSystem.StartScanning(uint, DataFormat, ScanningMode)"/> is called.<br/>
        /// A clone of <paramref name="configuration"/> is created internally. This means that changing the
        /// <see cref="ScanHeadConfiguration"/> object passed in after this function is called will not change
        /// the internal configuration.
        /// </remarks>
        /// <param name="configuration">The <see cref="ScanHeadConfiguration"/> to use for configuration
        /// of the scan head.
        /// </param>
        /// <exception cref="InvalidOperationException">
        /// <see cref="IsScanning"/> is <see langword="true"/>.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="configuration"/> is `null`.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// If any of the exposure or laser on time values are out of the minimum
        /// or maximum ranges for the scan head.
        /// </exception>
        public void Configure(ScanHeadConfiguration configuration)
        {
            if (IsScanning)
            {
                throw new InvalidOperationException("Can not set configuration while scanning.");
            }

            if (configuration is null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            if (configuration.MinLaserOnTimeUs < Specification.MinLaserOnTimeUs
                || configuration.MinLaserOnTimeUs > Specification.MaxLaserOnTimeUs)
            {
                throw new ArgumentOutOfRangeException(nameof(configuration.MinLaserOnTimeUs),
                    "Invalid minimum laser on time.");
            }

            if (configuration.DefaultLaserOnTimeUs < Specification.MinLaserOnTimeUs
                || configuration.DefaultLaserOnTimeUs > Specification.MaxLaserOnTimeUs)
            {
                throw new ArgumentOutOfRangeException(nameof(configuration.DefaultLaserOnTimeUs),
                    "Invalid default laser on time.");
            }

            if (configuration.MaxLaserOnTimeUs < Specification.MinLaserOnTimeUs
                || configuration.MaxLaserOnTimeUs > Specification.MaxLaserOnTimeUs)
            {
                throw new ArgumentOutOfRangeException(nameof(configuration.MaxLaserOnTimeUs),
                    "Invalid maximum laser on time.");
            }

            if (configuration.MinCameraExposureTimeUs < Specification.MinCameraExposureUs
                || configuration.MinCameraExposureTimeUs > Specification.MaxCameraExposureUs)
            {
                throw new ArgumentOutOfRangeException(nameof(configuration.MinCameraExposureTimeUs),
                    "Invalid minimum exposure time.");
            }

            if (configuration.DefaultCameraExposureTimeUs < Specification.MinCameraExposureUs
                || configuration.DefaultCameraExposureTimeUs > Specification.MaxCameraExposureUs)
            {
                throw new ArgumentOutOfRangeException(nameof(configuration.DefaultCameraExposureTimeUs),
                    "Invalid default exposure time.");
            }

            if (configuration.MaxCameraExposureTimeUs < Specification.MinCameraExposureUs
                || configuration.MaxCameraExposureTimeUs > Specification.MaxCameraExposureUs)
            {
                throw new ArgumentOutOfRangeException(nameof(configuration.MaxCameraExposureTimeUs),
                    "Invalid maximum exposure time.");
            }

            // we make a copy so that we don't share a single configuration between all heads
            Configuration = configuration.Clone() as ScanHeadConfiguration;

            FlagDirty(ScanHeadDirtyStateFlags.Configuration);
        }

        /// <summary>
        /// Gets a clone of the <see cref="ScanHeadConfiguration"/> used to configure the scan head. Use
        /// <see cref="Configure"/> to set the <see cref="ScanHeadConfiguration"/>.
        /// </summary>
        /// <value>A clone of the <see cref="ScanHeadConfiguration"/> used to configure the scan head.</value>
        public ScanHeadConfiguration GetConfigurationClone()
        {
            return Configuration.Clone() as ScanHeadConfiguration;
        }

        /// <summary>
        /// Blocks until the number of requested <see cref="IProfile"/>s are avilable to be read out.
        /// </summary>
        /// <param name="count">Number of <see cref="IProfile"/>s to wait for.</param>
        /// <param name="timeout">Maximum amount of time to wait.</param>
        /// <param name="token">Token to observe.</param>
        /// <returns>
        /// <see langword="true"/> if the requested number of profiles are available
        /// or <see langword="false"/> if the timeout elapses or the operation is canceled.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="count"/> is greater than <see cref="Globals.ProfileQueueSize"/>.
        /// </exception>
        /// <seealso cref="NumberOfProfilesAvailable"/>
        /// <seealso cref="WaitUntilProfilesAvailableAsync(int, TimeSpan, CancellationToken)"/>
        public bool WaitUntilProfilesAvailable(int count, TimeSpan timeout, CancellationToken token = default)
        {
            return WaitUntilProfilesAvailableAsync(count, timeout, token).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Blocks until the number of requested <see cref="IProfile"/>s are avilable to be read out.
        /// </summary>
        /// <remarks>
        /// This function periodically checks the number of available profiles as fast as the system
        /// clock allows. This is system dependent but is typically ~15 milliseconds.
        /// </remarks>
        /// <param name="count">Number of <see cref="IProfile"/>s to wait for.</param>
        /// <param name="timeout">Maximum amount of time to wait.</param>
        /// <param name="token">Token to observe.</param>
        /// <returns>
        /// <see langword="true"/> if the requested number of profiles are available
        /// or <see langword="false"/> if the timeout elapses or the operation is canceled.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="count"/> is greater than <see cref="Globals.ProfileQueueSize"/>.
        /// </exception>
        /// <seealso cref="NumberOfProfilesAvailable"/>
        /// <seealso cref="WaitUntilProfilesAvailable(int, TimeSpan, CancellationToken)"/>
        public async Task<bool> WaitUntilProfilesAvailableAsync(int count, TimeSpan timeout, CancellationToken token = default)
        {
            if (count > Globals.ProfileQueueSize)
            {
                throw new ArgumentOutOfRangeException(nameof(count), $"Can't wait for more than {Globals.ProfileQueueSize} profiles.");
            }

            try
            {
                var start = DateTime.Now;
                while (Profiles.Count < count)
                {
                    // `Task.Delay` uses the system clock which has a resolution
                    // of ~15 ms but try to go as fast as possible
                    await Task.Delay(1, token);

                    if (DateTime.Now - start > timeout)
                    {
                        // timeout
                        return false;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Takes an <see cref="IProfile"/> from the queue, blocking if the queue is empty.
        /// </summary>
        /// <param name="token">The <see cref="CancellationToken"/> to observe.</param>
        /// <returns>The dequeued <see cref="IProfile"/>.</returns>
        public IProfile TakeNextProfile(CancellationToken token = default)
        {
            return Profiles.Take(token);
        }

        /// <summary>
        /// Tries to take an <see cref="IProfile"/> from the queue.
        /// </summary>
        /// <param name="profile">The dequeued <see cref="IProfile"/>.</param>
        /// <param name="timeout">The time to wait for a profile when the queue is empty.</param>
        /// <param name="token"><see cref="CancellationToken"/> to observe.</param>
        /// <returns>Whether a <see cref="IProfile"/> was successfully taken.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if <see cref="IsConnected"/> is <see langword="false"/> when timeout occurs while
        /// waiting for a profile. This indicates a loss of communication with the scan head, either
        /// by a possible network or power issue.
        /// </exception>
        public bool TryTakeNextProfile(out IProfile profile, TimeSpan timeout = default, CancellationToken token = default)
        {
            bool success = Profiles.TryTake(out profile, (int)timeout.TotalMilliseconds, token);
            if (!success && !IsConnected)
            {
                throw new InvalidOperationException($"Scan head {SerialNumber} is not connected, possible network or power issue.");
            }

            return success;
        }

        /// <summary>
        /// Takes a number of <see cref="IProfile"/>s from the queue. The <see cref="IEnumerable{T}"/>
        /// returned contains the lesser of <paramref name="maxCount"/> and
        /// <see cref="NumberOfProfilesAvailable"/> profiles.
        /// </summary>
        /// <param name="maxCount">The maximum number of profiles to read.</param>
        /// <param name="timeout">The time to wait for a profile when the queue is empty.
        /// Use a <see cref="TimeSpan"/> that represents -1 milliseconds to wait indefinitely.</param>
        /// <param name="token"><see cref="CancellationToken"/> to observe.</param>
        /// <returns>An <see cref="IEnumerable{T}"/> of profiles.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if <see cref="IsConnected"/> is <see langword="false"/> when timeout occurs while
        /// waiting for a profile. This indicates a loss of communication with the scan head, either
        /// by a possible network or power issue.
        /// </exception>
        public IEnumerable<IProfile> TryTakeProfiles(int maxCount, TimeSpan timeout = default, CancellationToken token = default)
        {
            for (int i = 0; i < maxCount; ++i)
            {
                if (TryTakeNextProfile(out var profile, timeout, token))
                {
                    yield return profile;
                }
                else
                {
                    yield break;
                }
            }
        }

        /// <summary>
        /// Takes a number of <see cref="IProfile"/>s from the queue and places
        /// them directly in <paramref name="profiles"/>. The number dequeued
        /// will be the lesser of <see cref="Span{T}.Length"/> of <paramref name="profiles"/>
        /// and <see cref="NumberOfProfilesAvailable"/>.
        /// </summary>
        /// <param name="profiles">The preallocated destination buffer.</param>
        /// <param name="timeout">The time to wait for a profile when the queue is empty.
        /// Use a <see cref="TimeSpan"/> that represents -1 milliseconds to wait indefinitely.</param>
        /// <param name="token"><see cref="CancellationToken"/> to observe.</param>
        /// <returns>The number of profiles dequeued and placed in <paramref name="profiles"/>.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if <see cref="IsConnected"/> is <see langword="false"/> when timeout occurs while
        /// waiting for a profile. This indicates a loss of communication with the scan head, either
        /// by a possible network or power issue.
        /// </exception>
        public int TryTakeProfiles(Span<IProfile> profiles, TimeSpan timeout = default, CancellationToken token = default)
        {
            for (int i = 0; i < profiles.Length; ++i)
            {
                if (TryTakeNextProfile(out var profile, timeout, token))
                {
                    profiles[i] = profile;
                }
                else
                {
                    return i;
                }
            }

            return profiles.Length;
        }

        /// <summary>
        /// Sets the spatial transform parameters of the scan head in order to properly
        /// transform the data from a scan head based coordinate system to one based on
        /// mill placement. Parameters are applied to all <see cref="Camera"/>s.
        /// </summary>
        /// <param name="rollDegrees">The rotation around the Z axis in the mill coordinate system in degrees.</param>
        /// <param name="shiftX">The shift along the X axis in the mill coordinate system in <see cref="ScanSystemUnits"/>.</param>
        /// <param name="shiftY">The shift along the Y axis in the mill coordinate system in <see cref="ScanSystemUnits"/>.</param>
        /// <exception cref="InvalidOperationException">
        /// <see cref="IsScanning"/> is <see langword="true"/>.<br/>
        /// -or-<br/>
        /// A loss of communication with the scan head occurred, usually caused by a network or power issue.
        /// </exception>
        public void SetAlignment(double rollDegrees, double shiftX, double shiftY)
        {
            if (IsScanning)
            {
                throw new InvalidOperationException("Can not set alignment while scanning.");
            }

#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
            foreach (var pair in Alignments.Keys)
#else
            foreach (var pair in Alignments.Keys.ToList())
#endif
            {
                Alignments[pair] = new AlignmentParameters(CameraToMillScale, rollDegrees, shiftX, shiftY, Orientation);
            }

            FlagDirty(ScanHeadDirtyStateFlags.Window);
        }

        /// <summary>
        /// Sets the spatial transform parameters of the scan head in order to properly
        /// transform the data from a scan head based coordinate system to one based on
        /// mill placement. Parameters are applied only to specified <see cref="Camera"/>.
        /// In most cases <see cref="SetAlignment(double, double, double)"/> should be used instead.
        /// </summary>
        /// <param name="camera">The <see cref="Camera"/> which to set the alignment of.</param>
        /// <param name="rollDegrees">The rotation around the Z axis in the mill coordinate system in degrees.</param>
        /// <param name="shiftX">The shift along the X axis in the mill coordinate system in <see cref="ScanSystemUnits"/>.</param>
        /// <param name="shiftY">The shift along the Y axis in the mill coordinate system in <see cref="ScanSystemUnits"/>.</param>
        /// <exception cref="InvalidOperationException">
        /// <see cref="IsScanning"/> is <see langword="true"/>.<br/>
        /// -or-<br/>
        /// A loss of communication with the scan head occurred, usually caused by a network or power issue.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Trying to use a camera-driven function with a laser-driven scan head.
        /// Use <see cref="SetAlignment(Laser, double, double, double)"/> instead.
        /// </exception>
        public void SetAlignment(Camera camera, double rollDegrees, double shiftX, double shiftY)
        {
            if (IsScanning)
            {
                throw new InvalidOperationException("Can not set alignment while scanning.");
            }

            if (!IsValidCamera(camera))
            {
                throw new ArgumentOutOfRangeException(nameof(camera), "Invalid camera.");
            }

            var pair = GetPair(camera);
            Alignments[pair] = new AlignmentParameters(CameraToMillScale, rollDegrees, shiftX, shiftY, Orientation);

            FlagDirty(ScanHeadDirtyStateFlags.Window);
        }

        /// <summary>
        /// Sets the spatial transform parameters of the scan head in order to properly
        /// transform the data from a scan head based coordinate system to one based on
        /// mill placement. Parameters are applied only to the specified <see cref="Laser"/>. This method
        /// should be used only on models with multiple lasers. On single laser models, the factory calibration already provides
        /// a good fit between data from both cameras. In this case, <see cref="SetAlignment(double, double, double)"/>
        /// should be used.
        /// </summary>
        /// <param name="laser">The <see cref="Laser"/> which to set the alignment of.</param>
        /// <param name="rollDegrees">The rotation around the Z axis in the mill coordinate system in degrees.</param>
        /// <param name="shiftX">The shift along the X axis in the mill coordinate system in <see cref="ScanSystemUnits"/>.</param>
        /// <param name="shiftY">The shift along the Y axis in the mill coordinate system in <see cref="ScanSystemUnits"/>.</param>
        /// <exception cref="InvalidOperationException">
        /// <see cref="IsScanning"/> is <see langword="true"/>.<br/>
        /// -or-<br/>
        /// A loss of communication with the scan head occurred, usually caused by a network or power issue.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Trying to use a laser-driven function with a camera-driven scan head.
        /// Use <see cref="SetAlignment(Camera, double, double, double)"/> instead.
        /// </exception>
        public void SetAlignment(Laser laser, double rollDegrees, double shiftX, double shiftY)
        {
            if (IsScanning)
            {
                throw new InvalidOperationException("Can not set alignment while scanning.");
            }

            if (!IsValidLaser(laser))
            {
                throw new ArgumentOutOfRangeException(nameof(laser), "Invalid laser.");
            }

            var pair = GetPair(laser);
            Alignments[pair] = new AlignmentParameters(CameraToMillScale, rollDegrees, shiftX, shiftY, Orientation);

            FlagDirty(ScanHeadDirtyStateFlags.Window);
        }

        /// <summary>
        /// Sets the <see cref="ScanWindow"/> for the scan head. The window restricts
        /// where the scan head looks for valid points in mill space.
        /// </summary>
        /// <param name="window">The <see cref="ScanWindow"/> to use for the scan head.</param>
        /// <exception cref="InvalidOperationException">
        /// <see cref="IsScanning"/> is <see langword="true"/>.<br/>
        /// -or-<br/>
        /// A loss of communication with the scan head occurred, usually caused by a network or power issue.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="window"/> is null.
        /// </exception>
        public void SetWindow(ScanWindow window)
        {
            // TODO: If netstandard2.0 is dropped, the `ToList` can be removed
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
            foreach (var pair in Windows.Keys)
#else
            foreach (var pair in Windows.Keys.ToList())
#endif
            {
                SetWindow(pair, window);
            }
        }

        /// <summary>
        /// Sets the <see cref="ScanWindow"/> for <paramref name="camera"/>. The window
        /// restricts where the scan head looks for valid points in mill space.
        /// </summary>
        /// <param name="camera">The <see cref="Camera"/> to apply the window to.</param>
        /// <param name="window">The <see cref="ScanWindow"/> to use for the scan head.</param>
        /// <exception cref="InvalidOperationException">
        /// <see cref="IsScanning"/> is <see langword="true"/>.<br/>
        /// -or-<br/>
        /// A loss of communication with the scan head occurred, usually caused by a network or power issue.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="window"/> is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Trying to use a camera-driven function with a laser-driven scan head.
        /// Use <see cref="SetWindow(Laser, ScanWindow)"/> instead.
        /// </exception>
        public void SetWindow(Camera camera, ScanWindow window)
        {
            var pair = GetPair(camera);
            SetWindow(pair, window);
        }

        /// <summary>
        /// Sets the <see cref="ScanWindow"/> for <paramref name="laser"/>. The window
        /// restricts where the scan head looks for valid points in mill space.
        /// </summary>
        /// <param name="laser">The <see cref="Laser"/> to apply the window to.</param>
        /// <param name="window">The <see cref="ScanWindow"/> to use for the scan head.</param>
        /// <exception cref="InvalidOperationException">
        /// <see cref="IsScanning"/> is <see langword="true"/>.<br/>
        /// -or-<br/>
        /// A loss of communication with the scan head occurred, usually caused by a network or power issue.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="window"/> is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Trying to use a laser-driven function with a camera-driven scan head.
        /// Use <see cref="SetWindow(Camera, ScanWindow)"/> instead.
        /// </exception>
        public void SetWindow(Laser laser, ScanWindow window)
        {
            var pair = GetPair(laser);
            SetWindow(pair, window);
        }

        /// <summary>
        /// Creates a new <see cref="ExclusionMask"/> with the dimensions
        /// of the camera(s) of the scan head.
        /// </summary>
        /// <returns>A clear <see cref="ExclusionMask"/>.</returns>
        public ExclusionMask CreateExclusionMask()
        {
            ThrowIfNotVersionCompatible(16, 1, 0);
            return new ExclusionMask(this);
        }

        /// <summary>
        /// Sets the <see cref="ExclusionMask"/> for the <paramref name="camera"/> supplied.
        /// </summary>
        /// <param name="camera">The <see cref="Camera"/> that the mask will be applied to.</param>
        /// <param name="mask">The <see cref="ExclusionMask"/> to be applied.</param>
        /// <exception cref="InvalidOperationException">
        /// <see cref="IsScanning"/> is <see langword="true"/>.<br/>
        /// -or-<br/>
        /// A loss of communication with the scan head occurred, usually caused by a network or power issue.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="mask"/> is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Trying to use a camera-driven function with a laser-driven scan head.
        /// Use <see cref="SetExclusionMask(Laser, ExclusionMask)"/> instead.
        /// </exception>
        public void SetExclusionMask(Camera camera, ExclusionMask mask)
        {
            ThrowIfNotVersionCompatible(16, 1, 0);

            if (IsScanning)
            {
                throw new InvalidOperationException("Can not set exclusion mask while scanning.");
            }

            if (mask == null)
            {
                throw new ArgumentNullException(nameof(mask));
            }

            if (!IsValidCamera(camera))
            {
                throw new ArgumentOutOfRangeException(nameof(camera), "Invalid camera.");
            }

            var pair = GetPair(camera);
            ExclusionMasks[pair] = mask.Clone() as ExclusionMask;

            FlagDirty(ScanHeadDirtyStateFlags.ExclusionMask);
        }

        /// <summary>
        /// Sets the <see cref="ExclusionMask"/> for the <paramref name="laser"/> supplied.
        /// </summary>
        /// <param name="laser">The <see cref="Laser"/> that the mask will be applied to.</param>
        /// <param name="mask">The <see cref="ExclusionMask"/> to be applied.</param>
        /// <exception cref="InvalidOperationException">
        /// <see cref="IsScanning"/> is <see langword="true"/>.<br/>
        /// -or-<br/>
        /// A loss of communication with the scan head occurred, usually caused by a network or power issue.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="mask"/> is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Trying to use a camera-driven function with a laser-driven scan head.
        /// Use <see cref="SetExclusionMask(Laser, ExclusionMask)"/> instead.
        /// </exception>
        public void SetExclusionMask(Laser laser, ExclusionMask mask)
        {
            ThrowIfNotVersionCompatible(16, 1, 0);

            if (IsScanning)
            {
                throw new InvalidOperationException("Can not set exclusion mask while scanning.");
            }

            if (mask == null)
            {
                throw new ArgumentNullException(nameof(mask));
            }

            if (!IsValidLaser(laser))
            {
                throw new ArgumentOutOfRangeException(nameof(laser), "Invalid laser.");
            }

            var pair = GetPair(laser);
            ExclusionMasks[pair] = mask.Clone() as ExclusionMask;

            FlagDirty(ScanHeadDirtyStateFlags.ExclusionMask);
        }

        /// <summary>
        /// Obtains a single profile from a scan head to be used for
        /// diagnostic purposes. Each subsequent call to this function will
        /// trigger the auto-exposure mechanism to automatically adjust the
        /// camera and laser according to the <see cref="ScanHeadConfiguration"/>
        /// provided to <see cref="Configure(ScanHeadConfiguration)"/>.
        /// </summary>
        /// <param name="camera">The <see cref="Camera"/> used for the profile capture.</param>
        /// <returns>
        /// An <see cref="IProfile"/> from <paramref name="camera"/> and its associated <see cref="Laser"/>.
        /// </returns>
        /// <remarks>
        /// The auto-exposure mechanism is currently non-functional. The camera exposure will be set
        /// to <see cref="ScanheadConfiguration.DefaultLaserOnTimeUs"/>
        /// and laser on time will be set to <see cref="ScanHeadConfiguration.DefaultLaserOnTimeUs"/>.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// <see cref="IsConnected"/> is <see langword="false"/>.<br/>
        /// -or-<br/>
        /// <see cref="IsScanning"/> is <see langword="true"/>.<br/>
        /// -or-<br/>
        /// A loss of communication with the scan head occurred, usually caused by a network or power issue.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Trying to use a camera-driven function with a laser-driven scan head.
        /// Use <see cref="GetDiagnosticProfile(Laser)"/> instead.
        /// </exception>
        public IProfile GetDiagnosticProfile(Camera camera)
        {
            var pair = GetPair(camera);
            return GetDiagnosticProfile(pair,
                Configuration.DefaultLaserOnTimeUs,
                Configuration.DefaultLaserOnTimeUs);
        }

        /// <summary>
        /// Obtains a single profile from a scan head to be used for
        /// diagnostic purposes.
        /// </summary>
        /// <param name="camera">The <see cref="Camera"/> used for the profile capture.</param>
        /// <param name="cameraExposureUs">The exposure time for the <paramref name="camera"/> in microseconds.</param>
        /// <param name="laserOnTimeUs">The laser on time of the associated <see cref="Laser"/> in microseconds.</param>
        /// <returns>
        /// An <see cref="IProfile"/> from <paramref name="camera"/> and its associated <see cref="Laser"/>.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// <see cref="IsConnected"/> is <see langword="false"/>.<br/>
        /// -or-<br/>
        /// <see cref="IsScanning"/> is <see langword="true"/>.<br/>
        /// -or-<br/>
        /// A loss of communication with the scan head occurred, usually caused by a network or power issue.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Trying to use a camera-driven function with a laser-driven scan head.
        /// Use <see cref="GetDiagnosticProfile(Laser, uint, uint)"/> instead.
        /// </exception>
        public IProfile GetDiagnosticProfile(Camera camera, uint cameraExposureUs, uint laserOnTimeUs)
        {
            var pair = GetPair(camera);
            return GetDiagnosticProfile(pair, cameraExposureUs, laserOnTimeUs);
        }

        /// <summary>
        /// Obtains a single profile from a scan head to be used for
        /// diagnostic purposes. Each subsequent call to this function will
        /// trigger the auto-exposure mechanism to automatically adjust the
        /// camera and laser according to the <see cref="ScanHeadConfiguration"/>
        /// provided to <see cref="Configure(ScanHeadConfiguration)"/>.
        /// </summary>
        /// <param name="laser">The <see cref="Laser"/> used for the profile capture.</param>
        /// <returns>
        /// An <see cref="IProfile"/> from <paramref name="laser"/> and its associated <see cref="Camera"/>.
        /// </returns>
        /// <remarks>
        /// The auto-exposure mechanism is currently non-functional. The camera exposure
        /// and laser on time will be set to <see cref="ScanHeadConfiguration.DefaultLaserOnTimeUs"/>.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// <see cref="IsConnected"/> is <see langword="false"/>.<br/>
        /// -or-<br/>
        /// <see cref="IsScanning"/> is <see langword="true"/>.<br/>
        /// -or-<br/>
        /// A loss of communication with the scan head occurred, usually caused by a network or power issue.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Trying to use a laser-driven function with a camera-driven scan head.
        /// Use <see cref="GetDiagnosticProfile(Camera)"/> instead.
        /// </exception>
        public IProfile GetDiagnosticProfile(Laser laser)
        {
            var pair = GetPair(laser);
            return GetDiagnosticProfile(pair,
                Configuration.DefaultLaserOnTimeUs,
                Configuration.DefaultLaserOnTimeUs);
        }

        /// <summary>
        /// Obtains a single profile from a scan head to be used for diagnostic purposes.
        /// </summary>
        /// <param name="laser">The <see cref="Laser"/> used for the profile capture.</param>
        /// <param name="cameraExposureUs">The exposure time for the associated <see cref="Camera"/> in microseconds.</param>
        /// <param name="laserOnTimeUs">The laser on time of the <paramref name="laser"/> in microseconds.</param>
        /// <returns>
        /// An <see cref="IProfile"/> from <paramref name="laser"/> and its associated <see cref="Camera"/>.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// <see cref="IsConnected"/> is <see langword="false"/>.<br/>
        /// -or-<br/>
        /// <see cref="IsScanning"/> is <see langword="true"/>.<br/>
        /// -or-<br/>
        /// A loss of communication with the scan head occurred, usually caused by a network or power issue.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Trying to use a laser-driven function with a camera-driven scan head.
        /// Use <see cref="GetDiagnosticProfile(Camera, uint, uint)"/> instead.
        /// </exception>
        public IProfile GetDiagnosticProfile(Laser laser, uint cameraExposureUs, uint laserOnTimeUs)
        {
            var pair = GetPair(laser);
            return GetDiagnosticProfile(pair, cameraExposureUs, laserOnTimeUs);
        }

        /// <summary>
        /// Captures an image from the specified <see cref="Camera"/> without turning on the laser.
        /// Exposes the camera for <see cref="ScanHeadConfiguration.DefaultCameraExposureTimeUs"/>.
        /// </summary>
        /// <param name="camera">The <see cref="Camera"/> from which to capture the image.</param>
        /// <param name="imageDataType">The <see cref="DiagnosticImageType" /> determines whether the returned image should contain raw pixel data or pixels merged with the mask.</param>
        /// <returns>The <see cref="CameraImage"/>.</returns>
        /// <exception cref="InvalidOperationException">
        /// <see cref="IsConnected"/> is <see langword="false"/>.<br/>
        /// -or-<br/>
        /// <see cref="IsScanning"/> is <see langword="true"/>.<br/>
        /// -or-<br/>
        /// A loss of communication with the scan head occurred, usually caused by a network or power issue.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="camera"/> isn't valid.
        /// </exception>
        [Obsolete("Use GetDiagnosticCameraImage(Camera, uint, DiagnosticImageType) instead.")]
        public CameraImage GetDiagnosticCameraImage(Camera camera, DiagnosticImageType imageDataType = DiagnosticImageType.Masked)
        {
            return GetDiagnosticCameraImage(camera, Configuration.DefaultCameraExposureTimeUs, imageDataType);
        }

        /// <summary>
        /// Captures an image from the specified <see cref="Camera"/> without turning on the laser.
        /// </summary>
        /// <param name="camera">The <see cref="Camera"/> from which to capture the image.</param>
        /// <param name="cameraExposureUs">How long the camera should expose for in microseconds.</param>
        /// <param name="imageDataType">The <see cref="DiagnosticImageType" /> determines whether the returned image should contain raw pixel data or pixels merged with the mask.</param>
        /// <returns>The <see cref="CameraImage"/>.</returns>
        /// <exception cref="InvalidOperationException">
        /// <see cref="IsConnected"/> is <see langword="false"/>.<br/>
        /// -or-<br/>
        /// <see cref="IsScanning"/> is <see langword="true"/>.<br/>
        /// -or-<br/>
        /// A loss of communication with the scan head occurred, usually caused by a network or power issue.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="camera"/> isn't valid.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="cameraExposureUs"/> is too long or too short.
        /// </exception>
        public CameraImage GetDiagnosticCameraImage(Camera camera, uint cameraExposureUs, DiagnosticImageType imageDataType = DiagnosticImageType.Masked)
        {
            if (!IsValidCamera(camera))
            {
                throw new ArgumentException("Camera is invalid.", nameof(camera));
            }

            // Since laser is off, just find any valid laser to use as a dummy value
            var laser = CameraLaserPairs.First(clp => clp.Camera == camera).Laser;
            return GetDiagnosticCameraImage(camera, cameraExposureUs, laser, 0, imageDataType);
        }

        /// <summary>
        /// Captures an image from the specified <see cref="Camera"/> while a <see cref="Laser"/> is on.
        /// Exposes the camera for <see cref="ScanHeadConfiguration.DefaultLaserOnTimeUs"/> and
        /// turns the laser on for <see cref="ScanHeadConfiguration.DefaultLaserOnTimeUs"/>.
        /// </summary>
        /// <param name="camera">The <see cref="Camera"/> from which to capture the image.</param>
        /// <param name="laser">The <see cref="Laser"/> that should be on during the image capture.</param>
        /// <param name="imageDataType">The <see cref="DiagnosticImageType" /> determines whether the returned image should contain raw pixel data or pixels merged with the mask.</param>
        /// <returns>The <see cref="CameraImage"/>.</returns>
        /// <exception cref="InvalidOperationException">
        /// <see cref="IsConnected"/> is <see langword="false"/>.<br/>
        /// -or-<br/>
        /// <see cref="IsScanning"/> is <see langword="true"/>.<br/>
        /// -or-<br/>
        /// A loss of communication with the scan head occurred, usually caused by a network or power issue.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="camera"/> isn't valid.<br/>
        /// -or-<br/>
        /// <paramref name="laser"/> isn't valid.
        /// </exception>
        [Obsolete("Use GetDiagnosticCameraImage(Camera, uint, Laser, uint, DiagnosticImageType) instead.")]
        public CameraImage GetDiagnosticCameraImage(Camera camera, Laser laser, DiagnosticImageType imageDataType = DiagnosticImageType.Masked)
        {
            return GetDiagnosticCameraImage(camera, Configuration.DefaultLaserOnTimeUs, laser, Configuration.DefaultLaserOnTimeUs, imageDataType);
        }

        /// <summary>
        /// Captures an image from the specified <see cref="Camera"/> while a <see cref="Laser"/> is on.
        /// Exposes the camera for the same amount of time as the laserOnTimeUs parameter.
        /// </summary>
        /// <param name="camera">The <see cref="Camera"/> from which to capture the image.</param>
        /// <param name="laser">The <see cref="Laser"/> that should be on during the image capture.</param>
        /// <param name="laserOnTimeUs">How long the laser should be on for during the image capture in microseconds.</param>
        /// <param name="imageDataType">The <see cref="DiagnosticImageType" /> determines whether the returned image should contain raw pixel data or pixels merged with the mask.</param>
        /// <returns>The <see cref="CameraImage"/>.</returns>
        /// <exception cref="InvalidOperationException">
        /// <see cref="IsConnected"/> is <see langword="false"/>.<br/>
        /// -or-<br/>
        /// <see cref="IsScanning"/> is <see langword="true"/>.<br/>
        /// -or-<br/>
        /// A loss of communication with the scan head occurred, usually caused by a network or power issue.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="camera"/> isn't valid.<br/>
        /// -or-<br/>
        /// <paramref name="laser"/> isn't valid.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="laserOnTimeUs"/> is too long or too short.
        /// </exception>
        [Obsolete("Use GetDiagnosticCameraImage(Camera, uint, Laser, uint, DiagnosticImageType) instead.")]
        public CameraImage GetDiagnosticCameraImage(Camera camera, Laser laser, uint laserOnTimeUs, DiagnosticImageType imageDataType = DiagnosticImageType.Masked)
        {
            return GetDiagnosticCameraImage(camera, laserOnTimeUs, laser, laserOnTimeUs, imageDataType);
        }

        /// <summary>
        /// Captures an image from the specified <see cref="Camera"/> while a <see cref="Laser"/> is on.
        /// </summary>
        /// <param name="camera">The <see cref="Camera"/> from which to capture the image.</param>
        /// <param name="cameraExposureUs">How long the camera should expose for in microseconds.</param>
        /// <param name="laser">The <see cref="Laser"/> that should be on during the image capture.</param>
        /// <param name="laserOnTimeUs">How long the laser should be on for during the image capture in microseconds.</param>
        /// <param name="imageDataType">The <see cref="DiagnosticImageType" /> determines whether the returned image should contain raw pixel data or pixels merged with the mask.</param>
        /// <returns>The <see cref="CameraImage"/>.</returns>
        /// <exception cref="InvalidOperationException">
        /// <see cref="IsConnected"/> is <see langword="false"/>.<br/>
        /// -or-<br/>
        /// <see cref="IsScanning"/> is <see langword="true"/>.<br/>
        /// -or-<br/>
        /// A loss of communication with the scan head occurred, usually caused by a network or power issue.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="camera"/> isn't valid.<br/>
        /// -or-<br/>
        /// <paramref name="laser"/> isn't valid.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="cameraExposureUs"/> is too long or too short.<br/>
        /// -or-<br/>
        /// <paramref name="laserOnTimeUs"/> is too long or too short.
        /// </exception>
        public CameraImage GetDiagnosticCameraImage(Camera camera, uint cameraExposureUs, Laser laser, uint laserOnTimeUs,
            DiagnosticImageType imageDataType = DiagnosticImageType.Masked)
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("Not connected.");
            }

            if (IsScanning)
            {
                throw new InvalidOperationException("Scan system is scanning.");
            }

            if (!IsValidCamera(camera))
            {
                throw new ArgumentException("Invalid camera.", nameof(camera));
            }

            if (!IsValidLaser(laser))
            {
                throw new ArgumentException("Invalid laser.", nameof(laser));
            }

            if (cameraExposureUs > Specification.MaxCameraExposureUs
                || cameraExposureUs < Specification.MinCameraExposureUs)
            {
                throw new ArgumentOutOfRangeException(nameof(cameraExposureUs),
                    $"Exposure time must be between {Specification.MinCameraExposureUs}µs " +
                    $"and {Specification.MaxCameraExposureUs}µs.");
            }

            // laser can be turned off for image mode
            if (laserOnTimeUs != 0
                && (laserOnTimeUs > Specification.MaxLaserOnTimeUs
                || laserOnTimeUs < Specification.MinLaserOnTimeUs))
            {
                throw new ArgumentOutOfRangeException(nameof(laserOnTimeUs),
                    $"Laser on time must be between {Specification.MinLaserOnTimeUs}µs " +
                    $"and {Specification.MaxLaserOnTimeUs}µs.");
            }

            // ensure the scan head has up to date configuration
            scanSystem.PreSendConfiguration();

            var req = new Client::ImageRequestDataT()
            {
                CameraPort = CameraIdToPort(camera),
                LaserPort = LaserIdToPort(laser),
                CameraExposureNs = cameraExposureUs * 1000,
                LaserOnTimeNs = laserOnTimeUs * 1000,
                LaserDetectionThreshold = Configuration.LaserDetectionThreshold,
                SaturationThreshold = Configuration.SaturationThreshold,
                ImageDataType = (Client::ImageDataType)imageDataType
            };

            var img = senderReceiver.RequestDiagnosticImage(req);
            return new CameraImage(img, Specification)
            {
                ScanHeadID = ID
            };
        }

        /// <summary>
        /// Empties the internal client side software buffers used to store profiles received from a given scan head.
        /// <para>
        /// Under normal scanning conditions where the application consumes profiles as they become available,
        /// this function will not be needed. Its use is to be found in cases where the application fails to
        /// consume profiles after some time and the number of buffered profiles, as indicated by the
        /// <see cref="NumberOfProfilesAvailable"/> property, becomes more than the application can consume
        /// and only the most recent scan data is desired.
        /// </para>
        /// <para>
        /// When operating in frame scanning mode, use <see cref="ScanSystem.ClearFrames"/> instead.
        /// </para>
        /// </summary>
        /// <remarks>
        /// Profiles are automatically cleared when <see cref="ScanSystem.StartScanning(uint, DataFormat, ScanningMode)"/> is called.
        /// </remarks>
        public void ClearProfiles()
        {
            while (Profiles.TryTake(out _)) { }
        }

        /// <summary>Requests a new status from the scan head.</summary>
        /// <remarks>
        /// Use this function to get the status of the scan head. Applications concerned
        /// with scan speed and data throughput should call this function sparingly as to not over task
        /// a given scan head while it is scanning.
        /// </remarks>
        /// <returns>The updated <see cref="ScanHeadStatus"/>.</returns>
        /// <exception cref="InvalidOperationException">
        /// <see cref="IsConnected"/> is <see langword="false"/>.<br/>
        /// -or-<br/>
        /// A loss of communication with the scan head occurred, usually caused by a network or power issue.
        /// </exception>
        public ScanHeadStatus RequestStatus()
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("Not connected.");
            }

            var s = senderReceiver.RequestStatus();
            CachedStatus = new ScanHeadStatus(s, Specification);
            return CachedStatus;
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Checks if the current scan head version is compatible with the version information
        /// passed in. The version is considered compatible if its version is equal or greater
        /// than the one supplied.
        /// </summary>
        internal bool IsVersionCompatible(int major, int minor, int patch)
        {
            if (Version.Major > major)
            {
                return true;
            }
            else if (Version.Major == major)
            {
                if (Version.Minor > minor)
                {
                    return true;
                }
                else if (Version.Minor == minor && Version.Patch >= patch)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Performs a remote soft power cycle of the scan head.
        /// </summary>
        /// <remarks>
        /// After this function successfully completes, it will take several
        /// seconds before the scan head will appear on the network and be available
        /// for use. On average, the scan head will take 30 seconds to reboot.
        /// </remarks>
        /// <exception cref="IOException">
        /// A loss of communication with the scan head occurred, usually caused by a network or power issue.
        /// </exception>
        public void Reboot()
        {
            Reboot(SerialNumber);
        }

        /// <summary>
        /// Performs a remote soft power cycle of the scan head.
        /// </summary>
        /// <param name="serial">The serial of the scan head to power cycle.</param>
        /// <remarks>
        /// After this function successfully completes, it will take several
        /// seconds before the scan head will appear on the network and be available
        /// for use. On average, the scan head will take 30 seconds to reboot.
        /// </remarks>
        /// <exception cref="IOException">
        /// A loss of communication with the scan head occurred, usually caused by a network or power issue.
        /// </exception>
        public static void Reboot(uint serial)
        {
            IPAddress ip;
            IPAddress clientIp;
            var discoveries = ScanSystem.Discover().GetAwaiter().GetResult();
            if (!discoveries.ContainsKey(serial))
            {
                try
                {
                    string host = $"JS-50-{serial}.local";
                    var hostInfo = Dns.GetHostEntry(host);
                    ip = hostInfo.AddressList.Single(a => a.AddressFamily == AddressFamily.InterNetwork);
                    clientIp = IPAddress.Any;
                }
                catch
                {
                    throw new InvalidOperationException($"Failed to reboot {serial}, not found on network.");
                }
            }
            else
            {
                ip = discoveries[serial].IpAddress;
                clientIp = discoveries[serial].ClientIpAddress;
            }

            using (var updaterTcpClient = new TcpClient(new IPEndPoint(clientIp, 0)))
            {
                updaterTcpClient.Connect(ip, Globals.ScanServerUpdatePort);
                var updaterStream = updaterTcpClient.GetStream();
                byte[] rebootRequest = new UpdateClient::MessageClientT { Type = UpdateClient::MessageType.REBOOT_REQUEST }.SerializeToBinary();
                ScanHeadSenderReceiver.TcpSend(rebootRequest, updaterStream);
                byte[] rebootBuf = ScanHeadSenderReceiver.TcpRead(updaterStream);
                var rebootRsp = UpdateServer::MessageServerT.DeserializeFromBinary(rebootBuf);
                var rebootStatus = rebootRsp.Data.AsStatusData();

                if (rebootStatus.Status != UpdateServer::Status.SUCCESS)
                {
                    throw new InvalidOperationException($"Reboot failed for scan head {serial} because {rebootStatus.Status}.");
                }
            }
        }

        /// <summary>
        /// Checks if the current scan head version is compatible with the version information
        /// passed in. The version is considered compatible if its version is equal or greater
        /// than the one supplied. Throws a <see cref="VersionCompatibilityException"/> if
        /// not compatible.
        /// </summary>
        internal void ThrowIfNotVersionCompatible(int major, int minor, int patch, [CallerMemberName] string caller = "")
        {
            if (!IsVersionCompatible(major, minor, patch))
            {
                string err = $"'{caller}' is not compatible with scan head version {Version}."
                    + $" Requires {major}.{minor}.{patch} or greater.";
                throw new VersionCompatibilityException(err);
            }
        }

        internal void Connect(Client::ConnectionType connType, TimeSpan timeout)
        {
            senderReceiver?.Dispose();
            senderReceiver = new ScanHeadSenderReceiver(this);

            if (!senderReceiver.Connect(connType, timeout))
            {
                return;
            }

            _ = RequestStatus();
        }

        internal void StartScanning(StartScanningOptions opts)
        {
            if (IsDirty())
            {
                throw new InvalidOperationException("Scan head configuration was not sent prior to StartScanning");
            }

            if (opts.PeriodUs < Specification.MinScanPeriodUs)
            {
                throw new ArgumentOutOfRangeException(nameof(opts),
                    $"Scan period {opts.PeriodUs}µs is smaller than specification " +
                    $"{Specification.MinScanPeriodUs}µs for scan head {ID}");
            }

            if (opts.PeriodUs > Specification.MaxScanPeriodUs)
            {
                throw new ArgumentOutOfRangeException(nameof(opts),
                    $"Scan period {opts.PeriodUs}µs is bigger than specification " +
                    $"{Specification.MaxScanPeriodUs}µs for scan head {ID}");
            }

            if (opts.PeriodUs < CachedStatus.MinScanPeriodUs)
            {
                throw new ArgumentOutOfRangeException(nameof(opts),
                    $"Requested scan period {opts.PeriodUs}µs is smaller than that allowed by the " +
                    $"current scan head configuration {CachedStatus.MinScanPeriodUs}µs for scan head {ID}.");
            }

            ClearProfiles();
            QueueManager.Clear();

            senderReceiver.StartScanning(opts);
        }

        internal void StopScanning()
        {
            senderReceiver.StopScanning();
        }

        /// <summary>
        /// Internal function for set window implementation.
        /// </summary>
        internal void SetWindow(CameraLaserPair pair, ScanWindow window)
        {
            if (IsScanning)
            {
                throw new InvalidOperationException("Can not set scan window while scanning.");
            }

            if (window == null)
            {
                throw new ArgumentNullException(nameof(window));
            }

            Windows[pair] = window.Clone() as ScanWindow;

            FlagDirty(ScanHeadDirtyStateFlags.Window);
        }

        /// <summary>
        /// Sends the window of each <see cref="CameraLaserPair"/>
        /// </summary>
        internal void SendAllWindows()
        {
            foreach (var pair in CameraLaserPairs)
            {
                senderReceiver.SendWindow(pair);
            }
        }

        /// <summary>
        /// Sends all alignments to the scan head to be stored for diagnostic purposes
        /// </summary>
        internal void StoreAllAlignments()
        {
            if (!IsVersionCompatible(16, 1, 11))
            {
                return;
            }

            foreach (var pair in CameraLaserPairs)
            {
                // only send alignment if it has been set
                if (Alignments.TryGetValue(pair, out var alignment)
                    && alignment.ShiftY != 0
                    && alignment.ShiftX != 0
                    && alignment.Roll != 0)
                {
                    senderReceiver.SendAlignmentStoreData(pair);
                }
            }
        }

        /// <summary>
        /// Sends the exclusion mask of each <see cref="CameraLaserPair"/>
        /// </summary>
        internal void SendAllExclusionMasks()
        {
            if (!IsVersionCompatible(16, 1, 0))
            {
                return;
            }

            foreach (var pair in CameraLaserPairs)
            {
                senderReceiver.SendExclusionMask(pair);
            }
        }

        internal void SendAllBrightnessCorrections()
        {
            if (!IsVersionCompatible(16, 1, 0))
            {
                return;
            }

            foreach (var pair in CameraLaserPairs)
            {
                senderReceiver.SendBrightnessCorrection(pair);
            }
        }

        internal void SendMappleCorrection(CameraLaserPair pair, double x, double y, double roll, List<string> notes = null)
        {
            ThrowIfNotVersionCompatible(16, 1, 11);
            senderReceiver.SendCorrectionStoreData(pair, x, y, roll, notes);
        }

        internal void SendScanSyncMapping(Dictionary<Encoder, uint> mapping)
        {
            if (!IsVersionCompatible(16, 3, 0))
            {
                return;
            }

            senderReceiver.SendScanSyncConfiguration(mapping);
        }

        internal IEnumerable<DiscoveredScanSync> RequestScanSyncs()
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("Not connected.");
            }

            var data = senderReceiver.RequestScanSyncs();
            return data.Scansyncs.Select(ss => new DiscoveredScanSync(ss));
        }

        internal void Disconnect()
        {
            senderReceiver.Disconnect();
            FlagDirty(ScanHeadDirtyStateFlags.AllDirty);
        }

        /// <summary>
        /// Gets the orientation that should be sent to the scan head before scanning.
        /// This ensures that the data is always in the canonical order its expected
        /// to be in by those consuming profiles.
        /// </summary>
        internal Client::CameraOrientation GetCameraOrientation(CameraLaserPair pair)
        {
            bool isCableUpstream = Orientation == ScanHeadOrientation.CableIsUpstream;
            if (Specification.CameraPortCableUpstream == CameraIdToPort(pair.Camera))
            {
                return isCableUpstream
                    ? Client.CameraOrientation.UPSTREAM
                    : Client.CameraOrientation.DOWNSTREAM;
            }
            else
            {
                return isCableUpstream
                    ? Client.CameraOrientation.DOWNSTREAM
                    : Client.CameraOrientation.UPSTREAM;
            }
        }

        // Should only ever be used when loading scan heads from file (JSON deserialization)
        internal void SetScanSystem(ScanSystem scanSystem)
        {
            this.scanSystem = scanSystem;
        }

        internal ScanSystem GetScanSystem()
        {
            return scanSystem;
        }

        /// <summary>
        /// Obtains a single profile from a scan head to be used for diagnostic purposes.
        /// </summary>
        internal IProfile GetDiagnosticProfile(CameraLaserPair pair, uint cameraExposureUs, uint laserOnTimeUs)
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("Not connected.");
            }

            if (IsScanning)
            {
                throw new InvalidOperationException("Scan system is scanning.");
            }

            // ensure the scan head has up to date configuration
            scanSystem.PreSendConfiguration();

            var requestData = new Client::ProfileRequestDataT
            {
                CameraOrientation = GetCameraOrientation(pair),
                CameraPort = CameraIdToPort(pair.Camera),
                CameraExposureNs = cameraExposureUs * 1000,
                LaserPort = LaserIdToPort(pair.Laser),
                LaserOnTimeNs = laserOnTimeUs * 1000,
                LaserDetectionThreshold = Configuration.LaserDetectionThreshold,
                SaturationThreshold = Configuration.SaturationThreshold,
            };

            var profileData = senderReceiver.RequestDiagnosticProfile(requestData);
            return new Profile(this, profileData);
        }

        /// <summary>
        /// Gets mapple X and Y values and current window from scan head.
        /// </summary>
        internal ScanHeadMappleData GetMappleData(uint cameraPort, uint laserPort)
        {
            if (!IsValidCameraPort(cameraPort))
            {
                throw new ArgumentException("Invalid camera port.", nameof(cameraPort));
            }

            if (!IsValidLaserPort(laserPort))
            {
                throw new ArgumentException("Invalid laser port.", nameof(laserPort));
            }

            var req = new Client::MappleRequestDataT
            {
                CameraPort = cameraPort,
                LaserPort = laserPort
            };

            var rsp = senderReceiver.RequestMappleData(req);

            uint rows = Specification.MaxCameraRows;
            uint cols = Specification.MaxCameraColumns;
            var mappleData = new ScanHeadMappleData(cameraPort, laserPort, rows, cols);

            // Straight copy is okay as long as the X and Y values are accessed as [row, col]
            Buffer.BlockCopy(rsp.XValues.ToArray(), 0, mappleData.XValues, 0, rsp.XValues.Count);
            Buffer.BlockCopy(rsp.YValues.ToArray(), 0, mappleData.YValues, 0, rsp.YValues.Count);

            // The window is received as a bitmap that maps to the physical sensor where a 0 means
            // the pixel is out of the window and a 1 means the pixel is in the window
            var bitmap = rsp.WindowBitmap;
            for (int i = 0; i < bitmap.Count; i++)
            {
                byte bits = bitmap[i];
                int row = (int)(i * 8 / cols);
                int col = (int)(i * 8 % cols);
                for (int bit = 0; bit < 8; bit++)
                {
                    mappleData.Window[row, col + bit] = (bits & (1 << bit)) != 0;
                }
            }

            return mappleData;
        }

        /// <summary>
        /// Sends a keep alive message to the scan head. This is used to inform the scan
        /// head to keep scanning. Failure to send a keep alive message within 3 seconds
        /// causes the scan head to cease scanning.
        /// </summary>
        internal void KeepAlive()
        {
            senderReceiver.KeepAlive();
        }

        /// <summary>
        /// Gets the camera port from the associated ID.
        /// </summary>
        internal uint CameraIdToPort(Camera camera)
        {
            int port = Specification.CameraPortToId.FindIndex(p => p == (uint)camera);
            if (port == -1)
            {
                throw new ArgumentException($"{camera} does not exist for scan head {ID}.", nameof(camera));
            }

            return (uint)port;
        }

        /// <summary>
        /// Gets the laser port from the associated ID.
        /// </summary>
        internal uint LaserIdToPort(Laser laser)
        {
            int port = Specification.LaserPortToId.FindIndex(p => p == (uint)laser);
            if (port == -1)
            {
                throw new ArgumentException($"{laser} does not exist for scan head {ID}.", nameof(laser));
            }

            return (uint)port;
        }

        /// <summary>
        /// Gets the camera ID from the associated port.
        /// </summary>
        internal Camera CameraPortToId(uint cameraPort)
        {
            if (!IsValidCameraPort(cameraPort))
            {
                throw new ArgumentException("Invalid camera port.", nameof(cameraPort));
            }

            return (Camera)Specification.CameraPortToId[(int)cameraPort];
        }

        /// <summary>
        /// Gets the laser ID from the associated port.
        /// </summary>
        internal Laser LaserPortToId(uint laserPort)
        {
            if (!IsValidLaserPort(laserPort))
            {
                throw new ArgumentException("Invalid laser port.", nameof(laserPort));
            }

            return (Laser)Specification.LaserPortToId[(int)laserPort];
        }

        internal bool IsValidCamera(Camera camera)
        {
            return IsValidCameraPort(CameraIdToPort(camera));
        }

        internal bool IsValidLaser(Laser laser)
        {
            return IsValidLaserPort(LaserIdToPort(laser));
        }

        internal bool IsValidCameraPort(uint cameraPort)
        {
            return Specification.ConfigurationGroups.Exists(cg => cg.CameraPort == cameraPort);
        }

        internal bool IsValidLaserPort(uint laserPort)
        {
            return Specification.ConfigurationGroups.Exists(cg => cg.LaserPort == laserPort);
        }

        internal CameraLaserPair GetPair(Camera camera)
        {
            if (Specification.ConfigurationGroupPrimary != Client.ConfigurationGroupPrimary.CAMERA)
            {
                throw new ArgumentException($"Invalid configuration element for scan head {ID}. Use {typeof(Laser)} to configure.");
            }

            var groups = Specification.ConfigurationGroups;
            uint cameraPort = CameraIdToPort(camera);
            uint laserPort = groups.Single(g => g.CameraPort == cameraPort).LaserPort;
            var laser = LaserPortToId(laserPort);
            return new CameraLaserPair(camera, laser);
        }

        internal CameraLaserPair GetPair(Laser laser)
        {
            if (Specification.ConfigurationGroupPrimary != Client.ConfigurationGroupPrimary.LASER)
            {
                throw new ArgumentException($"Invalid configuration element for scan head {ID}. Use {typeof(Camera)} to configure.");
            }

            var groups = Specification.ConfigurationGroups;
            uint laserPort = LaserIdToPort(laser);
            uint cameraPort = groups.Single(g => g.LaserPort == laserPort).CameraPort;
            var camera = CameraPortToId(cameraPort);
            return new CameraLaserPair(camera, laser);
        }

        internal uint MinScanPeriod()
        {
            return Math.Max(CachedStatus.MinScanPeriodUs, Specification.MinScanPeriodUs);
        }

        internal bool IsDirty()
        {
            return dirtyState != ScanHeadDirtyStateFlags.Clean;
        }

        internal bool IsDirty(ScanHeadDirtyStateFlags flag)
        {
            return dirtyState.HasFlag(flag);
        }

        #endregion

        #region Private Methods

        internal static Client::ScanHeadSpecificationT GetSpecification(ProductType type)
        {
            string binName;
            switch (type)
            {
                case ProductType.JS50WSC:
                    binName = "js50wsc.bin";
                    break;
                case ProductType.JS50WX:
                    binName = "js50wx.bin";
                    break;
                case ProductType.JS50X6B20:
                    binName = "js50x6b20.bin";
                    break;
                case ProductType.JS50X6B30:
                    binName = "js50x6b30.bin";
                    break;
                case ProductType.JS50MX:
                    binName = "js50mx.bin";
                    break;
                case ProductType.JS50Z820:
                    binName = "js50z820.bin";
                    break;
                case ProductType.JS50Z830:
                    binName = "js50z830.bin";
                    break;
                default:
                    throw new ArgumentException($"Invalid product type: {type}.", nameof(type));
            }

            return Schema.ProductSpecification.GetSpecification(binName);
        }

        internal void FlagDirty(ScanHeadDirtyStateFlags flag)
        {
            if (flag.Equals(ScanHeadDirtyStateFlags.Clean))
            {
                return;
            }

            dirtyState |= flag;
        }

        internal void ClearDirty()
        {
            dirtyState = ScanHeadDirtyStateFlags.Clean;
        }

        /// <summary>
        /// Sets the timeout duration for heartbeat messages in milliseconds. Within the senderReceiver,
        /// the actual properties being set are ReceiveTimeout and SendTimeout on the TCP control client.
        /// </summary>
        /// <param name="timeoutMs">The timeout duration in milliseconds. Set to 0 to disable the timeout.</param>
        /// <remarks>
        /// The heartbeat mechanism is used to detect connection issues with the scan head.
        /// If no heartbeat response is received within the specified timeout period, the connection
        /// is considered lost. This method allows adjusting the sensitivity of this detection.
        /// This method requires firmware version 16.3.0 or later.
        /// </remarks>
        internal void SetHeartBeatTimeout(int timeoutMs)
        {
            ThrowIfNotVersionCompatible(16, 3, 0);
            senderReceiver.SetHeartBeatTCPTimeout(timeoutMs);
        }

        /// <summary>
        /// Sends a heartbeat request to the scan head to verify the connection is still active.
        /// </summary>
        /// <remarks>
        /// This method is used to check if the scan head is still responsive. It requires firmware
        /// version 16.3.0 or later. If the connection is lost, an IOException will be thrown with
        /// details about the socket error.
        /// </remarks>
        /// <exception cref="IOException">
        /// Thrown when the heartbeat request fails, indicating a loss of connection with the scan head.
        /// The exception message includes details about the underlying socket error.
        /// </exception>
        internal void GetHeartBeat()
        {
            try
            {
                senderReceiver.RequestHeartBeat();
            }
            catch (IOException e)
            {
                // Throw as an IOException, use the original message, but include (socket exception: error code)
                if (e.InnerException?.InnerException is SocketException se)
                {
                    throw new IOException(e.Message + $" (socket exception: {se.SocketErrorCode})", e);
                }
                throw;
            }
        }

        #endregion
    }
}
