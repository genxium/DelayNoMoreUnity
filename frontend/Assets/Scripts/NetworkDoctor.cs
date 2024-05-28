using System;
using System.Threading;
using shared;
using UnityEngine;

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
        preallocateHolders(128);
        Reset();
    }

    private void preallocateHolders(int bufferSize) {
        sendingQ = new FrameRingBuffer<QEle>(bufferSize);
        inputFrameDownsyncQ = new FrameRingBuffer<QEle>(bufferSize);
        peerInputFrameUpsyncQ = new FrameRingBuffer<QEle>(bufferSize);
        for (int i = 0; i < bufferSize; i++) {
            sendingQ.Put(new QEle {
                i = 0, j = 0, t = 0
            });
            inputFrameDownsyncQ.Put(new QEle {
                i = 0, j = 0, t = 0
            });
            peerInputFrameUpsyncQ.Put(new QEle {
                i = 0, j = 0, t = 0
            });
        }

        // Clear by "Reset()", and then use by "DryPut()"
    }

    private int inputFrameIdFront;
    private float inputRateThreshold;
    private float recvRateThreshold;
    private FrameRingBuffer<QEle> sendingQ;
    private FrameRingBuffer<QEle> inputFrameDownsyncQ;
    private FrameRingBuffer<QEle> peerInputFrameUpsyncQ;
    int immediateRollbackFrames;
    int lockedStepsCnt;
    long udpPunchedCnt;

    public void Reset() {
        inputFrameIdFront = 0;
        immediateRollbackFrames = 0;
        lockedStepsCnt = 0;
        udpPunchedCnt = 0;
        inputRateThreshold = Battle.BATTLE_DYNAMICS_FPS * 1f / (1 << Battle.INPUT_SCALE_FRAMES);
        recvRateThreshold = (Battle.BATTLE_DYNAMICS_FPS-5) * 1f / (1 << Battle.INPUT_SCALE_FRAMES);

        sendingQ.Clear();
        inputFrameDownsyncQ.Clear();
        peerInputFrameUpsyncQ.Clear();
    }

    public void LogInputFrameIdFront(int val) {
        inputFrameIdFront = val;
    }

    public void LogSending(int i, int j) {
        if (i > j) return; 
        int oldEd = sendingQ.EdFrameId;
        sendingQ.DryPut();
        var (ok, holder) = sendingQ.GetByFrameId(oldEd);
        if (!ok || null == holder) {
            return;
        }
        holder.i = i;
        holder.j = j;
        holder.t = DateTimeOffset.Now.ToUnixTimeMilliseconds();
    }

    public void LogInputFrameDownsync(int i, int j) {
        if (i > j) return; 
        int oldEd = inputFrameDownsyncQ.EdFrameId;
        inputFrameDownsyncQ.DryPut();
        var (ok, holder) = inputFrameDownsyncQ.GetByFrameId(oldEd);
        if (!ok || null == holder) {
            return;
        }
        holder.i = i;
        holder.j = j;
        holder.t = DateTimeOffset.Now.ToUnixTimeMilliseconds();
    }

    public void LogPeerInputFrameUpsync(int i, int j) {
        if (i > j) return; 
        int oldEd = peerInputFrameUpsyncQ.EdFrameId;
        peerInputFrameUpsyncQ.DryPut();
        var (ok, holder) = peerInputFrameUpsyncQ.GetByFrameId(oldEd);
        if (!ok || null == holder) {
            return;
        }
        holder.i = i;
        holder.j = j;
        holder.t = DateTimeOffset.Now.ToUnixTimeMilliseconds();
    }

    public void LogRollbackFrames(int val) {
        immediateRollbackFrames = val;
    }

    public void LogLockedStepCnt() {
        lockedStepsCnt += 1;
    }
    public void LogUdpPunchedCnt(long val) {
        long oldCnt = Interlocked.Read(ref udpPunchedCnt);
        if (oldCnt > val) return;
        Interlocked.Exchange(ref udpPunchedCnt, val);
    }

    public (float, float, float) Stats() {
        float sendingFps = 0f,
        srvDownsyncFps = 0f,
        peerUpsyncFps = 0f;
        if (1 < sendingQ.Cnt) {
            var st = sendingQ.GetFirst();
            var ed = sendingQ.GetLast();
            long elapsedMillis = ed.t - st.t;
            if (null != st && null != ed && 0 < elapsedMillis) {
                sendingFps = ((ed.j - st.i) * 1000f / elapsedMillis);
            }
        }
        if (1 < inputFrameDownsyncQ.Cnt) {
            var st = inputFrameDownsyncQ.GetFirst();
            var ed = inputFrameDownsyncQ.GetLast();
            long elapsedMillis = ed.t - st.t;
            if (null != st && null != ed && 0 < elapsedMillis) {
                srvDownsyncFps = ((ed.j - st.i) * 1000f / elapsedMillis);
            }
        }
        if (1 < peerInputFrameUpsyncQ.Cnt) {
            var st = peerInputFrameUpsyncQ.GetFirst();
            var ed = peerInputFrameUpsyncQ.GetLast();
            long elapsedMillis = ed.t - st.t;
            if (null != st && null != ed && 0 < elapsedMillis) {
                peerUpsyncFps = ((ed.j - st.i) * 1000f / elapsedMillis);
            }
        }
        return (sendingFps, srvDownsyncFps, peerUpsyncFps);
    }

    public (bool, int, float, float, float, int, int, long) IsTooFast(int roomCapacity, int selfJoinIndex, int[] lastIndividuallyConfirmedInputFrameId, int renderFrameLagTolerance, int inputFrameUpsyncDelayTolerance) {
        var (sendingFps, srvDownsyncFps, peerUpsyncFps) = Stats();
		bool sendingFpsNormal = (sendingFps >= inputRateThreshold);
		// An outstanding lag within the "inputFrameDownsyncQ" will reduce "srvDownsyncFps", HOWEVER, a constant lag wouldn't impact "srvDownsyncFps"! In native platforms we might use PING value might help as a supplement information to confirm that the "selfPlayer" is not lagged within the time accounted by "inputFrameDownsyncQ".  

        long latestRecvMillis = -Battle.MAX_INT;
        var ed1 = inputFrameDownsyncQ.GetLast(); 
        if (null != ed1 && ed1.t > latestRecvMillis) {
            latestRecvMillis = ed1.t;
        } 
        var ed2 = peerInputFrameUpsyncQ.GetLast();
        if (null != ed2 && ed2.t > latestRecvMillis) {
            latestRecvMillis = ed2.t;
        } 
        
        /* 
        [WARNING] 

        Equivalent to "(((nowMillis - latestRecvMillis)/millisPerRdf) >> INPUT_SCALE_FRAMES) >= inputFrameUpsyncDelayTolerance" where "millisPerRdf = (1000/BATTLE_DYNAMICS_FPS)" 
        */
        long nowMillis = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        bool latestRecvMillisTooOld = (nowMillis - latestRecvMillis)*Battle.BATTLE_DYNAMICS_FPS >= 1000*(inputFrameUpsyncDelayTolerance << Battle.INPUT_SCALE_FRAMES); 

        int inputFrameIdFrontLag = 0;
        int minInputFrameIdFront = Battle.MAX_INT;
        for (int k = 0; k < roomCapacity; ++k) {
            if (k + 1 == selfJoinIndex) continue; // Don't count self in
            if (lastIndividuallyConfirmedInputFrameId[k] >= minInputFrameIdFront) continue;
            minInputFrameIdFront = lastIndividuallyConfirmedInputFrameId[k];
        }

        bool ifdLagSignificant = (inputFrameIdFront > minInputFrameIdFront) && inputFrameIdFront > (inputFrameUpsyncDelayTolerance + minInputFrameIdFront); // First comparison to avoid integer overflow 

        if (ifdLagSignificant && sendingFpsNormal && (latestRecvMillisTooOld || immediateRollbackFrames > renderFrameLagTolerance)) {
            /*
            [WARNING]
        
            The bottom line is that we don't apply "lockstep" to a peer who's deemed "slow ticker" on the backend!
            */

            Debug.Log(String.Format("Should lock step, immediateRollbackFrames={0}, inputFrameIdFront={1}, minInputFrameIdFront={2}, renderFrameLagTolerance={3}, inputFrameUpsyncDelayTolerance={4}, sendingFps={5}, srvDownsyncFps={6}, inputRateThreshold={7}, latestRecvMillis={8}, nowMillis={9}", immediateRollbackFrames, inputFrameIdFront, minInputFrameIdFront, renderFrameLagTolerance, inputFrameUpsyncDelayTolerance, sendingFps, srvDownsyncFps, inputRateThreshold, latestRecvMillis, nowMillis));

            inputFrameIdFrontLag = inputFrameIdFront - minInputFrameIdFront;

            return (true, inputFrameIdFrontLag, sendingFps, srvDownsyncFps, peerUpsyncFps, immediateRollbackFrames, lockedStepsCnt, Interlocked.Read(ref udpPunchedCnt));
		}

        return (false, inputFrameIdFrontLag, sendingFps, srvDownsyncFps, peerUpsyncFps, immediateRollbackFrames, lockedStepsCnt, Interlocked.Read(ref udpPunchedCnt));
    }
}
