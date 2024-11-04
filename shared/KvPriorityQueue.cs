using System;
using System.Collections.Generic;

namespace shared {

    // [WARNING] This class is NOT thread-safe! By default it's a min-heap.
    // Reference https://github.com/genxium/DelayNoMore/blob/v1.0.15/frontend/assets/scripts/PriorityQueue.js, BUT in the C# version, "key" ALWAYS means "lookupKey" to be distinguished from "score".
    public class KvPriorityQueue<TKey, TVal>
            where TVal : class {

        public delegate int ValScore(TVal s);

        private int TERMINATING_ID = -1;
        public FrameRingBuffer<TVal> vals; // Using "FrameRingBuffer" because we have to support all operations "access by index", "pop" & "put"!  

        protected ValScore scoringFunc;

        protected Dictionary<TKey, int> lookupKeyToIndex;
        protected Dictionary<int, TKey> frameIdToLookupKey; // Compared with https://github.com/genxium/DelayNoMoreUnity/blob/v1.6.7/shared/KvPriorityQueue.cs#L16, this alllows me to use integer keys

        public KvPriorityQueue(int n, ValScore aScoringFunc) {
            vals = new FrameRingBuffer<TVal>(n);
            lookupKeyToIndex = new Dictionary<TKey, int>(); // Here "index" refers to the "frameId" in "FrameRingBuffer<TVal> vals"
            frameIdToLookupKey = new Dictionary<int, TKey>();
            scoringFunc = aScoringFunc;
        }

        public bool Put(TKey lookupKey, TVal val) {
            if (!lookupKeyToIndex.ContainsKey(lookupKey)) {
                int i = vals.EdFrameId;
                vals.Put(val);
                lookupKeyToIndex[lookupKey] = i;
                frameIdToLookupKey[i] = lookupKey;
                heapifyUp(i);
            } else {
                int i = lookupKeyToIndex[lookupKey];
                vals.SetByFrameId(val, i);

                heapifyUp(i);
                heapifyDown(i);
            }
            return true;
        }

        public int Cnt() {
            return lookupKeyToIndex.Count;
        }

        public TVal? Top() {
            if (0 == vals.Cnt) {
                return null;
            }

            var minVal = vals.GetFirst();
            if (null == minVal) {
                throw new ArgumentNullException(String.Format("Couldn't find first in vals"));
            }

            return minVal;
        }

        public TVal? Pop() {
            if (0 == vals.Cnt) {
                return null;
            }

            int origStFrameId = vals.StFrameId;
            if (1 == vals.Cnt) {
                var lookupKey = frameIdToLookupKey[origStFrameId];
                if (null == lookupKey) {
                    throw new ArgumentNullException(String.Format("Couldn't find first in keys"));
                }
                lookupKeyToIndex.Remove(lookupKey);
                frameIdToLookupKey.Remove(origStFrameId);
                var (_, ret) = vals.Pop();
                return ret;
            }

            var minVal = vals.GetFirst();
            if (null == minVal) {
                throw new ArgumentNullException(String.Format("Couldn't find first in vals"));
            }
            var minKey = frameIdToLookupKey[origStFrameId];
            if (null == minKey) {
                throw new ArgumentNullException(String.Format("Couldn't find first in keys"));
            }

            lookupKeyToIndex.Remove(minKey);
            frameIdToLookupKey.Remove(origStFrameId);
            
            int origEdFrameId = vals.EdFrameId;
            var tailKey = frameIdToLookupKey[origEdFrameId - 1];
            var (_, tailVal) = vals.PopTail();
            if (null == tailVal) {
                throw new ArgumentNullException(String.Format("Couldn't find tail in vals"));
            }
            vals.SetByFrameId(tailVal, origStFrameId);
            lookupKeyToIndex[tailKey] = origStFrameId;
            frameIdToLookupKey[origStFrameId] = tailKey;

            heapifyDown(origStFrameId);
            return minVal;
        }

        public TVal? Peek(TKey lookupKey) {
            if (0 == vals.Cnt) {
                return null;
            }

            if (!lookupKeyToIndex.ContainsKey(lookupKey)) {
                return null;
            }

            int i = lookupKeyToIndex[lookupKey];
            var (res1, thatVal) = vals.GetByFrameId(i);
            if (!res1 || null == thatVal) {
                throw new ArgumentNullException(String.Format("Couldn't find i={0} in vals", i));
            }
            return thatVal;
        }

        public TVal? PopAny(TKey lookupKey) {
            if (0 == vals.Cnt) {
                return null;
            }

            if (!lookupKeyToIndex.ContainsKey(lookupKey)) {
                return null;
            }

            if (1 == vals.Cnt) {
                return Pop();
            }

            int i = lookupKeyToIndex[lookupKey];
            var thatKey = frameIdToLookupKey[i];
            var (res1, thatVal) = vals.GetByFrameId(i);
            if (!res1 || null == thatVal) {
                throw new ArgumentNullException(String.Format("Couldn't find i={0} in vals", i));
            }

            int origEdFrameId = vals.EdFrameId;
            var tailKey = frameIdToLookupKey[origEdFrameId - 1];
            var (_, tailVal) = vals.PopTail();
            if (null == tailVal) {
                throw new ArgumentNullException(String.Format("Couldn't find tail in vals"));
            }
            lookupKeyToIndex.Remove(lookupKey);
            frameIdToLookupKey.Remove(origEdFrameId - 1);
            if (!lookupKeyToIndex.ContainsKey(tailKey)) {
                // Edge case: the lookupKey points to exactly the tail while heap size was larger than 1
                return thatVal;
            }

            vals.SetByFrameId(tailVal, i);
            lookupKeyToIndex[tailKey] = i;
            frameIdToLookupKey[i] = tailKey;

            heapifyUp(i);
            heapifyDown(i);

            return thatVal;
        }

        public void Clear() {
            vals.Clear();
            lookupKeyToIndex.Clear();
            frameIdToLookupKey.Clear();
        }

        private int compare(TVal lhs, TVal rhs) {
            int aScore = scoringFunc(lhs);
            int bScore = scoringFunc(rhs);
            return aScore < bScore ? -1 : aScore > bScore ? 1 : 0;
        }

        private void heapifyUp(int i) {
            if (1 >= vals.Cnt) return;
            int u = getParent(i);
            while (TERMINATING_ID != u) {
                var (res1, iVal) = vals.GetByFrameId(i);
                var (res2, uVal) = vals.GetByFrameId(u);
                if (!res1 || null == iVal) {
                    throw new ArgumentNullException(String.Format("Couldn't find i={0} in vals", i));
                }
                if (!res2 || null == uVal) {
                    throw new ArgumentNullException(String.Format("Couldn't find u={0} in vals", u));
                }

                if (compare(iVal, uVal) >= 0) break;
                swapInHolders(i, u);
                i = u;
                u = getParent(i);
            }
        }

        private void heapifyDown(int i) {
            if (1 >= vals.Cnt) return;
            int cur = i;
            int smallest = TERMINATING_ID;
            while (cur != smallest) {
                int l = getLeft(cur);
                int r = getRight(cur);

                smallest = cur;
                var (res, smallestVal) = vals.GetByFrameId(smallest);
                if (!res || null == smallestVal) {
                    throw new ArgumentNullException(String.Format("Couldn't find smallest={0} in vals#1", smallest));
                }
                var (_, lVal) = vals.GetByFrameId(l);
                if (null != lVal && compare(lVal, smallestVal) < 0) {
                    smallest = l;
                }
                (res, smallestVal) = vals.GetByFrameId(smallest);
                if (!res || null == smallestVal) {
                    throw new ArgumentNullException(String.Format("Couldn't find smallest={0} in vals#2", smallest));
                }
                var (_, rVal) = vals.GetByFrameId(r);
                if (null != rVal && compare(rVal, smallestVal) < 0) {
                    smallest = r;
                }

                if (smallest != cur) {
                    swapInHolders(cur, smallest);
                    cur = smallest;
                    smallest = TERMINATING_ID;
                }
            }
        }

        private void swapInHolders(int a, int b) {
            TKey aLookupKey = frameIdToLookupKey[a];
            TKey bLookupKey = frameIdToLookupKey[b];
           
            var (res3, aVal) = vals.GetByFrameId(a);
            if (!res3 || null == aVal) {
                throw new ArgumentNullException(String.Format("Couldn't find a={0} in vals", a));
            }
            var (res4, bVal) = vals.GetByFrameId(b);
            if (!res4 || null == bVal) {
                throw new ArgumentNullException(String.Format("Couldn't find b={0} in vals", b));
            }
            
            // swap in "vals"
            vals.SetByFrameId(bVal, a);
            vals.SetByFrameId(aVal, b);

            // swap in "lookupKeyToIndex"
            lookupKeyToIndex[aLookupKey] = b;
            lookupKeyToIndex[bLookupKey] = a;

            // swap in "keys"
            frameIdToLookupKey[a] = bLookupKey;
            frameIdToLookupKey[b] = aLookupKey;
        }

        private int getParent(int i) {
            if (0 == i) {
                return TERMINATING_ID;
            }
            return ((i - 1) >> 1);
        }

        private int getLeft(int i) {
            return (i << 1) + 1;
        }

        private int getRight(int i) {
            return (i << 1) + 2;
        }
    }
}
