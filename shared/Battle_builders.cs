using System;
using pbc = global::Google.Protobuf.Collections;

namespace shared {
    public partial class Battle {
        public static Collider NewConvexPolygonCollider(ConvexPolygon srcPolygon, float spaceOffsetX, float spaceOffsetY, object data) {
            if (null == srcPolygon) throw new ArgumentNullException("Null srcPolygon is not allowed in `NewConvexPolygonCollider`");
            AlignPolygon2DToBoundingBox(srcPolygon);
            float w = 0, h = 0;

            for (int i = 0; i < srcPolygon.Points.Cnt; i++) {
                for (int j = 0; j < srcPolygon.Points.Cnt; j++) {
                    if (i == j) {
                        continue;
                    }
                    Vector? pi = srcPolygon.GetPointByOffset(i);
                    if (null == pi) {
                        throw new ArgumentNullException("Null pi is not allowed in `NewConvexPolygonCollider`!");
                    }
                    Vector? pj = srcPolygon.GetPointByOffset(j);
                    if (null == pj) {
                        throw new ArgumentNullException("Null pj is not allowed in `NewConvexPolygonCollider`!");
                    }
                    w = Math.Max(w, Math.Abs(pj.X - pi.X));
                    h = Math.Max(h, Math.Abs(pj.Y - pi.Y));
                }
            }

            return new Collider(srcPolygon.X + spaceOffsetX, srcPolygon.Y + spaceOffsetY, w, h, srcPolygon, data);
        }

        public static Collider NewRectCollider(float wx, float wy, float w, float h, float topPadding, float bottomPadding, float leftPadding, float rightPadding, float spaceOffsetX, float spaceOffsetY, object data) {
            // [WARNING] (spaceOffsetX, spaceOffsetY) are taken into consideration while calling "NewConvexPolygonCollider" -- because "NewConvexPolygonCollider" might also be called for "polylines extracted from Tiled", it's more convenient to organized the codes this way.
            var (blX, blY) = PolygonColliderCtrToBL(wx, wy, w * 0.5f, h * 0.5f, topPadding, bottomPadding, leftPadding, rightPadding, 0, 0);
            float effW = leftPadding + w + rightPadding, effH = bottomPadding + h + topPadding;
            var srcPolygon = new ConvexPolygon(blX, blY, new float[] {
                0, 0,
                0 + effW, 0,
                0 + effW, 0 + effH,
                0, 0 + effH
            });

            return NewConvexPolygonCollider(srcPolygon, spaceOffsetX, spaceOffsetY, data);
        }

        public static void UpdateRectCollider(Collider collider, float wx, float wy, float w, float h, float topPadding, float bottomPadding, float leftPadding, float rightPadding, float spaceOffsetX, float spaceOffsetY, object data) {
            var (blX, blY) = PolygonColliderCtrToBL(wx, wy, w * 0.5f, h * 0.5f, topPadding, bottomPadding, leftPadding, rightPadding, spaceOffsetX, spaceOffsetY);

            float effW = leftPadding + w + rightPadding;
            float effH = bottomPadding + h + topPadding;
            (collider.X, collider.Y, collider.W, collider.H) = (blX, blY, effW, effH);
            collider.Shape.UpdateAsRectangle(0, 0, effW, effH);

            collider.Data = data;
        }

        public static void AssignToPlayerDownsync(int id, int virtualGridX, int virtualGridY, int dirX, int dirY, int velX, int velY, int framesToRecover, int framesInChState, int activeSkillId, int activeSkillHit, int framesInvinsible, int speed, int battleState, CharacterState characterState, int joinIndex, int hp, int maxHp, int colliderRadius, bool inAir, bool onWall, int onWallNormX, int onWallNormY, bool capturedByInertia, int bulletTeamId, int chCollisionTeamId, int revivalVirtualGridX, int revivalVirtualGridY, PlayerDownsync dst) {
            dst.Id = id;
            dst.VirtualGridX = virtualGridX;
            dst.VirtualGridY = virtualGridY;
            dst.DirX = dirX;
            dst.DirY = dirY;
            dst.VelX = velX;
            dst.VelY = velY;
            dst.FramesToRecover = framesToRecover;
            dst.FramesInChState = framesInChState;
            dst.ActiveSkillId = activeSkillId;
            dst.ActiveSkillHit = activeSkillHit;
            dst.FramesInvinsible = framesInvinsible;
            dst.Speed = speed;
            dst.BattleState = battleState;
            dst.CharacterState = characterState;
            dst.JoinIndex = joinIndex;
            dst.Hp = hp;
            dst.MaxHp = maxHp;
            dst.ColliderRadius = colliderRadius;
            dst.InAir = inAir;
            dst.OnWall = onWall;
            dst.OnWallNormX = onWallNormX;
            dst.OnWallNormY = onWallNormY;
            dst.CapturedByInertia = capturedByInertia;
            dst.BulletTeamId = bulletTeamId;
            dst.ChCollisionTeamId = chCollisionTeamId;
            dst.RevivalVirtualGridX = revivalVirtualGridX;
            dst.RevivalVirtualGridY = revivalVirtualGridY;
        }

