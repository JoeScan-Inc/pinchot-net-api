// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using System;
using System.Collections;

namespace JoeScan.Pinchot
{
    /// <summary>
    /// An exclusion mask determines which pixels on the sensor image should be ignored.
    /// </summary>
    public class ExclusionMask : ICloneable
    {
        /// <summary>
        /// The bit array of the exclusion mask. This is easier to use
        /// than dealing with bit bashing of a byte array.
        /// </summary>
        private BitArray bits;

        /// <summary>
        /// The height of the exclusion mask.
        /// </summary>
        public uint Height { get; }

        /// <summary>
        /// The width of the exclusion mask.
        /// </summary>
        public uint Width { get; }

        /// <summary>
        /// Creates a new exclusion mask using the sensor dimension
        /// of the <paramref name="scanHead"/>.
        /// </summary>
        internal ExclusionMask(ScanHead scanHead)
        {
            Height = scanHead.Capabilities.MaxCameraImageHeight;
            Width = scanHead.Capabilities.MaxCameraImageWidth;
            int length = (int)(Height * Width);
            bits = new BitArray(length);
        }

        /// <summary>
        /// Gets or sets the <paramref name="pixel"/>.
        /// </summary>
        /// <param name="pixel">The pixel to get or set.</param>
        /// <returns>
        /// <see langword="true"/> if the pixel is excluded else <see langword="false"/>.
        /// </returns>
        public bool this[int pixel]
        {
            get => bits[pixel];
            set => bits[pixel] = value;
        }

        /// <summary>
        /// Gets the exclusion mask as a byte array.
        /// </summary>
        /// <returns>
        /// The mask as an array of bytes. Each byte cooresponds to
        /// eight pixels in the image where a bit value of 1 means the
        /// pixel is excluded.
        /// </returns>
        public byte[] GetMask()
        {
            byte[] mask = new byte[bits.Length / 8];
            bits.CopyTo(mask, 0);
            return mask;
        }

        /// <summary>
        /// Marks all pixels between <paramref name="startPixel"/> and
        /// <paramref name="endPixel"/> to be excluded.
        /// </summary>
        /// <param name="startPixel">The starting exclusion pixel.</param>
        /// <param name="endPixel">The ending exclusion pixel.</param>
        /// <exception cref="ArgumentException"></exception>
        public void SetPixels(int startPixel, int endPixel)
        {
            if (startPixel >= bits.Length * sizeof(byte) || startPixel < 0)
            {
                throw new ArgumentException("Start pixel is outside valid range.");
            }

            if (endPixel >= bits.Length * sizeof(byte) || endPixel < 0)
            {
                throw new ArgumentException("End pixel is outside valid range.");
            }

            if (startPixel > endPixel)
            {
                throw new ArgumentException("Start pixel must be less than end pixel.");
            }

            for (int i = 0; i < endPixel - startPixel + 1; ++i)
            {
                bits.Set(startPixel + i, true);
            }
        }

        /// <summary>
        /// Returns a clone of this object. Changes to the original
        /// object will not reflect in the cloned object.
        /// </summary>
        /// <returns>A clone of this object.</returns>
        public object Clone()
        {
            var em = MemberwiseClone() as ExclusionMask;
            em.bits = new BitArray(bits);
            return em;
        }
    }
}
