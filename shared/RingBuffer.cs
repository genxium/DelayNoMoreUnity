using System;

namespace shared {
    public class RingBuffer<T> where T : class {
        public const int RING_BUFF_CONSECUTIVE_SET = 0;
        public const int RING_BUFF_NON_CONSECUTIVE_SET = 1;
        public const int RING_BUFF_FAILED_TO_SET = 2;
        public int Ed;        // write index, open index
        public int St;        // read index, closed index
        public int N;
        public int Cnt;       // the count of valid elements in the buffer, used mainly to distinguish what "st == ed" means for "Pop" and "Get" methods
        protected T[] Eles;
        public RingBuffer(int n) {
            Cnt = St = Ed = 0;
            N = n;
            Eles = new T[n];
        }

        public virtual bool Put(T item) {
            while (0 < Cnt && Cnt >= N) {
                // Make room for the new element
                Pop();
            }
            Eles[Ed] = item;
            Cnt++;
            Ed++;

            if (Ed >= N) {
                Ed -= N; // Deliberately not using "%" operator for performance concern

            }
            return true;
        }

        public virtual (bool, T?) Pop() {
            if (0 == Cnt) {
                return (false, default(T));
            }
            T holder = Eles[St];
            Cnt--; St++;

            if (St >= N) {
                St -= N;
            }
            return (true, holder);
        }

        public int GetArrIdxByOffset(int offsetFromSt) {
            if (0 == Cnt || 0 > offsetFromSt) {
                return -1;
            }
            int arrIdx = St + offsetFromSt;

            if (St < Ed) {
                // case#1: 0...st...ed...N-1
                if (St <= arrIdx && arrIdx < Ed) {
                    return arrIdx;
                }
            } else {
                // if St >= Ed
                // case#2: 0...ed...st...N-1
                if (arrIdx >= N) {
                    arrIdx -= N;

                }
                if (arrIdx >= St || arrIdx < Ed) {
                    return arrIdx;

                }
            }

            return -1;
        }

        public (bool, T?) GetByOffset(int offsetFromSt) {
            int arrIdx = GetArrIdxByOffset(offsetFromSt);

            if (-1 == arrIdx) {
                return (false, default(T));
            }

            if (0 > arrIdx || arrIdx >= N) {
                throw new ArgumentException(String.Format("arrIdx={0} is out of bound! Cnt={1}, N={2}, St={3}, Ed={4}, offsetFromSt={5}", arrIdx, Cnt, N, St, Ed, offsetFromSt));
            }
            return (true, Eles[arrIdx]);
        }

        public void Clear() {
            while (0 < Cnt) {
                Pop();
            }
            St = Ed = 0;
        }
    }
}
