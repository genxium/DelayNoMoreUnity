using System;

namespace shared {
    public class PacketStat {
        public FrameRingBuffer<PacketStatQEle> Q;

        public int SmallestI, LargestJ;
        public long TimeAtSmallestI, TimeAtLargestJ;

        public PacketStat(int bufferSize) {
            SmallestI = Battle.MAX_INT;
            LargestJ = -Battle.MAX_INT;
            TimeAtSmallestI = 0;
            TimeAtLargestJ = 0;
            Q = new FrameRingBuffer<PacketStatQEle>(bufferSize);
            for (int i = 0; i < bufferSize; i++) {
                Q.Put(new PacketStatQEle {
                    i = 0, j = 0, t = 0
                });
            }
        }

        public void Reset() {
            SmallestI = Battle.MAX_INT;
            LargestJ = -Battle.MAX_INT;

            Q.Clear(); // then use by "DryPut()"
        }

        public void LogInterval(int i, int j) {
            if (i > j) return; 
            int oldEd = Q.EdFrameId;
            Q.DryPut();
            var (ok, holder) = Q.GetByFrameId(oldEd);
            if (!ok || null == holder) {
                return;
            }
            holder.i = i;
            holder.j = j;
            holder.t = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            if (i < SmallestI) {
                SmallestI = i;
                TimeAtSmallestI = holder.t; 
            }
            if (j > LargestJ) {
                LargestJ = j;
                TimeAtLargestJ = holder.t; 
            }
        }
    }
}
