using System;
using System.Collections.Generic;

namespace shared {

    // [WARNING] This class is NOT thread-safe!
    // Reference https://github.com/genxium/DelayNoMore/blob/v1.0.15/frontend/assets/scripts/PriorityQueue.js
    public class KvPriorityQueue<TKey, TVal>
            where TKey : class
            where TVal : class {

        public delegate int ValScore(TVal s);

        private int TERMINATING_ID = -1;
        protected FrameRingBuffer<TVal> vals; // Using "FrameRingBuffer" because we have to support all operations "access by index", "pop" & "put"!  
        protected FrameRingBuffer<TKey> keys;

        protected ValScore scoringFunc;

        protected Dictionary<TKey, int> lookupKeyToIndex;
        public KvPriorityQueue(int n, ValScore aScoringFunc) {
            vals = new FrameRingBuffer<TVal>(n);
            keys = new FrameRingBuffer<TKey>(n);
            lookupKeyToIndex = new Dictionary<TKey, int>(); // Here "index" refers to the "frameId" in "FrameRingBuffer<TVal> vals"
            scoringFunc = aScoringFunc;
        }

        public bool Put(TKey lookupKey, TVal val) {
            int i = vals.EdFrameId;
            vals.Put(val);
            keys.Put(lookupKey);
            lookupKeyToIndex[lookupKey] = i;
            heapifyUp(i);
            return true;
        }

        public TVal? Pop() {
            if (0 == vals.Cnt) {
                return null;
            }
            if (1 == vals.Cnt) {
                var lookupKey = keys.GetFirst();
                if (null == lookupKey) {
                    throw new ArgumentNullException(String.Format("Couldn't find first in keys"));
                }
                lookupKeyToIndex.Remove(lookupKey);
                keys.Pop();
                var (_, ret) = vals.Pop();
                return ret;
            }

            var minVal = vals.GetFirst();
            if (null == minVal) {
                throw new ArgumentNullException(String.Format("Couldn't find first in vals"));
            }
            var minKey = keys.GetFirst();
            if (null == minKey) {
                throw new ArgumentNullException(String.Format("Couldn't find first in keys"));
            }
            lookupKeyToIndex.Remove(minKey);

            var (_, tailVal) = vals.PopTail();
            if (null == tailVal) {
                throw new ArgumentNullException(String.Format("Couldn't find tail in vals"));
            }
            var (_, tailKey) = keys.PopTail();
            if (null == tailKey) {
                throw new ArgumentNullException(String.Format("Couldn't find tail in keys"));
            }
            vals.SetByFrameId(tailVal, vals.StFrameId);
            keys.SetByFrameId(tailKey, keys.StFrameId);

            lookupKeyToIndex[tailKey] = keys.StFrameId;

            heapifyDown(keys.StFrameId);
            return minVal;
        }

        public void Clear() {
            vals.Clear();
            keys.Clear();
            lookupKeyToIndex.Clear();
        }

        private int compare(TVal lhs, TVal rhs) {
            int aScore = scoringFunc(lhs);
            int bScore = scoringFunc(rhs);
            return aScore < bScore ? -1 : aScore > bScore ? 1 : 0;
        }

        private void heapifyUp(int i) {
            int u = getParent(i);
            var (res1, iVal) = vals.GetByFrameId(i);
            var (res2, uVal) = vals.GetByFrameId(u);
            if (!res1 || null == iVal) {
                throw new ArgumentNullException(String.Format("Couldn't find i={0} in vals", i));
            }
            if (!res2 || null == uVal) {
                throw new ArgumentNullException(String.Format("Couldn't find u={0} in vals", u));
            }

            while (TERMINATING_ID != u && 0 > compare(iVal, uVal)) {
                swap(i, u);
                i = u;
                u = getParent(i);
                (res1, iVal) = vals.GetByFrameId(i);
                (res2, uVal) = vals.GetByFrameId(u);
                if (!res1 || null == iVal) {
                    throw new ArgumentNullException(String.Format("Couldn't find i={0} in vals", i));
                }
                if (!res2 || null == uVal) {
                    throw new ArgumentNullException(String.Format("Couldn't find u={0} in vals", u));
                }
            }
        }

        private void heapifyDown(int i) {
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
                if (null != lVal && 0 > compare(lVal, smallestVal)) {
                    smallest = l;
                }
                (res, smallestVal) = vals.GetByFrameId(smallest);
                if (!res || null == smallestVal) {
                    throw new ArgumentNullException(String.Format("Couldn't find smallest={0} in vals#2", smallest));
                }
                var (_, rVal) = vals.GetByFrameId(r);
                if (null != rVal && 0 > compare(rVal, smallestVal)) {
                    smallest = r;
                }

                if (smallest != cur) {
                    swap(cur, smallest);
                    cur = smallest;
                    smallest = TERMINATING_ID;
                }
            }
        }

        private void swap(int a, int b) {
            var (res1, aLookupKey) = keys.GetByFrameId(a);
            if (!res1 || null == aLookupKey) {
                throw new ArgumentNullException(String.Format("Couldn't find a={0} in keys", a));
            }
            var (res2, bLookupKey) = keys.GetByFrameId(b);
            if (!res2 || null == bLookupKey) {
                throw new ArgumentNullException(String.Format("Couldn't find b={0} in keys", b));
            }

            var (res3, aVal) = vals.GetByFrameId(a);
            if (!res3 || null == aVal) {
                throw new ArgumentNullException(String.Format("Couldn't find a={0} in vals", a));
            }
            var (res4, bVal) = vals.GetByFrameId(b);
            if (!res4 || null == bVal) {
                throw new ArgumentNullException(String.Format("Couldn't find b={0} in vals", b));
            }
            // swap in "keys"
            keys.SetByFrameId(bLookupKey, a);
            keys.SetByFrameId(aLookupKey, b);

            // swap in "vals"
            vals.SetByFrameId(bVal, a);
            vals.SetByFrameId(aVal, b);

            // swap in "lookupKeyToIndex"
            lookupKeyToIndex[aLookupKey] = b;
            lookupKeyToIndex[bLookupKey] = a;
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
