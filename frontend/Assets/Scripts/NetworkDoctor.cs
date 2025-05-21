using System;
using System.Threading;
using shared;
using System.Collections.Generic;

public class NetworkDoctor {
    private int EXPIRY_MILLIS = 1500;

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
    private FrameRingBuffer<QEle> sendingQ;
    private FrameRingBuffer<QEle> inputFrameDownsyncQ;
    private FrameRingBuffer<QEle> peerInputFrameUpsyncQ;
    int immediateRollbackFrames;
    int acLagLockedStepsCnt;
    int ifdFrontLockedStepsCnt;
    long udpPunchedCnt;

    int lastForceResyncedIfdId;
    bool exclusivelySelfConfirmedAtLastForceResync;
    bool exclusivelySelfUnconfirmedAtLastForceResync;
    bool lastForceResyncHasRollbackBurst;

    //int exclusivelySelfConfirmedLockStepQuota;
    int selfUnconfirmedLockStepSkipQuota;

    // For display on NetworkDoctorInfo panel only
    public float DEFAULT_INDICATOR_COUNTDOWN_RDF_CNT_1 = 7.0f; // 60.0f == 1 second  
    public float DEFAULT_INDICATOR_COUNTDOWN_RDF_CNT_2 = 120.0f;   
    public float DEFAULT_INDICATOR_COUNTDOWN_RDF_CNT_3 = 240.0f;   
    public float chasedToPlayerRdfIdIndicatorCountdown;
    public float forceResyncImmediatePumpIndicatorCountdown;
    public float forceResyncFutureAppliedIndicatorCountdown;

    public void Reset() {
        localRequiredIfdId = 0;
        immediateRollbackFrames = 0;
        acLagLockedStepsCnt = 0;
        ifdFrontLockedStepsCnt = 0;
        udpPunchedCnt = 0;
        inputRateThreshold = (Battle.BATTLE_DYNAMICS_FPS-1.0f) / (1 << Battle.INPUT_SCALE_FRAMES);
        lastForceResyncedIfdId = 0;
        exclusivelySelfConfirmedAtLastForceResync = false;
        exclusivelySelfUnconfirmedAtLastForceResync = false;
        lastForceResyncHasRollbackBurst = false;

        //exclusivelySelfConfirmedLockStepQuota = 0;
        selfUnconfirmedLockStepSkipQuota = 0;

        chasedToPlayerRdfIdIndicatorCountdown = 0;
        forceResyncImmediatePumpIndicatorCountdown = 0;
        forceResyncFutureAppliedIndicatorCountdown = 0;

        sendingQ.Clear();
        inputFrameDownsyncQ.Clear();
        peerInputFrameUpsyncQ.Clear();
    }

    public void LogChasedToPlayerRdfId() {
        // At 60 fps, 7 rdf is roughly 7*16.66ms = 116ms, hence if "immediateRollbackFrames" is always small enough relatively to "smallChasingRenderFramesPerUpdate & bigChasingRenderFramesPerUpdate", we should see "chasedToPlayerRdfIdIndicator" always lit. 
        chasedToPlayerRdfIdIndicatorCountdown = DEFAULT_INDICATOR_COUNTDOWN_RDF_CNT_1;
    }

    public void LogForceResyncImmediatePump() {
        forceResyncImmediatePumpIndicatorCountdown = DEFAULT_INDICATOR_COUNTDOWN_RDF_CNT_2;
    }

    public void LogForceResyncFutureApplied() {
        forceResyncFutureAppliedIndicatorCountdown = DEFAULT_INDICATOR_COUNTDOWN_RDF_CNT_3;
    }

