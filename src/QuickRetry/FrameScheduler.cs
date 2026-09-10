using System;
using System.Collections.Generic;

namespace QuickRetry
{
    /// <summary>
    /// Runs actions a fixed number of frames later. The loader entry point calls <see cref="Tick"/> once per frame.
    /// This avoids loader-specific coroutine APIs.
    /// </summary>
    internal static class FrameScheduler
    {
        private sealed class Entry
        {
            public int FramesLeft;
            public float NotBefore;
            public Action Action;
        }

        private static readonly List<Entry> Entries = new List<Entry>();

        public static void RunAfterFrames(int frames, Action action)
        {
            Entries.Add(new Entry { FramesLeft = frames, NotBefore = 0f, Action = action });
        }

        /// <summary>Runs the action after at least <paramref name="seconds"/> of unscaled time and at least one frame.</summary>
        public static void RunAfterSeconds(float seconds, Action action)
        {
            Entries.Add(new Entry { FramesLeft = 1, NotBefore = UnityEngine.Time.unscaledTime + seconds, Action = action });
        }

        public static void Tick()
        {
            if (Entries.Count == 0) return;
            for (int i = Entries.Count - 1; i >= 0; i--)
            {
                Entry e = Entries[i];
                e.FramesLeft--;
                if (e.FramesLeft > 0) continue;
                if (UnityEngine.Time.unscaledTime < e.NotBefore) continue;
                Entries.RemoveAt(i);
                try
                {
                    e.Action();
                }
                catch (Exception ex)
                {
                    ModLog.Error("FrameScheduler action failed", ex);
                }
            }
        }
    }
}
