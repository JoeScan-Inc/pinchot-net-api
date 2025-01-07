using System;

namespace JoeScan.Pinchot.Beta
{
    /// <summary>
    /// Beta functionality for a scan head. These functions are for preliminary testing
    /// and should be used with the expectation that the signature might change in a
    /// future revision.
    /// </summary>
    public static class ScanHeadBeta
    {
        /// <summary>
        /// Creates a new <see cref="BrightnessCorrection"/> with the
        /// dimension of the camera(s) of the scan head.
        /// </summary>
        /// <returns>A default <see cref="BrightnessCorrection"/>.</returns>
        public static BrightnessCorrection CreateBrightnessCorrection(this ScanHead scanHead)
        {
            scanHead.ThrowIfNotVersionCompatible(16, 1, 0);

            return new BrightnessCorrection(scanHead);
        }

        /// <summary>
        /// Sets the <see cref="BrightnessCorrection"/> for the <paramref name="camera"/> supplied.
        /// </summary>
        /// <remarks>
        /// This is a beta feature that may be changed in the future. It is
        /// is offered here to provide access to functionality that may prove useful to
        /// end users and allow them to submit feedback back to JoeScan. In a future
        /// release, this code may change; care should be taken when adding to
        /// applications. For any questions, reach out to a JoeScan representative for
        /// guidance.
        /// </remarks>
        /// <param name="scanHead">The scan head that the correction will be applied to.</param>
        /// <param name="camera">The <see cref="Camera"/> that the correction will be applied to.</param>
        /// <param name="correction">The <see cref="BrightnessCorrection"/> to be applied.</param>
        /// <exception cref="InvalidOperationException">
        /// <see cref="ScanSystem.IsScanning"/> is <see langword="true"/>.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="correction"/> is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Trying to use a camera-driven function with a laser-driven scan head.
        /// Use <see cref="SetBrightnessCorrection(ScanHead, Laser, BrightnessCorrection)"/> instead.
        /// </exception>
        public static void SetBrightnessCorrection(this ScanHead scanHead, Camera camera, BrightnessCorrection correction)
        {
            scanHead.ThrowIfNotVersionCompatible(16, 1, 0);

            if (scanHead.GetScanSystem().IsScanning)
            {
                throw new InvalidOperationException("Can not set brightness correction while scanning.");
            }

            if (correction == null)
            {
                throw new ArgumentNullException(nameof(correction));
            }

            if (!scanHead.IsValidCamera(camera))
            {
                throw new ArgumentOutOfRangeException(nameof(camera), "Invalid camera.");
            }

            var pair = scanHead.GetPair(camera);
            scanHead.BrightnessCorrections[pair] = correction.Clone() as BrightnessCorrection;

            scanHead.FlagDirty(ScanHeadDirtyStateFlags.BrightnessCorrection);
        }

        /// <summary>
        /// Sets the <see cref="BrightnessCorrection"/> for the <paramref name="laser"/> supplied.
        /// </summary>
        /// <remarks>
        /// This is a beta feature that may be changed in the future. It is
        /// is offered here to provide access to functionality that may prove useful to
        /// end users and allow them to submit feedback back to JoeScan. In a future
        /// release, this code may change; care should be taken when adding to
        /// applications. For any questions, reach out to a JoeScan representative for
        /// guidance.
        /// </remarks>
        /// <param name="scanHead">The scan head that the correction will be applied to.</param>
        /// <param name="laser">The <see cref="Laser"/> that the correction will be applied to.</param>
        /// <param name="correction">The <see cref="BrightnessCorrection"/> to be applied.</param>
        /// <exception cref="InvalidOperationException">
        /// <see cref="ScanSystem.IsScanning"/> is <see langword="true"/>.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="correction"/> is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Trying to use a laser-driven function with a camera-driven scan head.
        /// Use <see cref="SetBrightnessCorrection(ScanHead, Camera, BrightnessCorrection)"/> instead.
        /// </exception>
        public static void SetBrightnessCorrection(this ScanHead scanHead, Laser laser, BrightnessCorrection correction)
        {
            scanHead.ThrowIfNotVersionCompatible(16, 1, 0);

            if (scanHead.GetScanSystem().IsScanning)
            {
                throw new InvalidOperationException("Can not set brightness correction while scanning.");
            }

            if (correction == null)
            {
                throw new ArgumentNullException(nameof(correction));
            }

            if (!scanHead.IsValidLaser(laser))
            {
                throw new ArgumentOutOfRangeException(nameof(laser), "Invalid laser.");
            }

            var pair = scanHead.GetPair(laser);
            scanHead.BrightnessCorrections[pair] = correction.Clone() as BrightnessCorrection;

            scanHead.FlagDirty(ScanHeadDirtyStateFlags.BrightnessCorrection);
        }
    }
}
