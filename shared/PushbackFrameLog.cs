using System;
using System.Collections.Generic;

namespace shared {

    public class PushbackFrameLog {
        // 1 instance of this class represents the pushback collections of 1 CharacterDownsync in 1 RoomDownsyncFrame
        public int PrimaryHardPushbackIndex, PrimarySoftPushbackIndex;
        public FrameRingBuffer<Vector> HardPushbacks; // Not yet normalized, thus having the magnitude info
        public FrameRingBuffer<Vector> SoftPushbacks; // Not yet normalized, thus having the magnitude info
        public FrameRingBuffer<Vector> TouchingCells; 
        public int TotOtherChCnt, CellOverlappedOtherChCnt, ShapeOverlappedOtherChCnt, OrigResidueCollidedSt, OrigResidueCollidedEd;

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

            TouchingCells = new FrameRingBuffer<Vector>(9); // I just know that this is enough for a...
            for (int i = 0; i < TouchingCells.N; i++) {
                TouchingCells.Put(new Vector(0, 0));
            }
            TouchingCells.Clear(); // Then use it by "DryPut"
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

        public void setSoftPushback(int primarySoftPushbackIndex, Vector[] softPushbacks, int softPushbacksCnt, int totOtherChCnt, int cellOverlappedOtherChCnt, int shapeOverlappedOtherChCnt, int origResidueCollidedSt, int origResidueCollidedEd) {
            SoftPushbacks.Clear();
            TotOtherChCnt = totOtherChCnt;
            CellOverlappedOtherChCnt = cellOverlappedOtherChCnt;
            ShapeOverlappedOtherChCnt = shapeOverlappedOtherChCnt;
            OrigResidueCollidedSt = origResidueCollidedSt;
            OrigResidueCollidedEd = origResidueCollidedEd;
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

        public void setTouchingCells(Collider aCollider) {
            TouchingCells.Clear();
            var rb = aCollider.TouchingCells;
			for (int i = rb.StFrameId, j = 0; i < rb.EdFrameId; i++) {
				var (ok1, cell) = rb.GetByFrameId(i);
				if (!ok1 || null == cell) {
					continue;
				}

                var (ok2, candidate) = TouchingCells.GetByFrameId(j);
                if (!ok2 || null == candidate) {
                    if (j == TouchingCells.EdFrameId) {
                        TouchingCells.DryPut();
                        (_, candidate) = TouchingCells.GetByFrameId(j);
                    }
                }
                if (null == candidate) {
                    throw new ArgumentNullException(String.Format("TouchingCells was not fully pre-allocated for j={0}!", j));
                }
                j++;
                candidate.X = (float)cell.X; 
                candidate.Y = (float)cell.Y;
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
            if (0 < TouchingCells.Cnt) {
                sb.Add("    t:{{ " + Vector.VectorFrameRingBufferToString(TouchingCells) + " }}");
            }
            sb.Add(String.Format("    c0:{0},c1:{1},c2:{2},st:{3},ed:{4}", TotOtherChCnt, CellOverlappedOtherChCnt, ShapeOverlappedOtherChCnt, OrigResidueCollidedSt, OrigResidueCollidedEd));

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

        public void setHardPushbacksByJoinIndex(int joinIndex, int primaryIndex, Vector[] pushbacks, int pushbacksCnt) {
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
            candidate.setHardPushback(primaryIndex, pushbacks, pushbacksCnt);
        }

        public void setSoftPushbacksByJoinIndex(int joinIndex, int primaryIndex, Vector[] pushbacks, int pushbacksCnt, int totOtherChCnt, int cellOverlappedOtherChCnt, int shapeOverlappedOtherChCnt, int origResidueCollidedSt, int origResidueCollidedEd) {
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
            candidate.setSoftPushback(primaryIndex, pushbacks, pushbacksCnt, totOtherChCnt, cellOverlappedOtherChCnt, shapeOverlappedOtherChCnt, origResidueCollidedSt, origResidueCollidedEd);
        }

        public void setTouchingCellsByJoinIndex(int joinIndex, Collider aCollider) {
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
            candidate.setTouchingCells(aCollider);
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
