using System;

namespace shared {
    public class Collision {
        public float dx, dy;
        public Collider? checkingCollider;
        public FrameRingBuffer<Collider> ContactedColliders;
        public Collision() {
            dx = dy = 0;
            checkingCollider = null; 
            ContactedColliders = new FrameRingBuffer<Collider>(128); // I don't expect it to exceed 64 actually
        }

        public (bool, Collider?) PopFirstContactedCollider() {
            return ContactedColliders.Pop();
        }

        public bool HasSeen(Collider candidate) {
            // Deliberately using traversal instead of HashSet<Collider> because I assume that in small collections traversal of continuous RAM is faster. 
            for (int i = ContactedColliders.StFrameId; i < ContactedColliders.EdFrameId; i++) {
                var (ok, collider) = ContactedColliders.GetByFrameId(i);  
                if (ok && collider == candidate) {
                    return true;
                }
            }
            return false;
        }

        public void Clear() {
            dx = dy = 0;
            checkingCollider = null; 
            ContactedColliders.Clear();
        }

        public void ClearDeep() {
            dx = dy = 0;
            if (null != checkingCollider) {
                checkingCollider.clearTouchingCellsAndData();
            }
            checkingCollider = null; 
            while (0 < ContactedColliders.Cnt) {
                var (ok, c) = ContactedColliders.Pop(); 
                if (ok && null != c) {
                    c.clearTouchingCellsAndData();
                }
            } 
        }
    }
}
