using Google.Protobuf.Collections;

namespace shared {
    public partial class Battle {
        public static bool IsPickableAlive(Pickable pickable, int currRenderFrameId) {
            return (0 < pickable.RemainingLifetimeRdfCount);
        }

        private static void _moveAndInsertPickableColliders(RoomDownsyncFrame currRenderFrame, int roomCapacity, RepeatedField<Pickable> nextRenderFramePickables, CollisionSpace collisionSys, Collider[] dynamicRectangleColliders, Vector[] effPushbacks, ref int colliderCnt, ref int pickableCnt, ILoggerBridge logger) {
            var currRenderFramePickables = currRenderFrame.Pickables;
            for (int i = 0; i < currRenderFramePickables.Count; i++) {
                var src = currRenderFramePickables[i];
                if (TERMINATING_PICKABLE_LOCAL_ID == src.PickableLocalId) break;

                var dst = nextRenderFramePickables[pickableCnt];

                int remainingLifetimeRdfCount = src.RemainingLifetimeRdfCount - 1;
                var srcConfigFromTile = src.ConfigFromTiled;
                AssignToPickable(src.PickableLocalId, src.VirtualGridX, src.VirtualGridY, src.VelY, remainingLifetimeRdfCount, src.RemainingRecurQuota, src.PkState, src.FramesInPkState + 1, src.PickedByJoinIndex, srcConfigFromTile.InitVirtualGridX, srcConfigFromTile.InitVirtualGridY, srcConfigFromTile.TakesGravity, srcConfigFromTile.FirstShowRdfId, srcConfigFromTile.RecurQuota, srcConfigFromTile.RecurIntervalRdfCount, srcConfigFromTile.LifetimeRdfCountPerOccurrence, srcConfigFromTile.PickupType, srcConfigFromTile.StockQuotaPerOccurrence, srcConfigFromTile.SubscriptionId, srcConfigFromTile.ConsumableSpeciesId, srcConfigFromTile.BuffSpeciesId, dst);

                if (!IsPickableAlive(dst, currRenderFrame.Id)) {
                    continue;
                }

                pickableCnt++;

                int newVx = src.VirtualGridX, newVy = src.VirtualGridY + src.VelY;
                var dstVelY = src.VelY + (src.ConfigFromTiled.TakesGravity ? GRAVITY_Y : 0);
                if (dstVelY < DEFAULT_MIN_FALLING_VEL_Y_VIRTUAL_GRID) {
                    dstVelY = DEFAULT_MIN_FALLING_VEL_Y_VIRTUAL_GRID;
                }
                var (cx, cy) = VirtualGridToPolygonColliderCtr(newVx, newVy);
                var (hitboxSizeCx, hitboxSizeCy) = VirtualGridToPolygonColliderCtr(DEFAULT_PICKABLE_HITBOX_SIZE_X, DEFAULT_PICKABLE_HITBOX_SIZE_Y);
                var newCollider = dynamicRectangleColliders[colliderCnt];
                UpdateRectCollider(newCollider, cx, cy, hitboxSizeCx, hitboxSizeCy, SNAP_INTO_PLATFORM_OVERLAP, SNAP_INTO_PLATFORM_OVERLAP, SNAP_INTO_PLATFORM_OVERLAP, SNAP_INTO_PLATFORM_OVERLAP, 0, 0, dst);

                effPushbacks[colliderCnt].X = 0;
                effPushbacks[colliderCnt].Y = 0;
                colliderCnt++;

                collisionSys.AddSingle(newCollider);
                dst.VirtualGridX = newVx;
                dst.VirtualGridY = newVy;
                dst.VelY = dstVelY;    
            }
        }
    }
}
