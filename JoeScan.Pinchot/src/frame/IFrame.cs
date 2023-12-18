// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

namespace JoeScan.Pinchot
{
    /// <summary>
    /// Frame data from a scan system.
    /// </summary>
    /// <remarks>
    /// A frame contains a slot for each phase element of every <see cref="ScanHead"/> in a
    /// <see cref="ScanSystem"/>. A slot can either be a valid <see cref="IProfile"/> or
    /// <see langword="null"/> if something went wrong or was misconfigured.
    /// </remarks>
    /// <seealso cref="ScanSystem.WaitForFrame(System.TimeSpan, System.Threading.CancellationToken)"/>
    /// <seealso cref="ScanSystem.TakeFrame(System.Threading.CancellationToken)"/>
    /// <seealso cref="ScanSystem.TryTakeFrame(out IFrame, System.TimeSpan, System.Threading.CancellationToken)"/>
    public interface IFrame
    {
        /// <summary>
        /// Gets the slot at <paramref name="index"/>. Slots can contain either a valid
        /// <see cref="IProfile"/> or <see langword="null"/>. Use <see cref="Count"/>
        /// to get the total number of slots in this frame.
        /// </summary>
        /// <param name="index">The index of the slot.</param>
        /// <returns>A valid <see cref="IProfile"/> or <see langword="null"/>.</returns>
        ref IProfile this[int index]
        {
            get;
        }

        /// <summary>
        /// Gets the total number of slots in this frame.
        /// </summary>
        /// <value>The total number of slots in this frame.</value>
        int Count { get; }

        /// <summary>
        /// Gets the monotonically increasing frame sequence number.
        /// </summary>
        /// <value>The frame sequence number.</value>
        uint SequenceNumber { get; }

        /// <summary>
        /// Gets if the frame is complete or not. A frame will be
        /// considered complete only if all slots are valid (non-null).
        /// </summary>
        /// <value><see langword="true"/> if the frame is complete.</value>
        bool IsComplete { get; }
    }
}
