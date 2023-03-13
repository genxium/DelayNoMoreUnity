using System;

namespace shared {
    public class CollisionCell {
        int X, Y;         // The X and Y position of the cell in the Space - note that this is in Grid position, not World position.
        FrameRingBuffer<Collider> Colliders; // The colliders that a CollisionCell contains. Using "FrameRingBuffer" here for the ease of traversal

        public CollisionCell(int x, int y) {
            X = x;
            Y = y;
            Colliders = new FrameRingBuffer<Collider>(16); // A single cell is so small thus wouldn't have many touching colliders simultaneously
        }
        public bool Contains(Collider collider) {
            for (int i = Colliders.StFrameId; i < Colliders.EdFrameId; i++) {
                var (_, o) = Colliders.GetByFrameId(i);
                if (o == collider) {
                    return true;
                }
            }
            return false;
        }

        public void register(Collider collider) {
            if (!Contains(collider)) {
                Colliders.Put(collider);
            }
        }

        public void unregister(Collider collider) {
            for (int i = Colliders.StFrameId; i < Colliders.EdFrameId; i++) {
                var (_, o) = Colliders.GetByFrameId(i);
                if (o == collider) {
                    // swap with the st element
                    var (_, curStEle) = Colliders.GetByFrameId(Colliders.StFrameId);
                    if (null != curStEle) {
                        Colliders.SetByFrameId(curStEle, i);
                        // pop the current st element
                        Colliders.Pop();
                        break;
                    } else {
                        String msg = String.Format("Unexpected null element for FrameRingBuffer `CollisionCell.Colliders` at i={0}", i);
                        throw new ArgumentException(msg);
                    }
                }
            }
        }

        public bool Occupied() {
            return 0 < Colliders.Cnt;
        }
    }
}