        public static Bullet NewBullet(BulletType btype, int bulletLocalId, int originatedRenderFrameId, int offenderJoinIndex, int startupFrames, int cancellableStFrame, int cancellableEdFrame, int activeFrames, int hitStunFrames, int blockStunFrames, int pushbackVelX, int pushbackVelY, int damage, int selfLockVelX, int selfLockVelY, int hitboxOffsetX, int hitboxOffsetY, int hitboxSizeX, int hitboxSizeY, bool blowUp, int teamId, BulletState blState, int framesInBlState, int explosionFrames, int speciesId, int speed, pbc::MapField<int, int> cancelTransit) {
            var cf = new BulletConfig();
            cf.BType = btype;
            cf.StartupFrames = startupFrames;
            cf.CancellableStFrame = cancellableStFrame;
            cf.CancellableEdFrame = cancellableEdFrame;
            cf.ActiveFrames = activeFrames;

            cf.HitStunFrames = hitStunFrames;
            cf.BlockStunFrames = blockStunFrames;
            cf.PushbackVelX = pushbackVelX;
            cf.PushbackVelY = pushbackVelY;
            cf.Damage = damage;

            cf.SelfLockVelX = selfLockVelX;
            cf.SelfLockVelY = selfLockVelY;

            cf.HitboxOffsetX = hitboxOffsetX;
            cf.HitboxOffsetY = hitboxOffsetY;
            cf.HitboxSizeX = hitboxSizeX;
            cf.HitboxSizeY = hitboxSizeY;

            cf.BlowUp = blowUp;
            cf.ExplosionFrames = explosionFrames;
            cf.SpeciesId = speciesId;
            cf.CancelTransit.MergeFrom(cancelTransit);
            cf.Speed = speed;

            var attr = new BulletBattleAttr();
            attr.BulletLocalId = bulletLocalId;
            attr.OriginatedRenderFrameId = originatedRenderFrameId;
            attr.OffenderJoinIndex = offenderJoinIndex;
            attr.TeamId = 0;

            var b = new Bullet();
            b.BlState = blState;
            b.FramesInBlState = framesInBlState;
            b.Config = cf;
            b.BattleAttr = attr;

            b.VirtualGridX = 0;
            b.VirtualGridY = 0;
            b.DirX = 0;
            b.DirY = 0;
            b.VelX = 0;
            b.VelY = 0;

            return b;
        }

        public static RoomDownsyncFrame NewPreallocatedRoomDownsyncFrame(int roomCapacity, int preallocBulletCount) {
            var ret = new RoomDownsyncFrame();
            ret.Id = TERMINATING_RENDER_FRAME_ID;
            ret.BulletLocalIdCounter = TERMINATING_BULLET_LOCAL_ID;

            for (int i = 0; i < roomCapacity; i++) {
                var single = new PlayerDownsync();
                single.Id = TERMINATING_PLAYER_ID;
                ret.PlayersArr.Add(single);
            }

            for (int i = 0; i < preallocBulletCount; i++) {
                var single = NewBullet(BulletType.Undetermined, TERMINATING_BULLET_LOCAL_ID, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, false, 0, BulletState.Startup, 0, 0, 0, 0, new pbc::MapField<int, int>());
                ret.Bullets.Add(single);
            }

            return ret;
        }

        public static InputFrameDownsync NewPreallocatedInputFrameDownsync(int roomCapacity) {
            var ret = new InputFrameDownsync();
            ret.InputFrameId = TERMINATING_INPUT_FRAME_ID;
            ret.ConfirmedList = 0;
            for (int i = 0; i < roomCapacity; i++) {
                ret.InputList.Add(0);
            }

            return ret;
        }

        public static Skill NewSkill(int recoveryFrames, int recoveryFramesOnBlock, int recoveryFramesOnHit, SkillTriggerType triggerType, int boundChState, pbc::RepeatedField<BulletConfig> hits) {
            // This helper function doesn't reduce a large amount of typing, but still it saves some typing of the field names for each newly declared skill 
            var s = new Skill {
                RecoveryFrames = recoveryFrames,
                RecoveryFramesOnBlock = recoveryFramesOnBlock,
                RecoveryFramesOnHit = recoveryFramesOnHit,
                TriggerType = triggerType
            };
            // "s.Hits" is readonly, thus not assignable by the initialization call above
            s.Hits.AddRange(hits);
            return s;
        }
    }
}
