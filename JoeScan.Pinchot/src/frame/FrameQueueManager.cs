// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace JoeScan.Pinchot
{
    internal class FrameQueueManager
    {
        private Dictionary<CameraLaserPair, FrameQueue> Queues { get; }
            = new Dictionary<CameraLaserPair, FrameQueue>();

        internal bool FrameQueueOverflowed => Queues.Any(q => q.Value.QueueOverflowed);

        internal int NumQueues => Queues.Count;

        internal void Clear()
        {
            foreach (var queue in Queues.Values)
            {
                queue.Clear();
            }
        }

        internal void EnqueueProfile(IProfile profile)
        {
            var pair = new CameraLaserPair(profile.Camera, profile.Laser);
            var queue = Queues[pair];
            queue.Enqueue(profile);
        }

        /// <summary>
        /// Fills <paramref name="dst"/> with profiles. Returns a <see langword="bool"/>
        /// that is <see langword="true"/> if all slots in <paramref name="dst"/> are
        /// filled with valid profiles else <see langword="false"/> if one or more of
        /// the slots are <see langword="null"/>.
        /// </summary>
        internal bool Dequeue(Span<IProfile> dst, uint currentSequence)
        {
            bool complete = true;

            int i = 0;
            foreach (var queue in Queues.Values)
            {
                var profile = queue.Peek();

                // empty queue, mark slot invalid
                if (profile == null)
                {
                    dst[i++] = null;
                    complete = false;
                    continue;
                }

                // queue is ahead of current sequence, mark slot invalid
                if (profile.SequenceNumber > currentSequence)
                {
                    dst[i++] = null;
                    complete = false;
                    continue;
                }

                // discard profiles with previous sequence numbers
                // until we have one with the current sequence number
                do
                {
                    if (!queue.TryDequeue(out profile))
                    {
                        profile = null;
                        complete = false;
                        break;
                    }
                } while (profile.SequenceNumber < currentSequence);

                dst[i++] = profile;
            }

            return complete;
        }

        internal FrameQueueStats GetStats()
        {
            uint minSeq = uint.MaxValue;
            uint maxSeq = 0;
            int minSize = int.MaxValue;
            int maxSize = 0;

            foreach (var kvp in Queues)
            {
                var queue = kvp.Value;

                uint firstSeq = queue.FirstSequence;
                if (minSeq > firstSeq) { minSeq = firstSeq; }

                uint lastSeq = queue.LastSequence;
                if (maxSeq < lastSeq) { maxSeq = lastSeq; }

                int size = queue.Count;
                if (minSize > size) { minSize = size; }
                if (maxSize < size) { maxSize = size; }
            }

            return new FrameQueueStats
            {
                MaxSize = maxSize,
                MinSize = minSize,
                MaxSeq = maxSeq,
                MinSeq = minSeq
            };
        }

        internal void SetValidCameraLaserPairs(IEnumerable<CameraLaserPair> pairs)
        {
            if (Queues.Keys.SequenceEqual(pairs))
            {
                return;
            }

            Queues.Clear();

            int bufferSize = Globals.ProfileQueueSize / pairs.Count();

            foreach (var clp in pairs)
            {
                Queues.Add(clp, new FrameQueue(bufferSize));
            }
        }
    }
}
