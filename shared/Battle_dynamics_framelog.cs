using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using Google.Protobuf.Collections;

namespace shared {
    public partial class Battle {
        public static void trimRdfInPlace(RoomDownsyncFrame rdf) {
            // Removed bullets with TERMINATING_ID
            while (null != rdf.Bullets && 0 < rdf.Bullets.Count && TERMINATING_BULLET_LOCAL_ID == rdf.Bullets[rdf.Bullets.Count - 1].BattleAttr.BulletLocalId) {
                rdf.Bullets.RemoveAt(rdf.Bullets.Count - 1);
            }
        }

        public static void trimIfdInPlace(InputFrameDownsync ifd) {
            // Removed bullets with TERMINATING_ID
            ifd.ConfirmedList = 0;
        }

        public static string stringifyInventorySlot(InventorySlot slot) {
            if (null == slot) return "";
            return String.Format("q:{0},fr:{1}", slot.Quota, slot.FramesToRecover);
        }

        public static string stringifyInventory(Inventory iv) {
            if (null == iv) return "";
            var slotsSb = new List<String>();
            for (int k = 0; k < iv.Slots.Count; k++) {
                var slot = iv.Slots[k];
                if (InventorySlotStockType.NoneIv == slot.StockType) break;
                slotsSb.Add(stringifyInventorySlot(slot));
            }
            return String.Join('|', slotsSb);
        }

        public static string stringifyBulletImmuneRecords(RepeatedField<BulletImmuneRecord> records) {
            if (null == records) return "";
            var recordsSb = new List<String>();
            for (int k = 0; k < records.Count; k++) {
                var record = records[k];
                if (null == record) break;
                if (TERMINATING_BULLET_LOCAL_ID == record.BulletLocalId) break;
                try {
                    recordsSb.Add(String.Format("{bid:{0},lfc:{1}}", record.BulletLocalId, record.RemainingLifetimeRdfCount));
                } catch (Exception _) {
                }
            }
            return String.Join('|', recordsSb);
        }

        public static string stringifyPlayer(CharacterDownsync pd) {
            if (null == pd) return "";
            return String.Format("j:{0},x:{1},y:{2},vx:{3},fvx:{4},vy:{5},fr:{6},air:{7},wl:{8},sl:{9},{10},fc:{11},fi:{12},jt:{13},fri:{14},dx:{15},dy:{16},ct:{17},sjt:{18},oshp:{19},js:{20},fsj:{21},iv:[{22}],bir:{23}", pd.JoinIndex, pd.VirtualGridX, pd.VirtualGridY, pd.VelX, pd.FrictionVelX, pd.VelY, pd.FramesToRecover, pd.InAir, pd.OnWall, pd.OnSlope, pd.CharacterState, pd.FramesInChState, pd.FramesCapturedByInertia, pd.JumpTriggered, pd.FramesInvinsible, pd.DirX, pd.DirY, pd.ChCollisionTeamId, pd.SlipJumpTriggered, pd.PrimarilyOnSlippableHardPushback, pd.JumpStarted, pd.FramesToStartJump, stringifyInventory(pd.Inventory), stringifyBulletImmuneRecords(pd.BulletImmuneRecords));
        }

        public static string stringifyNpc(CharacterDownsync pd) {
            if (null == pd) return "";
            return String.Format("j:{0},x:{1},y:{2},vx:{3},fvx:{4},vy:{5},fr:{6},air:{7},wl:{8},sl:{9},{10},fc:{11},fi:{12},jt:{13},fri:{14},frp:{15},wpc:{16},wsp:{17},cp:{18},dx:{19},dy:{20},ct:{21},sjt:{22},oshp:{23},js:{24},fsj:{25},bir:{26}", pd.JoinIndex, pd.VirtualGridX, pd.VirtualGridY, pd.VelX, pd.FrictionVelX, pd.VelY, pd.FramesToRecover, pd.InAir, pd.OnWall, pd.OnSlope, pd.CharacterState, pd.FramesInChState, pd.FramesCapturedByInertia, pd.JumpTriggered, pd.FramesInvinsible, pd.FramesInPatrolCue, pd.WaivingPatrolCueId, pd.WaivingSpontaneousPatrol, pd.CapturedByPatrolCue, pd.DirX, pd.DirY, pd.ChCollisionTeamId, pd.SlipJumpTriggered, pd.PrimarilyOnSlippableHardPushback, pd.JumpStarted, pd.FramesToStartJump, stringifyBulletImmuneRecords(pd.BulletImmuneRecords));
        }

