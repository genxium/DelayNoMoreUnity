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
                AssignToPickable(src.PickableLocalId, src.VirtualGridX, src.VirtualGridY, src.VelY, remainingLifetimeRdfCount, src.RemainingRecurQuota, src.PkState, src.FramesInPkState + 1, src.PickedByJoinIndex, srcConfigFromTile.InitVirtualGridX, srcConfigFromTile.InitVirtualGridY, srcConfigFromTile.TakesGravity, srcConfigFromTile.FirstShowRdfId, srcConfigFromTile.RecurQuota, srcConfigFromTile.RecurIntervalRdfCount, srcConfigFromTile.LifetimeRdfCountPerOccurrence, srcConfigFromTile.PickupType, srcConfigFromTile.StockQuotaPerOccurrence, srcConfigFromTile.SubscriptionId, srcConfigFromTile.ConsumableSpeciesId, srcConfigFromTile.BuffSpeciesId, srcConfigFromTile.SkillId, dst);

                if (!IsPickableAlive(dst, currRenderFrame.Id)) {
                    continue;
                }

                pickableCnt++;

                int newVx = src.VirtualGridX, newVy = src.VirtualGridY + src.VelY;
                var dstVelY = src.VelY + (src.ConfigFromTiled.TakesGravity ? GRAVITY_Y : 0);
                if (dstVelY < DEFAULT_MIN_FALLING_VEL_Y_VIRTUAL_GRID) {
                    dstVelY = DEFAULT_MIN_FALLING_VEL_Y_VIRTUAL_GRID;
                }
                
                if (dst.FramesInPkState >= DEFAULT_PICKABLE_NONCOLLIDING_FRAMES) {
                    var (cx, cy) = VirtualGridToPolygonColliderCtr(newVx, newVy);
                    var (hitboxSizeCx, hitboxSizeCy) = VirtualGridToPolygonColliderCtr(DEFAULT_PICKABLE_HITBOX_SIZE_X, DEFAULT_PICKABLE_HITBOX_SIZE_Y);
                    var newCollider = dynamicRectangleColliders[colliderCnt];
                    UpdateRectCollider(newCollider, cx, cy, hitboxSizeCx, hitboxSizeCy, SNAP_INTO_PLATFORM_OVERLAP, SNAP_INTO_PLATFORM_OVERLAP, SNAP_INTO_PLATFORM_OVERLAP, SNAP_INTO_PLATFORM_OVERLAP, 0, 0, dst, COLLISION_PICKABLE_INDEX_PREFIX);

                    collisionSys.AddSingleToCellTail(newCollider);
                } 
                
                // [WARNING] Always initialize "effPushbacks[colliderCnt]" and increment "colliderCnt" for convenient index alignment!  
                effPushbacks[colliderCnt].X = 0;
                effPushbacks[colliderCnt].Y = 0;
                colliderCnt++;

                dst.VirtualGridX = newVx;
                dst.VirtualGridY = newVy;
                dst.VelY = dstVelY;    
            }

            // Explicitly specify termination of nextRenderFramePickables
            if (pickableCnt < nextRenderFramePickables.Count) nextRenderFramePickables[pickableCnt].PickableLocalId = TERMINATING_PICKABLE_LOCAL_ID;
        }

        protected static bool addNewPickableToNextFrame(int rdfId, int virtualGridX, int virtualGridY, int remainingLifetimeRdfCount, int recurQuota, bool takesGravity, uint recurIntervalRdfCount, uint lifetimeRdfCountPerOccurrence, PickupType pkType, uint stockQuotaPerOccurrence, RepeatedField<Pickable> nextRenderFramePickables, uint consumableSpeciesId, uint buffSpeciesId, uint skillId, ref int pickableLocalIdCounter, ref int pickableCnt) {
            AssignToPickable(pickableLocalIdCounter, virtualGridX, virtualGridY, DEFAULT_PICKABLE_RISING_VEL_Y_VIRTUAL_GRID, remainingLifetimeRdfCount, recurQuota, PickableState.Pidle, 0, MAGIC_JOIN_INDEX_INVALID, virtualGridX, virtualGridY, takesGravity, rdfId, recurQuota, recurIntervalRdfCount, lifetimeRdfCountPerOccurrence, pkType, stockQuotaPerOccurrence, TERMINATING_EVTSUB_ID_INT, consumableSpeciesId, buffSpeciesId, skillId, nextRenderFramePickables[pickableCnt]);

            pickableLocalIdCounter++;
            pickableCnt++;

            // Explicitly specify termination of nextRenderFramePickables
            nextRenderFramePickables[pickableCnt].PickableLocalId = TERMINATING_PICKABLE_LOCAL_ID;

            return true;
        }

        private static void _calcPickableMovementPushbacks(RoomDownsyncFrame currRenderFrame, int roomCapacity, RepeatedField<Pickable> nextRenderFramePickables, ref SatResult overlapResult, ref SatResult primaryOverlapResult, Collision collision, Collider[] dynamicRectangleColliders, Vector[] effPushbacks, Vector[][] hardPushbackNormsArr, int iSt, int iEd, ILoggerBridge logger) {
            int primaryHardOverlapIndex;
            for (int i = iSt; i < iEd; i++) {
                Collider pickableCollider = dynamicRectangleColliders[i];
                if (null == pickableCollider.Data) continue;
                var pickableNextFrame = pickableCollider.Data as Pickable; // [WARNING] See "_moveAndInsertPickableColliders", the bound data in each collider is already belonging to "nextRenderFramePickables"!
                if (null == pickableNextFrame || TERMINATING_PICKABLE_LOCAL_ID == pickableNextFrame.PickableLocalId) {
                    //logger.LogWarn(String.Format("dynamicRectangleColliders[i:{0}] is not having pickable type! iSt={1}, iEd={2}", i, iSt, iEd));
                    continue;
                }

                primaryOverlapResult.reset();

                Collider aCollider = dynamicRectangleColliders[i];
                ConvexPolygon aShape = aCollider.Shape;
                int hardPushbackCnt = calcHardPushbacksNormsForPickable(currRenderFrame, pickableNextFrame, aCollider, aShape, hardPushbackNormsArr[i], collision, ref overlapResult, ref primaryOverlapResult, out primaryHardOverlapIndex, logger);

                if (0 < hardPushbackCnt) {
                    processPrimaryAndImpactEffPushback(effPushbacks[i], hardPushbackNormsArr[i], hardPushbackCnt, primaryHardOverlapIndex, SNAP_INTO_PLATFORM_OVERLAP, false);

                    float normAlignmentWithGravity = (primaryOverlapResult.OverlapY * -1f);  
                    bool landedOnGravityPushback = (SNAP_INTO_PLATFORM_THRESHOLD < normAlignmentWithGravity); 
                    if (landedOnGravityPushback) {
                        pickableNextFrame.VelY = 0;
                    }
                }
            }
        }
    }
}
