using System;

namespace shared {
    // [WARNING] It's non-trivial to declare "Vector" as a "struct" due to the generic type constraint imposed on "RingBuffer<T>"; moreover it's also unnecessary to save heap RAM allocation this way, as shown below the "ConvexPolygon" should hold retained "Points" in heap RAM anyway, and the "ConvexPolygon instances" themselves are reused by the "Collider instances" which are also reused in battle.
    public class Vector {
        public double X, Y;
        public Vector(double x, double y) {
            X = x;
            Y = y;
        }
    }

    public class ConvexPolygon {
        public FrameRingBuffer<Vector> Points;
        public double X, Y; // The anchor position coordinates
        public bool Closed;

        public ConvexPolygon(double[] points) {
            X = Y = 0; // [WARNING] AFAIK these anchor position coordinates are ALWAYS 0 during the original version of DelayNoMore -- the changes are only made on "Points" even for indicating movements
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
    }

    public struct SatResult {
        public double OverlapMag, OverlapX, OverlapY;
        public bool AContainedInB, BContainedInA;
        
        // [WARNING] Deliberately unboxed "Vector" to make the following fields primitive such that the whole "SatResult" will be easily allocated on stack.
        public double AxisX, AxisY;
    }
}
