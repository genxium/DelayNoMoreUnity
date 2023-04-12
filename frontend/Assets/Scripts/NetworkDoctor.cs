using UnityEngine;
using System;
using shared;

public class NetworkDoctor {

    private class QEle {
        public int i, j;
        public long t;
    }

    private static NetworkDoctor _instance;

    public static NetworkDoctor Instance {
        get {
            if (null == _instance) _instance = new NetworkDoctor();
            return _instance;
        }
    }

    private NetworkDoctor() {
        Reset(128);
    }

    private int inputFrameIdFront;
    private int inputRateThreshold;
    private FrameRingBuffer<QEle> sendingQ;
    private FrameRingBuffer<QEle> inputFrameDownsyncQ;
    private FrameRingBuffer<QEle> peerInputFrameUpsyncQ;
    int immediateRollbackFrames;
    int lockedStepCnt;
    int peerUpsyncThreshold;
    int rollbackFramesThreshold;

    public void Reset(int statsBuffSize) {
        inputFrameIdFront = 0;
        sendingQ = new FrameRingBuffer<QEle>(statsBuffSize);
        inputFrameDownsyncQ = new FrameRingBuffer<QEle>(statsBuffSize);
        peerInputFrameUpsyncQ = new FrameRingBuffer<QEle>(statsBuffSize);
        immediateRollbackFrames = 0;
        lockedStepCnt = 0;

        inputRateThreshold = Battle.ConvertToNoDelayInputFrameId(60);
        peerUpsyncThreshold = 8;
        rollbackFramesThreshold = 8; // Roughly the minimum "TurnAroundFramesToRecover".
    }

    public void LogInputFrameIdFront(int val) {
        inputFrameIdFront = val;
    }

    public void LogSending(int stFrameId, int edFrameId) {
        sendingQ.Put(new QEle {
            i = stFrameId,
            j = edFrameId,
            t = DateTimeOffset.Now.ToUnixTimeMilliseconds()
        });
    }

    public void LogInputFrameDownsync(int stFrameId, int edFrameId) {
        inputFrameDownsyncQ.Put(new QEle {
            i = stFrameId,
            j = edFrameId,
            t = DateTimeOffset.Now.ToUnixTimeMilliseconds()
        });
    }

    public void LogPeerInputFrameUpsync(int stFrameId, int edFrameId) {
        peerInputFrameUpsyncQ.Put(new QEle {
            i = stFrameId,
            j = edFrameId,
            t = DateTimeOffset.Now.ToUnixTimeMilliseconds()
        });
    }

    public void LogRollbackFrames(int val) {
        immediateRollbackFrames = val;
    }

    public void LogLockedStepCnt() {
        lockedStepCnt += 1;
    }

    public (int, int, int, int, int, int) Stats() {
        int sendingFps = 0,
        srvDownsyncFps = 0,
        peerUpsyncFps = 0;
        if (1 < sendingQ.Cnt) {
            var (_, st) = sendingQ.GetByFrameId(sendingQ.StFrameId);
            var (_, ed) = sendingQ.GetByFrameId(sendingQ.EdFrameId - 1);
            long elapsedMillis = ed.t - st.t;
            if (null != st && null != ed && 0 < elapsedMillis) {
                sendingFps = (int)((long)(ed.j - st.i) * 1000 / elapsedMillis);
            }
        }
        if (1 < inputFrameDownsyncQ.Cnt) {
            var (_, st) = inputFrameDownsyncQ.GetByFrameId(inputFrameDownsyncQ.StFrameId);
            var (_, ed) = inputFrameDownsyncQ.GetByFrameId(inputFrameDownsyncQ.EdFrameId - 1);
            long elapsedMillis = ed.t - st.t;
            if (null != st && null != ed && 0 < elapsedMillis) {
                srvDownsyncFps = (int)((long)(ed.j - st.i) * 1000 / elapsedMillis);
            }
        }
        if (1 < peerInputFrameUpsyncQ.Cnt) {
            var (_, st) = peerInputFrameUpsyncQ.GetByFrameId(peerInputFrameUpsyncQ.StFrameId);
            var (_, ed) = peerInputFrameUpsyncQ.GetByFrameId(this.peerInputFrameUpsyncQ.EdFrameId - 1);
            long elapsedMillis = ed.t - st.t;
            if (null != st && null != ed && 0 < elapsedMillis)
                peerUpsyncFps = (int)((long)(ed.j - st.i) * 1000 / elapsedMillis);
        }
        return (inputFrameIdFront, sendingFps, srvDownsyncFps, peerUpsyncFps, immediateRollbackFrames, lockedStepCnt);
    }

    public (bool, int, int, int, int, int, int) IsTooFast(int roomCapacity, int selfJoinIndex, int[] lastIndividuallyConfirmedInputFrameId, int inputFrameUpsyncDelayTolerance) {
        var (inputFrameIdFront, sendingFps, srvDownsyncFps, peerUpsyncFps, rollbackFrames, skippedRenderFrameCnt) = Stats();
        if (sendingFps >= inputRateThreshold + 3) {
            // Don't send too fast
            return (true, inputFrameIdFront, sendingFps, srvDownsyncFps, peerUpsyncFps, rollbackFrames, skippedRenderFrameCnt);
        } else {
            bool sendingFpsNormal = (sendingFps >= inputRateThreshold);
            // An outstanding lag within the "inputFrameDownsyncQ" will reduce "srvDownsyncFps", HOWEVER, a constant lag wouldn't impact "srvDownsyncFps"! In native platforms we might use PING value might help as a supplement information to confirm that the "selfPlayer" is not lagged within the time accounted by "inputFrameDownsyncQ".  
            bool recvFpsNormal = (srvDownsyncFps >= inputRateThreshold || peerUpsyncFps >= inputRateThreshold * (roomCapacity - 1));
            if (sendingFpsNormal && recvFpsNormal) {
                int minInputFrameIdFront = -shared.Battle.MAX_INT;
                for (int k = 0; k < roomCapacity; ++k) {
                    if (k + 1 == selfJoinIndex) continue;
                    if (lastIndividuallyConfirmedInputFrameId[k] >= minInputFrameIdFront) continue;
                    minInputFrameIdFront = lastIndividuallyConfirmedInputFrameId[k];
                }
                if ((inputFrameIdFront > minInputFrameIdFront) && ((inputFrameIdFront - minInputFrameIdFront) > (inputFrameUpsyncDelayTolerance + 1))) {
                    // first comparison condition is to avoid numeric overflow
                    return (true, inputFrameIdFront, sendingFps, srvDownsyncFps, peerUpsyncFps, rollbackFrames, skippedRenderFrameCnt);
                }
            }
        }

        return (false, inputFrameIdFront, sendingFps, srvDownsyncFps, peerUpsyncFps, rollbackFrames, skippedRenderFrameCnt);
    }
}
