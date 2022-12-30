using System;
using System.Collections.Generic;
using System.Linq;

namespace JoeScan.Pinchot.Beta
{
    /// <summary>
    /// This class can be used to calibrate the <see cref="Point2D.Brightness"/>
    /// values returned in an <see cref="IProfile"/> by setting the offset with
    /// <see cref="Offset"/> and the scale factors with <see cref="this[int]"/>.
    /// </summary>
    public class BrightnessCorrection : ICloneable
    {
        private List<float> scaleFactors;

        /// <summary>
        /// Gets all the scale factors. Use <see cref="this[int]"/>
        /// to modify the factors.
        /// </summary>
        /// <returns>A copy of the list of scale factors.</returns>
        public List<float> GetScaleFactors() => new List<float>(scaleFactors);

        /// <summary>
        /// The number of scale factors. There is one scale
        /// factor for each column of the camera.
        /// </summary>
        /// <returns>The number of scale factors.</returns>
        public int Count => scaleFactors.Count;

        /// <summary>
        /// The offset to be applied to all brightness values.
        /// </summary>
        /// <returns>The brightness offset.</returns>
        public int Offset { get; set; }

        /// <summary>
        /// Gets or sets the scale factor of the <paramref name="column"/>.
        /// There are <see cref="Count"/> number of columns that can be set.
        /// </summary>
        /// <param name="column">The column to get or set the scale factor.</param>
        /// <returns>
        /// The scale factor to be applied to the brightness of the <paramref name="column"/>.
        /// </returns>
        /// <exception cref="ArgumentException">
        /// <see langword="value"/> is non-positive, NaN, or infinity.
        /// </exception>
        public float this[int column]
        {
            get => scaleFactors[column];
            set
            {
                if (value <= 0 || float.IsNaN(value) || float.IsInfinity(value))
                {
                    throw new ArgumentException("Invalid scale factor value", nameof(value));
                }

                scaleFactors[column] = value;
            }
        }

        /// <summary>
        /// Creates a new brightness correction using the sensor dimension
        /// of the <paramref name="scanHead"/>.
        /// </summary>
        internal BrightnessCorrection(ScanHead scanHead)
        {
            scaleFactors = Enumerable.Repeat(1.0f, (int)scanHead.Capabilities.MaxCameraImageWidth).ToList();
            Offset = 0;
        }

        /// <summary>
        /// Returns a clone of this object. Changes to the original
        /// object will not reflect in the cloned object.
        /// </summary>
        /// <returns>A clone of this object.</returns>
        public object Clone()
        {
            var bc = MemberwiseClone() as BrightnessCorrection;
            bc.scaleFactors = new List<float>(scaleFactors);
            return bc;
        }
    }
}
