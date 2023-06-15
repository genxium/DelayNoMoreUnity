using System;

namespace shared {
    // [WARNING] It's non-trivial to declare "Vector" as a "struct" due to the generic type constraint imposed on "RingBuffer<T>"; moreover it's also unnecessary to save heap RAM allocation this way, as shown below the "ConvexPolygon" should hold retained "Points" in heap RAM anyway, and the "ConvexPolygon instances" themselves are reused by the "Collider instances" which are also reused in battle.
    public class Vector {
        public float X, Y;
        public Vector(float x, float y) {
            X = x;
            Y = y;
        }
    }

    public class ConvexPolygon {
        public FrameRingBuffer<Vector> Points;
        public float X, Y; // The anchor position coordinates
        public bool Closed;

        public ConvexPolygon(float x, float y, float[] points) {
            X = x;
			Y = y;
            Points = new FrameRingBuffer<Vector>(6); // I don't expected more points to be coped with in this particular game
            for (int i = 0; i < points.GetLength(0); i += 2) {
                Vector v = new Vector(points[i], points[i + 1]);
                Points.Put(v);
            }
            Closed = true;
        }

        public Vector? GetPointByOffset(int offset) {
            if (Points.Cnt <= offset) {
                return null;
            }
            var (_, v) = Points.GetByFrameId(Points.StFrameId + offset);
            return v;
        }

        public void SetPosition(float x, float y) {
            X = x;
            Y = y;
        }

        public bool UpdateAsRectangle(float x, float y, float w, float h) {
            // This function might look ugly but it's a fast in-place update!
            if (4 != Points.Cnt) {
                throw new ArgumentException("ConvexPolygon not having exactly 4 vertices to form a rectangle#1!");
            }
            for (int i = 0; i < Points.Cnt; i++) {
                Vector? thatVec = GetPointByOffset(i);
                if (null == thatVec) {
                    throw new ArgumentException("ConvexPolygon not having exactly 4 vertices to form a rectangle#2!");
                }
                switch (i) {
                    case 0:
                        thatVec.X = x;
                        thatVec.Y = y;
                        break;
                    case 1:
                        thatVec.X = x + w;
                        thatVec.Y = y;
                        break;
                    case 2:
                        thatVec.X = x + w;
                        thatVec.Y = y + h;
                        break;
                    case 3:
                        thatVec.X = x;
                        thatVec.Y = y + h;
                        break;
                }
            }
            return true;
        }

        public string ToString(bool anchorMode) {
            if (anchorMode) {
                var s = String.Format("[anchorX:{0}, anchorY:{1}; ", X, Y);
                for (int i = Points.StFrameId; i < Points.EdFrameId; i++) {
                    var p = GetPointByOffset(i);
                    if (null == p) throw new ArgumentNullException(String.Format("i={0} got a null point", i)); 
                    s += String.Format("({0}, {1})", p.X, p.Y); 
                    if (i == Points.EdFrameId-1) s += "]"; 
                    else s += ", ";
                }

                return s;
            } else {
                var s = String.Format("[");
                for (int i = Points.StFrameId; i < Points.EdFrameId; i++) {
                    var p = GetPointByOffset(i);
                    if (null == p) throw new ArgumentNullException(String.Format("i={0} got a null point", i)); 
                    s += String.Format("({0}, {1})", X+p.X, Y+p.Y); 
                    if (i == Points.EdFrameId-1) s += "]"; 
                    else s += ", ";
                }

                return s;
            }
        }
    }

    public struct SatResult {
        public float OverlapMag, OverlapX, OverlapY;
        public bool AContainedInB, BContainedInA;

        // [WARNING] Deliberately unboxed "Vector" to make the following fields primitive such that the whole "SatResult" will be easily allocated on stack.
        public float AxisX, AxisY;

        public void cloneInto(ref SatResult dist) {
            dist.OverlapMag = this.OverlapMag;
            dist.OverlapX = this.OverlapX;
            dist.OverlapY = this.OverlapY;  
            dist.AContainedInB = this.AContainedInB;
            dist.BContainedInA = this.BContainedInA;
            dist.AxisX = this.AxisX;
            dist.AxisY = this.AxisY;    
        }

        public string ToString() {
            return String.Format("(Mag:{0}, OverlapX:{1}, OverlapY:{2}, AContainedInB:{3}, BContainedInA:{4})", OverlapMag, OverlapX, OverlapY, AContainedInB, BContainedInA);
        }
    }
}
