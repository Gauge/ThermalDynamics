using System;
using System.Collections.Generic;
using VRageMath;

namespace Thermodynamics.Core
{
    /// <summary>Dense or lazily paged cell bits. Resetting pages invalidates them without clearing the bounding volume.</summary>
    internal sealed class PagedCellBitset
    {
        private const int PageShift = 12;
        private const int WordsPerPage = 1 << (PageShift - 6);
        private sealed class Page
        {
            public readonly long[] Words = new long[WordsPerPage];
            public int[] Ranks;
            public long Generation, Key;
            public Page Previous, Next;
        }
        private readonly CellBitset dense = new CellBitset();
        private readonly Dictionary<long, Page> pages = new Dictionary<long, Page>();
        private readonly List<long> rankPages = new List<long>();
        private bool paged, ranked;
        private long generation, capacity, cachedKey = -1;
        private Page cachedPage, activeHead, activeTail, freeHead, freeTail;
        private Vector3I min;
        private int sizeX, sizeY, sizeZ, count;

        public int Count { get { return paged ? count : dense.Count; } }
        internal int RetainedPageCount { get { return pages.Count; } }

        public void Clear()
        {
            if (paged) Reset(min, min + new Vector3I(sizeX, sizeY, sizeZ), true);
            else dense.Clear();
        }

        public void Reset(Vector3I lower, Vector3I upper, bool usePages)
        {
            paged = usePages;
            if (!paged) { dense.Reset(lower, upper); return; }
            min = lower;
            sizeX = Math.Max(0, upper.X - lower.X);
            sizeY = Math.Max(0, upper.Y - lower.Y);
            sizeZ = Math.Max(0, upper.Z - lower.Z);
            capacity = (long)sizeX * sizeY * sizeZ;
            if (generation == long.MaxValue)
            {
                pages.Clear(); activeHead = activeTail = freeHead = freeTail = null;
                generation = 0;
            }
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
            count = 0; ranked = false;
        }

        public long IndexOf(Vector3I cell)
        {
            if (!paged) return dense.IndexOf(cell);
            int x = cell.X - min.X, y = cell.Y - min.Y, z = cell.Z - min.Z;
            if (x < 0 || x >= sizeX || y < 0 || y >= sizeY || z < 0 || z >= sizeZ) return -1;
            return (((long)z * sizeY) + y) * sizeX + x;
        }

        private Page FindPage(long key)
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

        private long Word(long index)
        {
            Page page = FindPage(index >> (PageShift - 6));
            return page == null || page.Generation != generation ? 0L : page.Words[(int)(index & (WordsPerPage - 1))];
        }

        public bool ContainsIndex(long index)
        {
            if (!paged) return dense.ContainsIndex(index);
            if (index < 0 || index >= capacity) return false;
            return (Word(index >> 6) & (1L << (int)(index & 63))) != 0L;
        }

        public bool Contains(Vector3I cell) { return ContainsIndex(IndexOf(cell)); }
        public bool Add(Vector3I cell) { return AddIndex(IndexOf(cell)); }

        public bool AddIndex(long index)
        {
            if (!paged) return dense.AddIndex(index);
            if (index < 0 || index >= capacity) return false;
            long key = index >> PageShift;
            Page page = FindPage(key);
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
                if (clear) Array.Clear(page.Words, 0, WordsPerPage);
                page.Generation = generation;
                page.Previous = activeTail;
                if (activeTail == null) activeHead = page;
                else activeTail.Next = page;
                activeTail = page;
                cachedPage = page;
            }
            int word = (int)((index >> 6) & (WordsPerPage - 1));
            long bit = 1L << (int)(index & 63);
            if ((page.Words[word] & bit) != 0L) return false;
            page.Words[word] |= bit;
            count++; ranked = false;
            return true;
        }

        public long NextClearIndex(long index, long endExclusive, out int examined)
        {
            if (!paged) return dense.NextClearIndex(index, endExclusive, out examined);
            examined = 0;
            if (index < 0) index = 0;
            while (index < endExclusive)
            {
                long word = index >> 6;
                if (word >= (capacity + 63) / 64) return index;
                long bits = Word(word);
                examined++;
                int offset = (int)(index & 63);
                if ((~bits >> offset) == 0L) { index = (word + 1) << 6; continue; }
                for (int bit = offset; bit < 64; bit++)
                {
                    if ((bits & (1L << bit)) != 0L) continue;
                    long found = (word << 6) + bit;
                    return found < endExclusive ? found : endExclusive;
                }
                index = (word + 1) << 6;
            }
            return endExclusive;
        }

        public void BuildRanks()
        {
            if (!paged) { dense.BuildRanks(); return; }
            rankPages.Clear();
            for (Page page = activeHead; page != null; page = page.Next)
                rankPages.Add(page.Key);
            rankPages.Sort();
            int running = 0;
            for (int i = 0; i < rankPages.Count; i++)
            {
                Page page = pages[rankPages[i]];
                if (page.Ranks == null) page.Ranks = new int[WordsPerPage];
                for (int w = 0; w < WordsPerPage; w++)
                {
                    page.Ranks[w] = running;
                    running += Population(page.Words[w]);
                }
            }
            ranked = true;
        }

        public int RankOfIndex(long index)
        {
            if (!paged) return dense.RankOfIndex(index);
            if (!ranked || index < 0 || (index >> 6) >= (capacity + 63) / 64) return -1;
            long key = index >> PageShift;
            Page page = FindPage(key);
            if (page == null || page.Generation != generation)
            {
                // A clear page still has a rank: the count at the preceding page's end.
                int low = 0, high = rankPages.Count;
                while (low < high)
                {
                    int middle = low + (high - low) / 2;
                    if (rankPages[middle] < key) low = middle + 1;
                    else high = middle;
                }
                if (low == 0) return 0;
                Page before = pages[rankPages[low - 1]];
                return before.Ranks[WordsPerPage - 1] + Population(before.Words[WordsPerPage - 1]);
            }
            int word = (int)((index >> 6) & (WordsPerPage - 1));
            long below = page.Words[word] & ((1L << (int)(index & 63)) - 1L);
            return page.Ranks[word] + Population(below);
        }

        private static int Population(long value)
        {
            ulong v = (ulong)value;
            v = v - ((v >> 1) & 0x5555555555555555UL);
            v = (v & 0x3333333333333333UL) + ((v >> 2) & 0x3333333333333333UL);
            v = (v + (v >> 4)) & 0x0F0F0F0F0F0F0F0FUL;
            return (int)((v * 0x0101010101010101UL) >> 56);
        }
    }
}
