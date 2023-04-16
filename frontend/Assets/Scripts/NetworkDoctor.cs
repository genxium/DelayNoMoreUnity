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
    private int inputRateThreshold;
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
        inputRateThreshold = Battle.ConvertToNoDelayInputFrameId(59);

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

    public (int, int, int) Stats() {
        int sendingFps = 0,
        srvDownsyncFps = 0,
        peerUpsyncFps = 0;
        if (1 < sendingQ.Cnt) {
            var st = sendingQ.GetFirst();
            var ed = sendingQ.GetLast();
            long elapsedMillis = ed.t - st.t;
            if (null != st && null != ed && 0 < elapsedMillis) {
                sendingFps = (int)((long)(ed.j - st.i) * 1000 / elapsedMillis);
            }
        }
        if (1 < inputFrameDownsyncQ.Cnt) {
            var st = inputFrameDownsyncQ.GetFirst();
            var ed = inputFrameDownsyncQ.GetLast();
            long elapsedMillis = ed.t - st.t;
            if (null != st && null != ed && 0 < elapsedMillis) {
                srvDownsyncFps = (int)((long)(ed.j - st.i) * 1000 / elapsedMillis);
            }
        }
        if (1 < peerInputFrameUpsyncQ.Cnt) {
            var st = peerInputFrameUpsyncQ.GetFirst();
            var ed = peerInputFrameUpsyncQ.GetLast();
            long elapsedMillis = ed.t - st.t;
            if (null != st && null != ed && 0 < elapsedMillis) {
                peerUpsyncFps = (int)((long)(ed.j - st.i) * 1000 / elapsedMillis);
            }
        }
        return (sendingFps, srvDownsyncFps, peerUpsyncFps);
    }

    public (bool, int, int, int, int, int, int, long) IsTooFast(int roomCapacity, int selfJoinIndex, int[] lastIndividuallyConfirmedInputFrameId, int inputFrameUpsyncDelayTolerance) {
        var (sendingFps, srvDownsyncFps, peerUpsyncFps) = Stats();
		bool sendingFpsNormal = (sendingFps >= inputRateThreshold);
		// An outstanding lag within the "inputFrameDownsyncQ" will reduce "srvDownsyncFps", HOWEVER, a constant lag wouldn't impact "srvDownsyncFps"! In native platforms we might use PING value might help as a supplement information to confirm that the "selfPlayer" is not lagged within the time accounted by "inputFrameDownsyncQ".  
		bool recvFpsNormal = (srvDownsyncFps >= inputRateThreshold || peerUpsyncFps >= inputRateThreshold * (roomCapacity - 1));
		if (sendingFpsNormal && recvFpsNormal) {
			int minInputFrameIdFront = Battle.MAX_INT;
			for (int k = 0; k < roomCapacity; ++k) {
				if (k + 1 == selfJoinIndex) continue; // Don't count self in
				if (lastIndividuallyConfirmedInputFrameId[k] >= minInputFrameIdFront) continue;
				minInputFrameIdFront = lastIndividuallyConfirmedInputFrameId[k];
			}
			if ((inputFrameIdFront - minInputFrameIdFront) > (inputFrameUpsyncDelayTolerance+3)) {
				Debug.Log(String.Format("Should lock step, inputFrameIdFront={0}, minInputFrameIdFront={1}, inputFrameUpsyncDelayTolerance={2}, sendingFps={3}, srvDownsyncFps={4}, inputRateThreshold={5}", inputFrameIdFront, minInputFrameIdFront, inputFrameUpsyncDelayTolerance, sendingFps, srvDownsyncFps, inputRateThreshold));
				return (true, inputFrameIdFront, sendingFps, srvDownsyncFps, peerUpsyncFps, immediateRollbackFrames, lockedStepsCnt, Interlocked.Read(ref udpPunchedCnt));
			}
		}

        return (false, inputFrameIdFront, sendingFps, srvDownsyncFps, peerUpsyncFps, immediateRollbackFrames, lockedStepsCnt, Interlocked.Read(ref udpPunchedCnt));
    }
}