        public static string stringifyTrap(Trap tr) {
            if (null == tr) return "";
            return String.Format("id:{0},ts:{1},fts:{2},cpc:{3},fpc:{4},wpcid:{5},wsp:{6},x:{7},y:{8},dx:{9},dy:{10},vx:{11},vy:{12}", tr.TrapLocalId, tr.TrapState, tr.FramesInTrapState, tr.CapturedByPatrolCue, tr.FramesInPatrolCue, tr.WaivingPatrolCueId, tr.WaivingSpontaneousPatrol, tr.VirtualGridX, tr.VirtualGridY, tr.DirX, tr.DirY, tr.VelX, tr.VelY);
        }

        public static string stringifyBullet(Bullet bt) {
            if (null == bt) return "";
            return String.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},j:{13}", bt.BattleAttr.BulletLocalId, bt.BattleAttr.OriginatedRenderFrameId, bt.VirtualGridX, bt.VirtualGridY, bt.VelX, bt.VelY, bt.DirX, bt.DirY, bt.BlState, bt.FramesInBlState, bt.Config.HitboxSizeX, bt.Config.HitboxSizeY, bt.RepeatQuotaLeft, bt.BattleAttr.OffenderJoinIndex);
        }

        public static string stringifyRdf(RoomDownsyncFrame rdf) {
            var playerSb = new List<String>();
            for (int k = 0; k < rdf.PlayersArr.Count; k++) {
                playerSb.Add(stringifyPlayer(rdf.PlayersArr[k]));
            }

            var npcSb = new List<String>();
            for (int k = 0; k < rdf.NpcsArr.Count; k++) {
                var npc = rdf.NpcsArr[k];
                if (null == npc || TERMINATING_PLAYER_ID == npc.Id) break;
                npcSb.Add(stringifyNpc(npc));
            }

            var trapSb = new List<String>();
            for (int k = 0; k < rdf.TrapsArr.Count; k++) {
                var trap = rdf.TrapsArr[k];
                if (null == trap || TERMINATING_TRAP_ID == trap.TrapLocalId) break;
                trapSb.Add(stringifyTrap(trap));
            }

            var bulletSb = new List<String>();
            for (int k = 0; k < rdf.Bullets.Count; k++) {
                var bt = rdf.Bullets[k];
                if (null == bt || TERMINATING_BULLET_LOCAL_ID == bt.BattleAttr.BulletLocalId) break;
                bulletSb.Add(stringifyBullet(bt));
            }

            var rdfSb = new List<String>();
             
            var playerS = String.Join(';', playerSb);
            rdfSb.Add(String.Format("id:{0}", rdf.Id));
            rdfSb.Add(String.Format("ps:{0}", playerS));
            if (0 < npcSb.Count) {      
                rdfSb.Add(String.Format("ns:{0}", String.Join(';', npcSb)));
            }

            if (0 < trapSb.Count) {      
                rdfSb.Add(String.Format("ts:{0}", String.Join(';', trapSb)));
            }
            
            if (0 < bulletSb.Count) {
                rdfSb.Add(String.Format("bs:{0}", String.Join(';', bulletSb)));
            } 

            return String.Format("{{ {0} }}", String.Join('\n', rdfSb));
        }

        public static string stringifyIfd(InputFrameDownsync ifd, bool trimConfirmedList) {
            var inputListSb = new List<String>();
            for (int k = 0; k < ifd.InputList.Count; k++) {
                inputListSb.Add(String.Format("{0}", ifd.InputList[k]));
            }
            if (trimConfirmedList) {
                return String.Format("{{ ifId:{0},ipts:{1} }}", ifd.InputFrameId, String.Join(',', inputListSb));
            } else {
                return String.Format("{{ ifId:{0},ipts:{1},cfd:{2} }}", ifd.InputFrameId, String.Join(',', inputListSb), ifd.ConfirmedList);
            }
        }

        public static string stringifyIfdBatch(RepeatedField<InputFrameDownsync> ifdBatch, bool trimConfirmedList) {
            var listSb = new List<String>();
            for (int k = 0; k < ifdBatch.Count; k++) {
                listSb.Add(stringifyIfd(ifdBatch[k], trimConfirmedList));
            }
            return String.Join('\n', listSb);
        }

