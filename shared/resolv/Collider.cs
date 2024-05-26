using System;
using System.Text;
using System.Collections.Generic;

namespace shared {
    public class Collider {
        public float X, Y, W, H;      // Position and size of the Collider in the Space
        public int maxTouchingCellsCnt;
        public FrameRingBuffer<CollisionCell> TouchingCells; // An array of Cells the Collider is touching
        public ConvexPolygon Shape;
        public ulong Mask;
        public object? Data;                     // A pointer to a user-definable object

        public CollisionSpace? Space;           // Reference to the Space the Collider exists within

        public Collider(float x, float y, float w, float h, ConvexPolygon shape, int aMaxTouchingCellsCnt, object? data, ulong mask) {
            X = x;
            Y = y;
            W = w;
            H = h;
            TouchingCells = new FrameRingBuffer<CollisionCell>(aMaxTouchingCellsCnt); // [WARNING] Should make N large enough to cover all "TouchingCells", otherwise some cells would fail to unregister a collider, resulting in memory corruption and incorrect detection result!
            Shape = shape;
            Data = data;
            Space = null;
            Mask = mask;
        }

        public (int, int, int, int) BoundsToSpace(float dx, float dy) {
            if (null == Space) {
                throw new ArgumentException("Collider Space is null when calling `BoundsToSpace`!");
            }
            var (cx, cy) = Space.WorldToSpace(X + dx, Y + dy);
            var (ex, ey) = Space.WorldToSpace(X + W + dx, Y + H + dy);
            return (cx, cy, ex, ey);
        }

        public bool CheckAllWithHolder(float dx, float dy, Collision cc, HashSet<ulong>? collidablePairs) {
            if (null == Space) {
                return false;
            }
            cc.Clear();
            cc.checkingCollider = this;

            if (dx < 0) {
                dx = Math.Min(dx, -1);
            } else if (dx > 0) {
                dx = Math.Max(dx, 1);
            }

            if (dy < 0) {
                dy = Math.Min(dy, -1);
            } else if (dy > 0) {
                dy = Math.Max(dy, 1);
            }

            cc.dx = dx;
            cc.dy = dy;

            var (cx, cy, ex, ey) = BoundsToSpace(dx, dy);

            for (int y = cy; y <= ey; y++) {
                for (int x = cx; x <= ex; x++) {
                    var c = Space.GetCell(x, y);
                    if (null == c) continue;
                    var rb = c.Colliders;
                    for (int i = rb.StFrameId; i < rb.EdFrameId; i++) {
                        var (ok, o) = rb.GetByFrameId(i);
                        if (!ok || null == o) {
                            continue;
                        }
                        // We only want cells that have objects other than the checking object, or that aren't on the ignore list.
                        if (o == this) {
                            continue;
                        }

                        if (null != collidablePairs) {
                            if (!collidablePairs.Contains(this.Mask | o.Mask)) {
                               continue; 
                            }
                        }

                        if (cc.HasSeen(o)) {
                            continue;
                        }
                        if (cc.ContactedColliders.Cnt >= cc.ContactedColliders.N) {
                            throw new ArgumentException(String.Format("cc.ContactedColliders is already full! for cell at X={0}, Y={1}, cc.ContactedColliders.Cnt={2}, cc.ContactedColliders.N={3}: trying to insert collider.Shape={4}, collider.Data={5}", X, Y, cc.ContactedColliders.Cnt, cc.ContactedColliders.N, o.Shape, o.Data));
                        }
                        cc.ContactedColliders.Put(o);
                    }
                }
            }

            if (0 >= cc.ContactedColliders.Cnt) {
                return false;
            }

            if (0 < cc.ContactedColliders.StFrameId) {
                throw new Exception("FrameRingBuffer collision.ContactedColliders is overloaded!");
            }

            return true;
        }

        public String TouchingCellsStr() {
            var rb = this.TouchingCells;
            var sb = new StringBuilder();
            for (int i = rb.StFrameId; i < rb.EdFrameId; i++) {
                var (ok, cell) = rb.GetByFrameId(i);
                if (!ok || null == cell) {
                    continue;
                }
                sb.AppendFormat("(X:{0}, Y:{1}) ", cell.X, cell.Y);
            }

            return sb.ToString();
        }

        public String TouchingCellsStaticColliderStr() {
            var rb = this.TouchingCells;
            var sb = new StringBuilder();
            for (int i = rb.StFrameId; i < rb.EdFrameId; i++) {
                var (ok, cell) = rb.GetByFrameId(i);
                if (!ok || null == cell) {
                    continue;
                }
                sb.AppendFormat("{{ {0} }}\n", cell.toStaticColliderShapeStr());
            }

            return sb.ToString();
        }

        public void clearTouchingCellsAndData() {
            Space = null;
            Data = null;
            TouchingCells = new FrameRingBuffer<CollisionCell>(0); // To dereference existing Cells
        }
    }	
}
