using System.Transactions;

namespace shared {
    public class FrameRingBuffer<T> : RingBuffer<T> where T : class {
        int EdFrameId;
        int StFrameId;
        public FrameRingBuffer(int n) : base(n) {
            StFrameId = EdFrameId = 0;
        }

        public new bool Put(T item) {
            bool ret = base.Put(item);
            EdFrameId++;
            return ret;
        }

        public new (bool, T?) Pop() {
            var (retBool, retHolder) = base.Pop();
            if (retBool) {
                StFrameId++;
            }
            return (retBool, retHolder);
        }

        public void DryPut() {
            while (0 < Cnt && Cnt >= N) {
                // Make room for the new element
                Pop();
            }
            EdFrameId++;
            Cnt++;
            Ed++;

            if (Ed >= N) {
                Ed -= N; // Deliberately not using "%" operator for performance concern
            }
        }

        public (bool, T?) GetByFrameId(int frameId) {
            if (frameId >= EdFrameId || frameId < StFrameId) {
                return (false, default(T));
            }
            return GetByOffset(frameId - StFrameId);
        }

        // [WARNING] During a battle, frontend could receive non-consecutive frames (either renderFrame or inputFrame) due to resync, the buffer should handle these frames properly.
        public (int, int, int) SetByFrameId(T item, int frameId) {
            int oldStFrameId = StFrameId;
            int oldEdFrameId = EdFrameId;

            if (frameId < oldStFrameId) {
                return (RING_BUFF_FAILED_TO_SET, oldStFrameId, oldEdFrameId);
            }
            // By now "StFrameId <= frameId"
            if (oldEdFrameId > frameId) {
                int arrIdx = GetArrIdxByOffset(frameId - StFrameId);

                if (-1 != arrIdx) {
                    Eles[arrIdx] = item;
                    return (RING_BUFF_CONSECUTIVE_SET, oldStFrameId, oldEdFrameId);
                }
            }

            // By now "EdFrameId <= frameId"
            int ret = RING_BUFF_CONSECUTIVE_SET;

            if (oldEdFrameId < frameId) {
                St = Ed = 0;
                StFrameId = EdFrameId = frameId;
                Cnt = 0;
                ret = RING_BUFF_NON_CONSECUTIVE_SET;
            }

            // By now "EdFrameId == frameId"
            Put(item);

            return (ret, oldStFrameId, oldEdFrameId);
        }
        public new void Clear() {
            base.Clear();
            StFrameId = EdFrameId = 0;
        }
    }
}
