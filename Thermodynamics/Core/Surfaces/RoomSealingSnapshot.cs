using System;
using System.Collections.Generic;

namespace Thermodynamics.Core
{
    /// <summary>Sparse structural-sealing pages; unwritten cells are open and prior generations are invisible.</summary>
    internal sealed class RoomSealingSnapshot
    {
        private const int PageShift = 12;
        private const int PageSize = 1 << PageShift;
        private sealed class Page
        {
            public readonly byte[] Cells = new byte[PageSize];
            public long Generation, Key;
            public Page Previous, Next;
        }
        private readonly Dictionary<long, Page> pages = new Dictionary<long, Page>();
        private long generation, cachedKey = -1;
        private Page cachedPage, activeHead, activeTail, freeHead, freeTail;

        internal int RetainedPageCount { get { return pages.Count; } }

        public void Reset()
        {
            if (generation == long.MaxValue)
            {
                pages.Clear(); activeHead = activeTail = freeHead = freeTail = null;
                generation = 0;
            }
            // Retain a high-water pool, not every key ever touched. Move the active chain
            // to the free chain without walking it; writes clear/rekey one page at a time.
            if (activeHead != null)
            {
                activeHead.Previous = freeTail;
                if (freeTail == null) freeHead = activeHead;
                else freeTail.Next = activeHead;
                freeTail = activeTail;
                activeHead = activeTail = null;
            }
            generation++;
            cachedKey = -1; cachedPage = null;
        }

        private Page Find(long key)
        {
            if (key != cachedKey)
            {
                cachedKey = key;
                pages.TryGetValue(key, out cachedPage);
            }
            return cachedPage;
        }

        private void UnlinkFree(Page page)
        {
            if (page.Previous == null) freeHead = page.Next;
            else page.Previous.Next = page.Next;
            if (page.Next == null) freeTail = page.Previous;
            else page.Next.Previous = page.Previous;
            page.Previous = page.Next = null;
        }

        public byte Read(long index)
        {
            Page page = Find(index >> PageShift);
            return page == null || page.Generation != generation ? (byte)0 : page.Cells[(int)(index & (PageSize - 1))];
        }

        public void Write(long index, byte value)
        {
            if (value == 0) return;
            long key = index >> PageShift;
            Page page = Find(key);
            if (page == null || page.Generation != generation)
            {
                bool clear = true;
                if (page != null) UnlinkFree(page);
                else if (freeHead != null)
                {
                    page = freeHead; UnlinkFree(page);
                    pages.Remove(page.Key);
                    page.Key = key; pages.Add(key, page);
                }
                else
                {
                    page = new Page { Key = key }; pages.Add(key, page);
                    clear = false;
                }
                if (clear) Array.Clear(page.Cells, 0, PageSize);
                page.Generation = generation;
                page.Previous = activeTail;
                if (activeTail == null) activeHead = page;
                else activeTail.Next = page;
                activeTail = page;
                cachedPage = page;
            }
            page.Cells[(int)(index & (PageSize - 1))] = value;
        }
    }
}
