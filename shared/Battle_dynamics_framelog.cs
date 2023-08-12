using System;
using System.IO;
using System.Collections.Generic;

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

        public static string stringifyPlayer(CharacterDownsync pd) {
            if (null == pd) return "";
            return String.Format("j:{0},x:{1},y:{2},vx:{3},fvx:{4},vy:{5},fr:{6},air:{7},wl:{8},sl:{9},{10},fcs:{11},ci:{12},jt:{13},fri:{14},dx:{15},dy:{16},ct:{17}", pd.JoinIndex, pd.VirtualGridX, pd.VirtualGridY, pd.VelX, pd.FrictionVelX, pd.VelY, pd.FramesToRecover, pd.InAir, pd.OnWall, pd.OnSlope, pd.CharacterState, pd.FramesInChState, pd.CapturedByInertia, pd.JumpTriggered, pd.FramesInvinsible, pd.DirX, pd.DirY, pd.ChCollisionTeamId);
        }

        public static string stringifyNpc(CharacterDownsync pd) {
            if (null == pd) return "";
            return String.Format("j:{0},x:{1},y:{2},vx:{3},fvx:{4},vy:{5},fr:{6},air:{7},wl:{8},sl:{9},{10},fcs:{11},ci:{12},jt:{13},fri:{14},frp:{15},wpc:{16},wsp:{17},cp:{18},dx:{19},dy:{20},ct:{21}", pd.JoinIndex, pd.VirtualGridX, pd.VirtualGridY, pd.VelX, pd.FrictionVelX, pd.VelY, pd.FramesToRecover, pd.InAir, pd.OnWall, pd.OnSlope, pd.CharacterState, pd.FramesInChState, pd.CapturedByInertia, pd.JumpTriggered, pd.FramesInvinsible, pd.FramesInPatrolCue, pd.WaivingPatrolCueId, pd.WaivingSpontaneousPatrol, pd.CapturedByPatrolCue, pd.DirX, pd.DirY, pd.ChCollisionTeamId);
        }

        public static string stringifyTrap(Trap tr) {
            if (null == tr) return "";
            return String.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12}", tr.TrapLocalId, tr.TrapState, tr.FramesInTrapState, tr.CapturedByPatrolCue, tr.FramesInPatrolCue, tr.WaivingPatrolCueId, tr.WaivingSpontaneousPatrol, tr.VirtualGridX, tr.VirtualGridY, tr.DirX, tr.DirY, tr.VelX, tr.VelY);
        }

        public static string stringifyBullet(Bullet bt) {
            if (null == bt) return "";
            return String.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11}", bt.BattleAttr.BulletLocalId, bt.BattleAttr.OriginatedRenderFrameId, bt.VirtualGridX, bt.VirtualGridY, bt.VelX, bt.VelY, bt.DirX, bt.DirY, bt.BlState, bt.FramesInBlState, bt.Config.HitboxSizeX, bt.Config.HitboxSizeY);
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

        public static string stringifyFrameLog(FrameLog fl, bool trimConfirmedList) {
            // Why do we need an extra class definition of "FrameLog" while having methods "stringifyRdf" & "stringifyIfd"? That's because we might need put "FrameLog" on transmission, i.e. sending to backend upon battle stopped, thus a wrapper class would provide some convenience though not 100% necessary.
            return String.Format("{0}\n{1}", stringifyRdf(fl.Rdf), stringifyIfd(fl.ActuallyUsedIdf, trimConfirmedList));
        }

        public static void wrapUpFrameLogs(FrameRingBuffer<RoomDownsyncFrame> renderBuffer, FrameRingBuffer<InputFrameDownsync> inputBuffer, Dictionary<int, InputFrameDownsync> rdfIdToActuallyUsedInput, bool trimConfirmedList, FrameRingBuffer<RdfPushbackFrameLog> pushbackFrameLogBuffer, string dirPath, string filename) {
            using (StreamWriter outputFile = new StreamWriter(Path.Combine(dirPath, filename))) {
                for (int i = renderBuffer.StFrameId; i < renderBuffer.EdFrameId; i++) {
                    var (ok1, rdf) = renderBuffer.GetByFrameId(i);
                    if (!ok1 || null == rdf) {
                        throw new ArgumentNullException(String.Format("wrapUpFrameLogs#1 rdf for i={0} doesn't exist! renderBuffer[StFrameId, EdFrameId)=[{1}, {2})", i, renderBuffer.StFrameId, renderBuffer.EdFrameId));
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
                        throw new ArgumentNullException(String.Format("wrapUpFrameLogs#2 ifd for i={0}, j={1} doesn't exist! renderBuffer[StFrameId, EdFrameId)=[{2}, {3}), inputBuffer[StFrameId, EdFrameId)=[{4}, {5})", i, j, renderBuffer.StFrameId, renderBuffer.EdFrameId, inputBuffer.StFrameId, inputBuffer.EdFrameId));
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
                        throw new ArgumentNullException(String.Format("wrapUpFrameLogs#2 rdfPfl for i={0} doesn't exist! renderBuffer[StFrameId, EdFrameId)=[{1}, {2}), pushbackFrameLogBuffer[StFrameId, EdFrameId)=[{3}, {4})", i, renderBuffer.StFrameId, renderBuffer.EdFrameId, pushbackFrameLogBuffer.StFrameId, pushbackFrameLogBuffer.EdFrameId));
                    }

                    if (null != rdfPfl) {       
                        outputFile.Write(rdfPfl.toString() + "\n");
                    }
                }
            }
        }
    }
}
