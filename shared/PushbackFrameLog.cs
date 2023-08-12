using System;
using System.Collections.Generic;

namespace shared {
    public class PushbackFrameLog {
        // 1 instance of this class represents the pushback collections of 1 CharacterDownsync in 1 RoomDownsyncFrame
        public int PrimaryHardPushbackIndex, PrimarySoftPushbackIndex;
        public FrameRingBuffer<Vector> HardPushbacks; // Not yet normalized, thus having the magnitude info
        public FrameRingBuffer<Vector> SoftPushbacks; // Not yet normalized, thus having the magnitude info

        public PushbackFrameLog(int hardOrSoftPushbacksCntUpper) {
            HardPushbacks = new FrameRingBuffer<Vector>(hardOrSoftPushbacksCntUpper);
            for (int i = 0; i < hardOrSoftPushbacksCntUpper; i++) {
                HardPushbacks.Put(new Vector(0, 0));
            }
            HardPushbacks.Clear(); // Then use it by "DryPut"

            SoftPushbacks = new FrameRingBuffer<Vector>(hardOrSoftPushbacksCntUpper);
            for (int i = 0; i < hardOrSoftPushbacksCntUpper; i++) {
                SoftPushbacks.Put(new Vector(0, 0));
            }
            SoftPushbacks.Clear(); // Then use it by "DryPut"
        }

        public void setHardPushback(int primaryHardPushbackIndex, Vector[] hardPushbacks, int hardPushbacksCnt) {
            HardPushbacks.Clear();
            PrimaryHardPushbackIndex = primaryHardPushbackIndex;
            for (int i = 0; i < hardPushbacksCnt; i++) {
                var (ok, candidate) = HardPushbacks.GetByFrameId(i);
                if (!ok || null == candidate) {
                    if (i == HardPushbacks.EdFrameId) {
                        HardPushbacks.DryPut();
                        (_, candidate) = HardPushbacks.GetByFrameId(i);
                    }
                }
                if (null == candidate) {
                    throw new ArgumentNullException(String.Format("HardPushbacks was not fully pre-allocated for i={0}!", i));
                }

                candidate.X = hardPushbacks[i].X;
                candidate.Y = hardPushbacks[i].Y;
            }
        }

        public void setSoftPushback(int primarySoftPushbackIndex, Vector[] softPushbacks, int softPushbacksCnt) {
            SoftPushbacks.Clear();
            PrimarySoftPushbackIndex = primarySoftPushbackIndex;
            for (int i = 0; i < softPushbacksCnt; i++) {
                var (ok, candidate) = SoftPushbacks.GetByFrameId(i);
                if (!ok || null == candidate) {
                    if (i == SoftPushbacks.EdFrameId) {
                        SoftPushbacks.DryPut();
                        (_, candidate) = SoftPushbacks.GetByFrameId(i);
                    }
                }
                if (null == candidate) {
                    throw new ArgumentNullException(String.Format("SoftPushbacks was not fully pre-allocated for i={0}!", i));
                }

                candidate.X = softPushbacks[i].X;
                candidate.Y = softPushbacks[i].Y;
            }
        }

        public string toString() {
            var sb = new List<string>();
            if (-1 != PrimaryHardPushbackIndex) {
                sb.Add("    h:{{ " + Vector.VectorFrameRingBufferToString(HardPushbacks) + ", pi:" + PrimaryHardPushbackIndex + " }}");
            } 
            if (-1 != PrimarySoftPushbackIndex) {
                sb.Add("    s:{{ " + Vector.VectorFrameRingBufferToString(SoftPushbacks) + ", pi:" + PrimarySoftPushbackIndex + " }}");
            }

            return String.Join('\n', sb);
        }
    }

    public class RdfPushbackFrameLog {
        // 1 instance of this class represents the pushback collections of 1 RoomDownsyncFrame
        public int RdfId;
        public FrameRingBuffer<PushbackFrameLog> CharacterPushbackFrameLogs; // indexed by "joinIndex-1"

        public RdfPushbackFrameLog(int rdfId, int characterCnt, int hardOrSoftPushbacksCntUpper) {
            RdfId = rdfId;
            CharacterPushbackFrameLogs = new FrameRingBuffer<PushbackFrameLog>(characterCnt);
            for (int i = 0; i < characterCnt; i++) {
                CharacterPushbackFrameLogs.Put(new PushbackFrameLog(hardOrSoftPushbacksCntUpper));
            }
            CharacterPushbackFrameLogs.Clear(); // Then use it by "DryPut"
        }

        public void setByJoinIndex(int joinIndex, int primaryIndex, Vector[] pushbacks, int pushbacksCnt, bool isHardPushback) {
            // [WARNING] The "DryPut" usage here shouldn't be generalized, because we know that "joinIndex" will be traversed in a "consecutively increasing" manner.
            int i = joinIndex-1;
            var (ok, candidate) = CharacterPushbackFrameLogs.GetByFrameId(i);
            if (!ok || null == candidate) {
                if (i == CharacterPushbackFrameLogs.EdFrameId) {
                    CharacterPushbackFrameLogs.DryPut();
                    (_, candidate) = CharacterPushbackFrameLogs.GetByFrameId(i);
                }
            }
            if (null == candidate) {
                throw new ArgumentNullException(String.Format("RdfId={0}, CharacterPushbackFrameLogs was not fully pre-allocated for joinIndex={1} a.k.a. i={2}!", RdfId, joinIndex, i));
            }
            if (isHardPushback) {
                candidate.setHardPushback(primaryIndex, pushbacks, pushbacksCnt);
            } else {
                candidate.setSoftPushback(primaryIndex, pushbacks, pushbacksCnt);
            }
        }

        public string toString() {
            var chSb = new List<String>();
            for (int k = CharacterPushbackFrameLogs.StFrameId; k < CharacterPushbackFrameLogs.EdFrameId; k++) {
                var (ok, single) = CharacterPushbackFrameLogs.GetByFrameId(k);
                if (!ok || null == single) throw new Exception(String.Format("RdfId={0}, CharacterPushbackFrameLogs doesn't have k={1} properly set! N={2}, Cnt={3}, StFrameId={4}, EdFrameId={5}", RdfId, k, CharacterPushbackFrameLogs.N, CharacterPushbackFrameLogs.Cnt, CharacterPushbackFrameLogs.StFrameId, CharacterPushbackFrameLogs.EdFrameId));
                int joinIndex = k+1;
                if (-1 != single.PrimaryHardPushbackIndex || -1 != single.PrimarySoftPushbackIndex) {
                    chSb.Add("j:" + joinIndex + " {{\n" + single.toString() + "\n}}");
                }
            }
            return String.Format("{{ \nrdfId:{0}\n{1} \n}}", RdfId, String.Join('\n', chSb));
        }
    }
}
