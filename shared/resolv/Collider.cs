using System;

namespace shared {
    public class Collider {
        public double X, Y, W, H;      // Position and size of the Collider in the Space
        public FrameRingBuffer<CollisionCell> TouchingCells; // An array of Cells the Collider is touching
        public CollisionSpace? Space;           // Reference to the Space the Collider exists within

        public object? Data;          // A pointer to a user-definable object

        public Collider(double x, double y, double w, double h) {
            X = x;
            Y = y;
            W = w;
            H = h;
            TouchingCells = new FrameRingBuffer<CollisionCell>(512); // [WARNING] Should make N large enough to cover all "TouchingCells", otherwise some cells would fail to unregister a collider, resulting in memory corruption and incorrect detection result!
            Data = null;
            Space = null;
        }

        public (int, int, int, int) BoundsToSpace(double dx, double dy) {
			if (null == Space) {
				throw new ArgumentException("Collider Space is null when calling `BoundsToSpace`!");
			}
            var (cx, cy) = Space.WorldToSpace(X + dx, Y + dy);
            var (ex, ey) = Space.WorldToSpace(X + W + dx - 1, Y + H + dy - 1);
            return (cx, cy, ex, ey);
        }
        public void Update() {
            if (null != Space) {
                var heldSpace = Space; // Holds the space
                Space.RemoveSingle(this); // Would set "this.Space = null"
                Space = heldSpace; // Re-assign the held space
                var (cx, cy, ex, ey) = BoundsToSpace(0, 0);
                for (int y = cy; y <= ey; y++) {
                    for (int x = cx; x <= ex; x++) {
                        var c = Space.GetCell(x, y);
                        if (null != c) {
                            c.register(this);
                            TouchingCells.Put(c);
                        }
                    }

                }
            }

            /*
			if null != Shape {
				Shape.SetPosition(X, Y);
			}
			*/
        }
    }
}
