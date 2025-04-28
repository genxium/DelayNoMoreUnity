- When not null, a "CollisionSpace" instance holds memory of at least 1 cell even if "0 == spaceWidth and 0 == spaceHeight", where each cell holds memory of "FrameRingBuffer<Collider> Colliders(128)".
- When not null, a "Collision" instance holds memory of a "FrameRingBuffer<Collider> ContactedColliders(128)".

To reduce unnecessary memory holding by idle rooms, I plan to try proactively calling "CollisionSpace.RemoveAll()" and "Collision.ClearDeep()" in "Room()" constructor without touching the constructors of "CollisionSpace" or "Collision", because the implicit memory allocation in the latter makes "Battle_builder.refreshColliders" for a new battle quite convenient.
