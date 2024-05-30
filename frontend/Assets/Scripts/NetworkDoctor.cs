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

    private int localRequiredIfdId;
    private float inputRateThreshold;
    private float recvRateThreshold;
    private FrameRingBuffer<QEle> sendingQ;
    private FrameRingBuffer<QEle> inputFrameDownsyncQ;
    private FrameRingBuffer<QEle> peerInputFrameUpsyncQ;
    int immediateRollbackFrames;
    int lockedStepsCnt;
    long udpPunchedCnt;

    int lastForceResyncedIfdId;
    bool exclusivelySelfConfirmedAtLastForceResync;
    bool exclusivelySelfUnconfirmedAtLastForceResync;
    bool lastForceResyncHasHistoryRdfUpdate;

    public void Reset() {
        localRequiredIfdId = 0;
        immediateRollbackFrames = 0;
        lockedStepsCnt = 0;
        udpPunchedCnt = 0;
        inputRateThreshold = Battle.BATTLE_DYNAMICS_FPS * 1f / (1 << Battle.INPUT_SCALE_FRAMES);
        recvRateThreshold = (Battle.BATTLE_DYNAMICS_FPS-5) * 1f / (1 << Battle.INPUT_SCALE_FRAMES);
        lastForceResyncedIfdId = 0;
        exclusivelySelfConfirmedAtLastForceResync = false;
        exclusivelySelfUnconfirmedAtLastForceResync = false;
        lastForceResyncHasHistoryRdfUpdate = false;

        sendingQ.Clear();
        inputFrameDownsyncQ.Clear();
        peerInputFrameUpsyncQ.Clear();
    }

    public void LogForceResyncedIfdId(int val, bool exclusivelySelfConfirmed, bool exclusivelySelfUnconfirmed, bool hasHistoryRdfUpdate) {
        /*
        [WARNING] 
        
        In practice, after UDP contributes to backend inputBuffer confirmation, "type#1 forceConfirmation" is verified to be accurate (i.e. the slow peer appears laggy on the fast peer, while the fast peer appears smooth on the slow peer).
        */
        lastForceResyncedIfdId = val;
        exclusivelySelfConfirmedAtLastForceResync = exclusivelySelfConfirmed;
        exclusivelySelfUnconfirmedAtLastForceResync = exclusivelySelfUnconfirmed;
        lastForceResyncHasHistoryRdfUpdate = hasHistoryRdfUpdate;
    }

    public void LogLocalRequiredIfdId(int val) {
        localRequiredIfdId = val;
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

        int minInputFrameIdFront = Battle.MAX_INT;
        for (int k = 0; k < roomCapacity; ++k) {
            if (k + 1 == selfJoinIndex) continue; // Don't count self in
            if (lastIndividuallyConfirmedInputFrameId[k] >= minInputFrameIdFront) continue;
            minInputFrameIdFront = lastIndividuallyConfirmedInputFrameId[k];
        }

        int ifdIdLag = (localRequiredIfdId - minInputFrameIdFront);
        if (0 > ifdIdLag) {
            ifdIdLag = 0;
        }

        bool ifdLagSignificant = (localRequiredIfdId > minInputFrameIdFront) && localRequiredIfdId > (inputFrameUpsyncDelayTolerance + minInputFrameIdFront); // First comparison to avoid integer overflow 

        bool recvIfdIdFrontIsFromLastResynced = (minInputFrameIdFront <= lastForceResyncedIfdId);
        bool lockstepAllowedByLastForceResync = false;
        if (recvIfdIdFrontIsFromLastResynced) {
            if (exclusivelySelfConfirmedAtLastForceResync) {
                lockstepAllowedByLastForceResync = true;
            } else if (!exclusivelySelfUnconfirmedAtLastForceResync && latestRecvMillisTooOld) {
                lockstepAllowedByLastForceResync = true;
            }
        } else {
            lockstepAllowedByLastForceResync = true;
        }

        bool rollbackFramesDueToLastForceResync = (recvIfdIdFrontIsFromLastResynced && lastForceResyncHasHistoryRdfUpdate);
		bool sendingFpsNormal = (sendingFps >= inputRateThreshold) || (recvIfdIdFrontIsFromLastResynced && exclusivelySelfConfirmedAtLastForceResync);

        if (ifdLagSignificant && sendingFpsNormal && lockstepAllowedByLastForceResync && (immediateRollbackFrames > renderFrameLagTolerance && !rollbackFramesDueToLastForceResync)) {
            /*
            [WARNING]

            We shouldn't rely solely on "immediateRollbackFrames > renderFrameLagTolerance" for lockstep decision, because upon "type#X forceConfirmation" if history update occurs, the "immediateRollbackFrames" can surge abruptly while "minInputFrameIdFront" is advanced enough.

            Similarly, "ifdLagSignificant" alone is not enough to assert the need of lockstep, because it could be a slow-ticker's slow network syndrome. 

            I'm not quite sure whether or not "latestRecvMillisTooOld" should be taken into consideration when deciding "shouldLockStep", because when "ifdLagSignificant && sendingFpsNormal && true == latestRecvMillisTooOld", we know that during "[latestRecvMillis, nowMillis]" the receiving of both TCP(WebSocket) and UDP packets doesn't do well, yet the peer(s) could still be quite advanced at "noDelayInputFrameId" locally.

            What about "ifdLagSignificant && sendingFpsNormal && false == latestRecvMillisTooOld", is it a good sign to lock step? Maybe.
        
            The bottom line is that we don't apply "lockstep" to a peer who's deemed "slow ticker" on the backend!
            */

            Debug.Log(String.Format("Should lock step, immediateRollbackFrames={0}, localRequiredIfdId={1}, minInputFrameIdFront={2}, renderFrameLagTolerance={3}, inputFrameUpsyncDelayTolerance={4}, sendingFps={5}, srvDownsyncFps={6}, inputRateThreshold={7}, latestRecvMillis={8}, nowMillis={9}, latestRecvMillisTooOld={10}, lastForceResyncedIfdId={11}, exclusivelySelfConfirmedAtLastForceResync={12}, exclusivelySelfUnconfirmedAtLastForceResync={13}", immediateRollbackFrames, localRequiredIfdId, minInputFrameIdFront, renderFrameLagTolerance, inputFrameUpsyncDelayTolerance, sendingFps, srvDownsyncFps, inputRateThreshold, latestRecvMillis, nowMillis, latestRecvMillisTooOld, lastForceResyncedIfdId, exclusivelySelfConfirmedAtLastForceResync, exclusivelySelfUnconfirmedAtLastForceResync));

            return (true, ifdIdLag, sendingFps, srvDownsyncFps, peerUpsyncFps, immediateRollbackFrames, lockedStepsCnt, Interlocked.Read(ref udpPunchedCnt));
		}

        return (false, ifdIdLag, sendingFps, srvDownsyncFps, peerUpsyncFps, immediateRollbackFrames, lockedStepsCnt, Interlocked.Read(ref udpPunchedCnt));
    }
}
