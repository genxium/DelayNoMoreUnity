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
    bool lastForceResyncHasRollbackBurst;

    int exclusivelySelfConfirmedLockStepQuota;
    
    // For display on NetworkDoctorInfo panel only
    public float DEFAULT_INDICATOR_COUNTDOWN_RDF_CNT = 120.0f; // 60.0f == 1 second  
    public float chasedToPlayerRdfIdIndicatorCountdown;
    public float forceResyncImmediatePumpIndicatorCountdown;
    public float forceResyncFutureAppliedIndicatorCountdown;

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
        lastForceResyncHasRollbackBurst = false;

        exclusivelySelfConfirmedLockStepQuota = 0;

        chasedToPlayerRdfIdIndicatorCountdown = 0;
        forceResyncImmediatePumpIndicatorCountdown = 0;
        forceResyncFutureAppliedIndicatorCountdown = 0;

        sendingQ.Clear();
        inputFrameDownsyncQ.Clear();
        peerInputFrameUpsyncQ.Clear();
    }

    public void LogChasedToPlayerRdfId() {
        chasedToPlayerRdfIdIndicatorCountdown = DEFAULT_INDICATOR_COUNTDOWN_RDF_CNT;
    }

    public void LogForceResyncImmediatePump() {
        forceResyncImmediatePumpIndicatorCountdown = DEFAULT_INDICATOR_COUNTDOWN_RDF_CNT;
    }

    public void LogForceResyncFutureApplied() {
        forceResyncFutureAppliedIndicatorCountdown = DEFAULT_INDICATOR_COUNTDOWN_RDF_CNT;
    }

    public void LogForceResyncedIfdId(int val, bool exclusivelySelfConfirmed, bool exclusivelySelfUnconfirmed, bool hasRollbackBurst, int inputFrameUpsyncDelayTolerance) {
        /*
        [WARNING] 
        
        In practice, after UDP contributes to backend inputBuffer confirmation, "type#1 forceConfirmation" is verified to be accurate (i.e. the slow peer appears laggy on the fast peer, while the fast peer appears smooth on the slow peer).
        */
        lastForceResyncedIfdId = val;
        exclusivelySelfConfirmedAtLastForceResync = exclusivelySelfConfirmed;
        exclusivelySelfUnconfirmedAtLastForceResync = exclusivelySelfUnconfirmed;
        lastForceResyncHasRollbackBurst = hasRollbackBurst;

        if (exclusivelySelfConfirmedAtLastForceResync) {
            exclusivelySelfConfirmedLockStepQuota = (inputFrameUpsyncDelayTolerance << Battle.INPUT_SCALE_FRAMES);    
        }
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

    public (bool, int, float, float, float, int, int, long) IsTooFast(int roomCapacity, int selfJoinIndex, int[] lastIndividuallyConfirmedInputFrameId, int rdfLagTolerance, int ifdLagTolerance) {
        var (sendingFps, srvDownsyncFps, peerUpsyncFps) = Stats();
        chasedToPlayerRdfIdIndicatorCountdown -= 1.0f;
        if (0 > chasedToPlayerRdfIdIndicatorCountdown) {
            chasedToPlayerRdfIdIndicatorCountdown = 0;
        }
        forceResyncImmediatePumpIndicatorCountdown -= 1.0f;
        if (0 > forceResyncImmediatePumpIndicatorCountdown) {
            forceResyncImmediatePumpIndicatorCountdown = 0;
        }
        forceResyncFutureAppliedIndicatorCountdown -= 1.0f;
        if (0 > forceResyncFutureAppliedIndicatorCountdown) {
            forceResyncFutureAppliedIndicatorCountdown = 0;
        }

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

        bool ifdLagSignificant = (localRequiredIfdId > minInputFrameIdFront) && localRequiredIfdId > (ifdLagTolerance + minInputFrameIdFront); // First comparison to avoid integer overflow 

        long latestRecvMillis = -Battle.MAX_INT;
        var ed1 = inputFrameDownsyncQ.GetLast(); 
        if (null != ed1 && ed1.t > latestRecvMillis) {
            latestRecvMillis = ed1.t;
        } 
        var ed2 = peerInputFrameUpsyncQ.GetLast();
        if (null != ed2 && ed2.t > latestRecvMillis) {
            latestRecvMillis = ed2.t;
        }

        // [WARNING] Equivalent to "(((nowMillis - latestRecvMillis)/millisPerRdf) >> INPUT_SCALE_FRAMES) >= ifdLagTolerance" where "millisPerRdf = (1000/BATTLE_DYNAMICS_FPS)" 
        long nowMillis = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        bool latestRecvMillisTooOld = (nowMillis - latestRecvMillis)*Battle.BATTLE_DYNAMICS_FPS >= 1000*(ifdLagTolerance << Battle.INPUT_SCALE_FRAMES); 

        /*
        bool recvIfdIdFrontIsFromLastResynced = (minInputFrameIdFront <= lastForceResyncedIfdId);
        bool lockstepAllowedByLastForceResync = false;
        if (recvIfdIdFrontIsFromLastResynced) {
            if (exclusivelySelfConfirmedAtLastForceResync) {
                lockstepAllowedByLastForceResync = true;
            } else if (!exclusivelySelfUnconfirmedAtLastForceResync && latestRecvMillisTooOld) {
                lockstepAllowedByLastForceResync = true;
            }
        } else {
            if (!exclusivelySelfUnconfirmedAtLastForceResync || latestRecvMillisTooOld) {
                lockstepAllowedByLastForceResync = true;
            }
        }
        */

        if (ifdLagSignificant && (0 < exclusivelySelfConfirmedLockStepQuota)) {
            /*
            [WARNING]

            We shouldn't rely solely on "immediateRollbackFrames > renderFrameLagTolerance" for lockstep decision, because upon "type#X forceConfirmation" if history update occurs, the "immediateRollbackFrames" can surge abruptly while "minInputFrameIdFront" is advanced enough.

            Similarly, "ifdLagSignificant" alone is not enough to assert the need of lockstep, because it could be a slow-ticker's slow network syndrome. 

            I'm not quite sure whether or not "latestRecvMillisTooOld" should be taken into consideration when deciding "shouldLockStep", because when "ifdLagSignificant && sendingFpsNormal && true == latestRecvMillisTooOld", we know that during "[latestRecvMillis, nowMillis]" the receiving of both TCP(WebSocket) and UDP packets doesn't do well, yet the peer(s) could still be quite advanced at "noDelayInputFrameId" locally.

            What about "ifdLagSignificant && sendingFpsNormal && false == latestRecvMillisTooOld", is it a good sign to lock step? Maybe.
        
            The bottom line is that we don't apply "lockstep" to a peer who's deemed "slow ticker" on the backend!
            */

            exclusivelySelfConfirmedLockStepQuota--;
            if (0 > exclusivelySelfConfirmedLockStepQuota) {
                exclusivelySelfConfirmedLockStepQuota = 0;
            }

            Debug.Log(String.Format("Should lock step, [localRequiredIfdId={0}, minInputFrameIdFront={1}, lastForceResyncedIfdId={2}, ifdLagTolerance={3}]; [immediateRollbackFrames={4}, rdfLagTolerance={5}]; [sendingFps={6}, srvDownsyncFps={7}, inputRateThreshold={8}]; [latestRecvMillis={9}, nowMillis={10}, latestRecvMillisTooOld={11}]; [exclusivelySelfConfirmedAtLastForceResync={12}, exclusivelySelfUnconfirmedAtLastForceResync={13}, lastForceResyncHasRollbackBurst={14}, exclusivelySelfConfirmedLockStepQuota={15}]", localRequiredIfdId, minInputFrameIdFront, lastForceResyncedIfdId, ifdLagTolerance, immediateRollbackFrames, rdfLagTolerance, sendingFps, srvDownsyncFps, inputRateThreshold, latestRecvMillis, nowMillis, latestRecvMillisTooOld, exclusivelySelfConfirmedAtLastForceResync, exclusivelySelfUnconfirmedAtLastForceResync, lastForceResyncHasRollbackBurst, exclusivelySelfConfirmedLockStepQuota));

            return (true, ifdIdLag, sendingFps, srvDownsyncFps, peerUpsyncFps, immediateRollbackFrames, lockedStepsCnt, Interlocked.Read(ref udpPunchedCnt));
		}

        exclusivelySelfConfirmedLockStepQuota = 0; // Can only be applied consecutively together with "ifdLagSignificant".
        return (false, ifdIdLag, sendingFps, srvDownsyncFps, peerUpsyncFps, immediateRollbackFrames, lockedStepsCnt, Interlocked.Read(ref udpPunchedCnt));
    }
}
