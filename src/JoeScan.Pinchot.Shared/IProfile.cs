using System;
using System.Collections.Generic;

namespace JoeScan.Pinchot
{
    /// <summary>
    /// Profile data from a scan head.
    /// </summary>
    public interface IProfile
    {
        /// <summary>
        /// Gets the ID of the <see cref="ScanHead"/> the <see cref="Profile"/> originates from.
        /// </summary>
        /// <value>The ID of the <see cref="ScanHead"/> the <see cref="Profile"/> originates from.</value>
        uint ScanHeadID { get; }

        /// <summary>
        /// Gets the <see cref="Camera"/> the <see cref="Profile"/> originates from.
        /// </summary>
        /// <value>The <see cref="Camera"/> the <see cref="Profile"/> originates from.</value>
        Camera Camera { get; }

        /// <summary>
        /// Gets the <see cref="Laser"/> used to generate the <see cref="Profile"/>.
        /// </summary>
        /// <value>The <see cref="Laser"/> used to generate the <see cref="Profile"/>.</value>
        Laser Laser { get; }

        /// <summary>
        /// Gets the time of the scan head in nanoseconds when the <see cref="Profile"/> was generated.
        /// </summary>
        /// <value>The time of the scan head in nanoseconds when the <see cref="Profile"/> was generated.</value>
        long Timestamp { get; }

        /// <summary>
        /// Gets the encoder positions when the <see cref="Profile"/> was generated.
        /// </summary>
        /// <value>A <see cref="IDictionary{TKey,TValue}"/> of encoder positions when the <see cref="Profile"/> was generated.</value>
        IDictionary<Encoder, long> EncoderValues { get; }

        /// <summary>
        /// Gets the laser on time in microseconds used to generate the <see cref="Profile"/>.
        /// </summary>
        /// <value>The laser on time in microseconds used to generate the <see cref="Profile"/>.</value>
        double LaserOnTime { get; }

        /// <summary>
        /// Gets the <see cref="Point2D"/> data for the <see cref="Profile"/>, including invalid points.
        /// </summary>
        /// <value>A <see cref="Span{T}"/> of <see cref="Point2D"/> data for the <see cref="Profile"/>.</value>
        Span<Point2D> RawPoints { get; }

        /// <summary>
        /// Gets the number of valid <see cref="Point2D"/>s in <see cref="RawPoints"/>.
        /// </summary>
        /// <value>The number of valid <see cref="Point2D"/>s in <see cref="RawPoints"/>.</value>
        int ValidPointCount { get; }

        /// <summary>
        /// Gets the <see cref="DataFormat"/> of the <see cref="Profile"/>.
        /// </summary>
        /// <value>The <see cref="DataFormat"/> of the <see cref="Profile"/>.</value>
        DataFormat DataFormat { get; }

        /// <summary>
        /// Gets the valid <see cref="Point2D"/>s in the <see cref="Profile"/>.
        /// </summary>
        /// <returns>A <see cref="IEnumerable{Point2D}"/> of the valid <see cref="Point2D"/>s in the <see cref="Profile"/>.</returns>
        IEnumerable<Point2D> GetValidXYPoints();

        /// <summary>
        /// Gets the valid <see cref="Point2D"/>s in the <see cref="Profile"/>.
        /// </summary>
        /// <param name="validPoints">A <see cref="Span{T}"/> of <see cref="Point2D"/> that is the
        /// storage location for the valid <see cref="Point2D"/>s. Must be of length greater than
        /// or equal to <see cref="ValidPointCount"/>.</param>
        void GetValidXYPoints(Span<Point2D> validPoints);

        /// <summary>
        /// <see cref="Profile"/> implements <see cref="ICloneable"/>.
        /// </summary>
        /// <returns>A shallow copy of the <see cref="Profile"/> object.</returns>
        object Clone();
    }
}
