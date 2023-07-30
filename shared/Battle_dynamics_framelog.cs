using System;
using System.IO;
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

        public static string stringifyCharacterDownsync(CharacterDownsync pd) {
            if (null == pd) return "";
            return String.Format("{0},{1},{2},{3},{4},{5},{6}", pd.JoinIndex, pd.VirtualGridX, pd.VirtualGridY, pd.VelX, pd.VelY, pd.FramesToRecover, pd.InAir, pd.OnWall);
        }

        public static string stringifyBullet(Bullet bt) {
            if (null == bt) return "";
            return String.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13}", bt.BattleAttr.BulletLocalId, bt.BattleAttr.OriginatedRenderFrameId, bt.VirtualGridX, bt.VirtualGridY, bt.VelX, bt.VelY, bt.DirX, bt.DirY, bt.BlState, bt.FramesInBlState, bt.Config.HitboxSizeX, bt.Config.HitboxSizeY);
        }

        public static string stringifyRdf(RoomDownsyncFrame rdf) {
            var playerSb = new List<String>();
            for (int k = 0; k < rdf.PlayersArr.Count; k++) {
                playerSb.Add(stringifyCharacterDownsync(rdf.PlayersArr[k]));
            }

            var bulletSb = new List<String>();
            for (int k = 0; k < rdf.Bullets.Count; k++) {
                var bt = rdf.Bullets[k];
                if (null == bt || TERMINATING_BULLET_LOCAL_ID == bt.BattleAttr.BulletLocalId) break;
                bulletSb.Add(stringifyBullet(bt));
            }

            if (0 >= bulletSb.Count) {
                return String.Format("{{ id:{0}\nps:{1} }}", rdf.Id, String.Join(',', playerSb));
            } else {
                return String.Format("{{ id:{0}\nps:{1}\nbs:{2} }}", rdf.Id, String.Join(',', playerSb), String.Join(',', bulletSb));
            }
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

        public static void wrapUpFrameLogs(FrameRingBuffer<RoomDownsyncFrame> renderBuffer, FrameRingBuffer<InputFrameDownsync> inputBuffer, Dictionary<int, InputFrameDownsync> rdfIdToActuallyUsedInput, bool trimConfirmedList, string dirPath, string filename) {
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
                            outputFile.WriteLine(String.Format("[{0}]", stringifyRdf(rdf)));
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
                    outputFile.WriteLine(String.Format("[{0}]", stringifyFrameLog(frameLog, trimConfirmedList)));
                }
            }
        }
    }
}
