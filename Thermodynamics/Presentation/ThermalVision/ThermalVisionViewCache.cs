using System;
using System.Collections.Generic;

namespace Thermodynamics.Presentation
{
    /// <summary>Retains a bounded set of snapshots while pausing work as soon as admission ends.</summary>
    public sealed class ThermalVisionViewCache<TKey,TValue>
    {
        private sealed class Entry
        {
            public TValue Value;
            public double LastUsed;
            public bool Admitted,Paused;
        }
        private readonly Dictionary<TKey,Entry> entries=new Dictionary<TKey,Entry>();
        private readonly List<TKey> retired=new List<TKey>();
        private readonly int capacity;
        private readonly double retentionSeconds;
        private readonly Action<TValue> pause;
        private readonly Func<TValue,bool> invalid;

        public ThermalVisionViewCache(int capacity,double retentionSeconds,Action<TValue> pause,Func<TValue,bool> invalid)
        {
            if(capacity<1) throw new ArgumentException("Positive snapshot capacity required", "capacity");
            if(retentionSeconds<0) throw new ArgumentException("Nonnegative retention required", "retentionSeconds");
            this.capacity=capacity; this.retentionSeconds=retentionSeconds;
            this.pause=pause; this.invalid=invalid;
        }

        public int Count { get { return entries.Count; } }

        private void Pause(Entry entry)
        {
            if(entry.Paused) return;
            pause(entry.Value); entry.Paused=true;
        }

        public void BeginFrame()
        {
            retired.Clear();
            foreach(var pair in entries)
            {
                var entry=pair.Value;
                entry.Admitted=false;
                if(!invalid(entry.Value)) continue;
                Pause(entry); retired.Add(pair.Key);
            }
            foreach(var key in retired) entries.Remove(key);
            retired.Clear();
        }

        public bool TryAdmit(TKey key,double now,out TValue value)
        {
            Entry entry;
            if(!entries.TryGetValue(key,out entry)) { value=default(TValue); return false; }
            // A returning view must expire before its last-use time is refreshed.
            // Trim cannot remove it after it has become admitted this frame.
            if(!entry.Admitted && (now<entry.LastUsed || now-entry.LastUsed>=retentionSeconds))
            {
                Pause(entry); entries.Remove(key); value=default(TValue); return false;
            }
            entry.Admitted=true; entry.Paused=false; entry.LastUsed=now;
            value=entry.Value; return true;
        }

        public void AddAdmitted(TKey key,TValue value,double now)
        {
            entries.Add(key,new Entry { Value=value,LastUsed=now,Admitted=true });
        }

        public void Trim(double now)
        {
            retired.Clear();
            foreach(var pair in entries)
            {
                var entry=pair.Value;
                if(entry.Admitted) continue;
                Pause(entry);
                if(now<entry.LastUsed || now-entry.LastUsed>=retentionSeconds) retired.Add(pair.Key);
            }
            foreach(var key in retired) entries.Remove(key);
            retired.Clear();
            while(entries.Count>capacity)
            {
                bool found=false; TKey oldest=default(TKey); double oldestTime=double.MaxValue;
                foreach(var pair in entries)
                {
                    var entry=pair.Value;
                    if(entry.Admitted || entry.LastUsed>=oldestTime) continue;
                    oldest=pair.Key; oldestTime=entry.LastUsed; found=true;
                }
                // Active admission is never evicted. Its caller keeps the admitted
                // count below capacity; only recently inactive snapshots compete.
                if(!found) break;
                entries.Remove(oldest);
            }
        }

        public void PauseAll() { foreach(var entry in entries.Values) Pause(entry); }
        public void Clear() { PauseAll(); entries.Clear(); retired.Clear(); }
    }
}