        public static string stringifyFrameLog(FrameLog fl, bool trimConfirmedList) {
            // Why do we need an extra class definition of "FrameLog" while having methods "stringifyRdf" & "stringifyIfd"? That's because we might need put "FrameLog" on transmission, i.e. sending to backend upon battle stopped, thus a wrapper class would provide some convenience though not 100% necessary.
            return String.Format("{0}\n{1}", stringifyRdf(fl.Rdf), stringifyIfd(fl.ActuallyUsedIdf, trimConfirmedList));
        }

        public static void wrapUpFrameLogs(FrameRingBuffer<RoomDownsyncFrame> renderBuffer, FrameRingBuffer<InputFrameDownsync> inputBuffer, Dictionary<int, InputFrameDownsync> rdfIdToActuallyUsedInput, bool trimConfirmedList, FrameRingBuffer<RdfPushbackFrameLog> pushbackFrameLogBuffer, string dirPath, string filename) {
            // TODO: On frontend, log "chaserRenderFrameId" at the end of its framelog file -- kindly note that for "rdfId > chaserRenderFrameId", they might be uncorrected by the latest inputs and thus expected to show some differences.  
            using (StreamWriter outputFile = new StreamWriter(Path.Combine(dirPath, filename))) {
                for (int i = renderBuffer.StFrameId; i < renderBuffer.EdFrameId; i++) {
                    var (ok1, rdf) = renderBuffer.GetByFrameId(i);
                    if (!ok1 || null == rdf) {
                        throw new ArgumentNullException(String.Format("wrapUpFrameLogs#1 rdf for i={0} doesn't exist! renderBuffer:{1}", i, renderBuffer.StFrameId, renderBuffer.toSimpleStat()));
                    }

                    trimRdfInPlace(rdf);
                    InputFrameDownsync ifd;
                    if (!rdfIdToActuallyUsedInput.TryGetValue(i, out ifd)) {
                        if (i + 1 == renderBuffer.EdFrameId) {
                            // It's OK that "InputFrameDownsync for the latest RoomDownsyncFrame" HASN'T BEEN USED YET. 
                            outputFile.Write(String.Format("{0}\n", stringifyRdf(rdf))); // Don't use "WriteLine", here we deliberately need a same "line ending symbol" across all platforms for better comparison!
                            break;
                        }
                        var j = ConvertToDelayedInputFrameId(i);
                        throw new ArgumentNullException(String.Format("wrapUpFrameLogs#2 ifd for i={0}, j={1} doesn't exist! renderBuffer:{2}, inputBuffer:{3}", i, j, renderBuffer.toSimpleStat(), inputBuffer.toSimpleStat()));
                    }
                    if (trimConfirmedList) {
                        trimIfdInPlace(ifd);
                    }
                    var frameLog = new FrameLog {
                        Rdf = rdf,
                        ActuallyUsedIdf = ifd
                    };
                    
                    outputFile.Write(stringifyFrameLog(frameLog, trimConfirmedList) + "\n");
                }

                for (int i = renderBuffer.StFrameId; i < renderBuffer.EdFrameId; i++) {
                    var (ok2, rdfPfl) = pushbackFrameLogBuffer.GetByFrameId(i);
                    if ((!ok2 || null == rdfPfl) && i+1 < renderBuffer.EdFrameId) {
                        throw new ArgumentNullException(String.Format("wrapUpFrameLogs#2 rdfPfl for i={0} doesn't exist! renderBuffer:{1}, pushbackFrameLogBuffer:{2}", i, renderBuffer.toSimpleStat(), pushbackFrameLogBuffer.toSimpleStat()));
                    }

                    if (null != rdfPfl) {       
                        outputFile.Write(rdfPfl.toString() + "\n");
                    }
                }
            }
        }

        public static PlayerStoryProgress? loadStoryProgress(string dirPath, string filename) {
            const int CHUNK_SIZE = (1 << 12);
            using (FileStream fs = new FileStream(Path.Combine(dirPath, filename), FileMode.OpenOrCreate, FileAccess.Read)) {
                using (BinaryReader br = new BinaryReader(fs, new ASCIIEncoding())) {
                    byte[] chunk = br.ReadBytes(CHUNK_SIZE);
                    if (0 < chunk.Length) {
                        return PlayerStoryProgress.Parser.ParseFrom(chunk);
                    } else {
                        return null;
                    }
                }
            }
        }
    }
}