    public void LogForceResyncedIfdId(int val, bool selfConfirmed, bool selfUnconfirmed, bool exclusivelySelfConfirmed, bool exclusivelySelfUnconfirmed, bool hasRollbackBurst, int inputFrameUpsyncDelayTolerance) {
        lastForceResyncedIfdId = val;
        exclusivelySelfConfirmedAtLastForceResync = exclusivelySelfConfirmed;
        exclusivelySelfUnconfirmedAtLastForceResync = exclusivelySelfUnconfirmed;
        lastForceResyncHasRollbackBurst = hasRollbackBurst;

        if (exclusivelySelfConfirmed) {
            //exclusivelySelfConfirmedLockStepQuota = (inputFrameUpsyncDelayTolerance << Battle.INPUT_SCALE_FRAMES);    
            selfUnconfirmedLockStepSkipQuota = 0;
        } else if (selfConfirmed) {
            selfUnconfirmedLockStepSkipQuota = 0;
        }

        if (exclusivelySelfUnconfirmed) {
            //exclusivelySelfConfirmedLockStepQuota = 0;
            selfUnconfirmedLockStepSkipQuota = (shared.Battle.BATTLE_DYNAMICS_FPS >> 2);
        } else if (selfUnconfirmed) {
            //exclusivelySelfConfirmedLockStepQuota = 0;    
            selfUnconfirmedLockStepSkipQuota = (shared.Battle.BATTLE_DYNAMICS_FPS >> 3);
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

        var nowMillis = DateTimeOffset.Now.ToUnixTimeMilliseconds(); 
        holder.t = nowMillis;

        while (0 < sendingQ.Cnt) {
            var st = sendingQ.GetFirst();
            if (st.t + EXPIRY_MILLIS < nowMillis ) {
                sendingQ.Pop();
            } else {
                break;
            } 
        }
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

        var nowMillis = DateTimeOffset.Now.ToUnixTimeMilliseconds(); 
        holder.t = nowMillis;

        while (0 < inputFrameDownsyncQ.Cnt) {
            var st = inputFrameDownsyncQ.GetFirst();
            if (st.t + EXPIRY_MILLIS < nowMillis ) {
                inputFrameDownsyncQ.Pop();
            } else {
                break;
            }
        }
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

        var nowMillis = DateTimeOffset.Now.ToUnixTimeMilliseconds(); 
        holder.t = nowMillis;

        while (0 < peerInputFrameUpsyncQ.Cnt) {
            var st = peerInputFrameUpsyncQ.GetFirst();
            if (st.t + EXPIRY_MILLIS < nowMillis ) {
                peerInputFrameUpsyncQ.Pop();
            } else {
                break;
            } 
        }
    }

    public void LogRollbackFrames(int val) {
        immediateRollbackFrames = val;
    }

    public void LogAcLagLockedStepCnt() {
        acLagLockedStepsCnt += 1;
    }

    public void LogIfdFrontLockedStepCnt() {
        ifdFrontLockedStepsCnt += 1;
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

    public (bool, int, float, float, int, int, int, long) IsTooFast(int roomCapacity, int selfJoinIndex, int[] lastIndividuallyConfirmedInputFrameId, int ifdLagTolerance, HashSet<int> disconnectedPeerJoinIndices) {
        var (sendingFps, _, peerUpsyncFps) = Stats();
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
            if (null != disconnectedPeerJoinIndices && disconnectedPeerJoinIndices.Contains(k + 1)) {
                continue;
            }
            minInputFrameIdFront = lastIndividuallyConfirmedInputFrameId[k];
        }

        int ifdIdLag = (localRequiredIfdId - minInputFrameIdFront);
        if (0 > ifdIdLag) {
            ifdIdLag = 0;
        }

        bool sendingFpsNormal = (sendingFps > inputRateThreshold);
        if (sendingFpsNormal) {
            bool ifdLagSignificant = (localRequiredIfdId >= minInputFrameIdFront) && localRequiredIfdId > (ifdLagTolerance + minInputFrameIdFront); // First comparison to avoid integer overflow 

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

            if (ifdLagSignificant) {
                /*
                 [WARNING]

                 The latest lockstep handling takes reference from `Street Fighter VI` experience, which sometimes locks the "slow ticker" as well due to the possibility that a "slow ticker" is cheating by the so called "lag switch". However, I HAVEN'T found any phenomenon like my "force-resync" here, i.e. the "red light", so I couldn't just try to imitate its strategy completely.

                 NOT every game netcode emphasizes "lockstep" so much, e.g. `KOF XV` doesn't seem to have a tangible lockstep even under terrible network (500ms+ ping) -- even if there was any lockstep applied it was much smaller than the obvious locksteps of `Street Fighter V/VI` under same network condition.
                 */
                if (0 >= selfUnconfirmedLockStepSkipQuota) {

                    return (true, ifdIdLag, sendingFps, peerUpsyncFps, immediateRollbackFrames, acLagLockedStepsCnt, ifdFrontLockedStepsCnt, Interlocked.Read(ref udpPunchedCnt));
                } else {
                    selfUnconfirmedLockStepSkipQuota -= 1;
                    if (0 > selfUnconfirmedLockStepSkipQuota) {
                        selfUnconfirmedLockStepSkipQuota = 0;
                    }
                }
            }
        }

        //exclusivelySelfConfirmedLockStepQuota = 0; // Can only be applied consecutively together with "ifdLagSignificant".
        return (false, ifdIdLag, sendingFps, peerUpsyncFps, immediateRollbackFrames, acLagLockedStepsCnt, ifdFrontLockedStepsCnt,  Interlocked.Read(ref udpPunchedCnt));
    }
}
