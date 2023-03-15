using System;

namespace shared {
    public class Collision {
        public double dx, dy;
        public Collider? checkingCollider;
        public FrameRingBuffer<Collider> ContactedColliders;
        public Collision() {
            dx = dy = 0;
            checkingCollider = null; 
            ContactedColliders = new FrameRingBuffer<Collider>(16); // I don't expect it to exceed 10 actually
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
    }
}
