using Google.Protobuf;
using Google.Protobuf.Collections;
using shared;
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

public class UdpSessionManager {
    private static UdpSessionManager _instance;

    public static UdpSessionManager Instance {
        get {
            if (null == _instance) _instance = new UdpSessionManager();
            return _instance;
        }
    }

    /**
     As potentially more threads are accessing "senderBuffer" than that of WsSessionManager, using the actual "ConcurrentQueue" type here.

    ################################################################################################################################################
    UPDATE 2024-04-17
    ################################################################################################################################################

    The "ConcurrentQueue" of "senderBuffer" is changed into an "BlockingCollection", see comments in "WsSessionManager" for more information.
     */
    public BlockingCollection<WsReq> senderBuffer;
    public ConcurrentQueue<WsReq> recvBuffer;
    private IPEndPoint[] peerUdpEndPointList;
    private PeerUdpAddr[] peerUdpAddrList;
    private long[] peerUdpEndPointPunched;
    private UdpClient udpSession;
    private WsReq serverHolePuncher, peerHolePuncher;
    private int sendBufferReadTimeoutMillis = 512;
    private uint localSeqNo = 0;

    private UdpSessionManager() {
        senderBuffer = new BlockingCollection<WsReq>();
        recvBuffer = new ConcurrentQueue<WsReq>();
    }

    public void ResetUdpClient(int roomCapacity, int selfJoinIndex, RepeatedField<PeerUdpAddr> initialPeerAddrList, WsReq theServerHolePuncher, WsReq thePeerHolePuncher, CancellationToken sessionCancellationToken) {
        Debug.Log($"ResetUdpClient#1: roomCapacity={roomCapacity}, thread id={Thread.CurrentThread.ManagedThreadId}.");
        serverHolePuncher = theServerHolePuncher;
        peerHolePuncher = thePeerHolePuncher;
        Debug.Log($"ResetUdpClient#2: roomCapacity={roomCapacity}, thread id={Thread.CurrentThread.ManagedThreadId}.");
        localSeqNo = 0;
        UpdatePeerAddr(roomCapacity, selfJoinIndex, initialPeerAddrList);
    }

    public async Task OpenUdpSession(int roomCapacity, int selfJoinIndex, CancellationToken sessionCancellationToken) {
        try {
            ++localSeqNo;
            Debug.Log($"OpenUdpSession#1: roomCapacity={roomCapacity}, thread id={Thread.CurrentThread.ManagedThreadId}...");
            peerHolePuncher.SeqNo = localSeqNo;
            serverHolePuncher.SeqNo = localSeqNo;
            udpSession = new UdpClient(port: 0);
            /*
            try {
                udpSession.AllowNatTraversal(true);
            } catch (Exception innerEx) {
                Debug.LogWarning($"OpenUdpSession allow NAT traversal exception: {innerEx}, thread id={Thread.CurrentThread.ManagedThreadId}.");
            }
            */
            var localUdpEndpoint = (IPEndPoint)udpSession.Client.LocalEndPoint;
            // [WARNING] UdpClient class in .NET Standard 2.1 doesn't support CancellationToken yet! See https://learn.microsoft.com/en-us/dotnet/api/system.net.sockets.udpclient?view=netstandard-2.1 for more information. The cancellationToken here is still used to keep in pace with the WebSocket session, i.e. is WebSocket Session is closed, then UdpSession should be closed too.
            Debug.Log($"OpenUdpSession#2: roomCapacity={roomCapacity}, localUdpEndpoint={localUdpEndpoint}, thread id={Thread.CurrentThread.ManagedThreadId}.");
            UdpSessionManager.Instance.PunchBackendUdpTunnel(sessionCancellationToken); // [WARNING] After clearing of "senderBuffer"
            await Task.WhenAll(Receive(udpSession, roomCapacity, sessionCancellationToken), Send(udpSession, roomCapacity, selfJoinIndex, sessionCancellationToken));
            Debug.Log($"Both UdpSession 'Receive' and 'Send' tasks are ended @localSeqNo={localSeqNo}.");
        } catch (Exception ex) {
            Debug.LogError($"Error opening udpSession @localSeqNo={localSeqNo}: {ex}");
        }
    }

    private async Task Send(UdpClient udpSession, int roomCapacity, int selfJoinIndex, CancellationToken sessionCancellationToken) {
        // TODO: Make this method thread-safe for "this.peerUdpAddrList"
        Debug.Log($"Starts udpSession 'Send' loop, now senderBuffer.Count={senderBuffer.Count}");
        WsReq toSendObj;
        try {
            while (!sessionCancellationToken.IsCancellationRequested) {
                if (senderBuffer.TryTake(out toSendObj, sendBufferReadTimeoutMillis, sessionCancellationToken)) {
                    var toSendBuffer = toSendObj.ToByteArray();
                    if (toSendObj.Act == serverHolePuncher.Act) {
                        if (null == peerUdpEndPointList[Battle.MAGIC_JOIN_INDEX_SRV_UDP_TUNNEL]) {
                            Debug.LogWarning($"udpSession cannot punch SRV_UDP_TUNNEL#1 @localSeqNo={localSeqNo}: null endPoint");
                            continue;
                        }
                        Debug.Log($"udpSession sending serverHolePuncher to endPoint={peerUdpEndPointList[Battle.MAGIC_JOIN_INDEX_SRV_UDP_TUNNEL]} @localSeqNo={localSeqNo}");
                        await udpSession.SendAsync(toSendBuffer, toSendBuffer.Length, peerUdpEndPointList[Battle.MAGIC_JOIN_INDEX_SRV_UDP_TUNNEL]);
                    } else if (toSendObj.Act == peerHolePuncher.Act) {
                        for (int otherJoinIndex = 1; otherJoinIndex <= roomCapacity; otherJoinIndex++) {
                            if (otherJoinIndex == selfJoinIndex) continue;
                            if (null == peerUdpEndPointList[otherJoinIndex]) {
                                //Debug.Log($"udpSession cannot punch otherJoinIndex={otherJoinIndex}#1: null endPoint");
                                continue;
                            }
                            // [WARNING] Deliberately keep sending even if "1 == Interlocked.Read(ref peerUdpEndPointPunched[i])"
                            //Debug.Log($"udpSession sending peerHolePuncher to otherJoinIndex={otherJoinIndex}, endPoint={peerUdpEndPointList[otherJoinIndex]}");
                            await udpSession.SendAsync(toSendBuffer, toSendBuffer.Length, peerUdpEndPointList[otherJoinIndex]);
                        }
                    } else {
                        int successPeerCnt = 0;
                        for (int i = 1; i <= roomCapacity; i++) {
                            if (i == selfJoinIndex) continue;
                            if (null == peerUdpEndPointList[i]) continue;
                            if (0 == Interlocked.Read(ref peerUdpEndPointPunched[i])) continue;
                            // Debug.Log(String.Format("udpSession sending {0} to punched peer {1}", toSendObj, peerUdpEndPointList[i]));
                            await udpSession.SendAsync(toSendBuffer, toSendBuffer.Length, peerUdpEndPointList[i]);
                            if (1 == Interlocked.Read(ref peerUdpEndPointPunched[i])) {
                                successPeerCnt++;
                            }
                        }

                        if (successPeerCnt + 1 < roomCapacity) {
                            // Debug.Log(String.Format("udpSession sending {0} to UdpTunnel {1}", toSendObj, peerUdpEndPointList[shared.Battle.MAGIC_JOIN_INDEX_SRV_UDP_TUNNEL]));
                            await udpSession.SendAsync(toSendBuffer, toSendBuffer.Length, peerUdpEndPointList[Battle.MAGIC_JOIN_INDEX_SRV_UDP_TUNNEL]);
                        }
                    }
                }
            }
        } catch (ObjectDisposedException ex1) {
    
        } catch (Exception ex) {
            Debug.LogWarning($"UdpSession is stopping for 'Send' upon exception @localSeqNo={localSeqNo}; ex={ex}");
        } finally {
            while (senderBuffer.TryTake(out _, sendBufferReadTimeoutMillis, sessionCancellationToken)) { }
            Debug.Log($"Ends udpSession 'Send' loop @localSeqNo={localSeqNo}");
        }
    }

    private void markPunched(int fromJoinIndex, int roomCapacity) {
        Interlocked.Exchange(ref peerUdpEndPointPunched[fromJoinIndex], 1);
        int newUdpPunchedCnt = 0;
        for (int i = 1; i <= roomCapacity; i++) {
            if (0 == Interlocked.Read(ref peerUdpEndPointPunched[i])) continue; // Automatically excludes backend udp tunnel and self.
            newUdpPunchedCnt++;
        }
        NetworkDoctor.Instance.LogUdpPunchedCnt(newUdpPunchedCnt);
    }

    private async Task Receive(UdpClient udpSession, int roomCapacity, CancellationToken sessionCancellationToken) {
        Debug.Log($"Starts udpSession 'Receive' loop @localSeqNo={localSeqNo}");
        try {
            while (!sessionCancellationToken.IsCancellationRequested) {
                var recvResult = await udpSession.ReceiveAsync(); // by experiment, "udpSession.Close()" would unblock it even at the absence of a cancellation token!
                WsReq req = WsReq.Parser.ParseFrom(recvResult.Buffer);
                switch (req.Act) {
                    case Battle.UPSYNC_MSG_ACT_HOLEPUNCH_PEER_UDP_ADDR:
                        Debug.Log($"Received direct holePuncher from joinIndex: {req.JoinIndex}, from addr={recvResult.RemoteEndPoint}");
                        markPunched(req.JoinIndex, roomCapacity);
                        break;
                    case Battle.UPSYNC_MSG_ACT_PLAYER_CMD:
                        if (0 == Interlocked.Read(ref peerUdpEndPointPunched[req.JoinIndex]) && !recvResult.RemoteEndPoint.Address.Equals(peerUdpEndPointList[Battle.MAGIC_JOIN_INDEX_SRV_UDP_TUNNEL].Address)) {
                            // [WARNING] Any packet not from the backend udp tunnel can be effectively a holepunch!
                            Debug.Log($"Received effective holePuncher from joinIndex: {req.JoinIndex}, from addr={recvResult.RemoteEndPoint}");
                            markPunched(req.JoinIndex, roomCapacity);
                        }
                        recvBuffer.Enqueue(req);
                        break;
                }
            }
        } catch (ObjectDisposedException ex1) {

        } catch (Exception ex) {
            Debug.LogWarning($"UdpSession is stopping for 'Receive' upon exception @localSeqNo={localSeqNo}; ex={ex}");
        } finally {
            recvBuffer.Clear();
            Debug.Log($"Ends udpSession 'Receive' loop @localSeqNo={localSeqNo}");
        }
    }

    public void UpdatePeerAddr(int roomCapacity, int selfJoinIndex, RepeatedField<PeerUdpAddr> newPeerUdpAddrList) {
        // TODO: Make this method thread-safe for "this.peerUdpAddrList"
        if (null == peerUdpEndPointList || roomCapacity+1 >= peerUdpEndPointList.Length
            || null == peerUdpEndPointPunched || roomCapacity + 1 >= peerUdpEndPointPunched.Length) {
            peerUdpEndPointList = new IPEndPoint[roomCapacity + 1];
            peerUdpAddrList = new PeerUdpAddr[roomCapacity + 1];
            peerUdpEndPointPunched = new long[roomCapacity + 1];
        }
        int updatedCnt = 0;
        for (int i = 0; i <= roomCapacity; i++) {
            if (i == selfJoinIndex) continue;
            if (i > newPeerUdpAddrList.Count) {
                Debug.LogWarning($"newPeerUdpAddrList {newPeerUdpAddrList} doesn't have i={i} indice; breaking");
                break;
            }
            if (0 < i && String.IsNullOrEmpty(newPeerUdpAddrList[i].Ip)) continue;
            if (null != peerUdpAddrList[i] && newPeerUdpAddrList[i].SeqNo <= peerUdpAddrList[i].SeqNo) {
                // Ignore obsolete packets
                continue;
            }
            IPAddress newEndpointIp;
            if (!IPAddress.TryParse(Battle.MAGIC_JOIN_INDEX_SRV_UDP_TUNNEL == i ? Env.Instance.getHostnameOnly() : newPeerUdpAddrList[i].Ip, out newEndpointIp)) {
                var msg = $"Invalid newEndpointIpStr {newPeerUdpAddrList[i].Ip} for joinIndex={i} @localSeqNo={localSeqNo}";
                Debug.LogError(msg);
                throw new FormatException(msg);
            }
            if (null != peerUdpEndPointList[i] && null != newEndpointIp && newEndpointIp.Equals(peerUdpEndPointList[i].Address) && newPeerUdpAddrList[i].Port == peerUdpEndPointList[i].Port) continue;
            try {
                peerUdpEndPointList[i] = new IPEndPoint(newEndpointIp, newPeerUdpAddrList[i].Port);
                peerUdpAddrList[i] = new PeerUdpAddr(newPeerUdpAddrList[i]);
            } catch (Exception ex) {
                Debug.LogError($"Error updating peerUdpEndPointList at i={i} indice with newPeerUdpAddrList={newPeerUdpAddrList}; ex={ex} breaking");
                break;
            }
            
            updatedCnt++;
        } 

        Debug.Log($"Ends updatePeerAddr with newPeerUdpAddrList={newPeerUdpAddrList}, updatedCnt={updatedCnt} @localSeqNo={localSeqNo}");
    }

    public void CloseUdpSession() {
        if (null != udpSession) {
            udpSession.Close();
            // "UdpClient.Close()" might effectively dispose the underlying resources, but just to be safe I'm still explicitly calling "Dispose()" here.
            try {
                udpSession.Dispose();
            } catch (Exception ex) {
                Debug.LogWarning($"Error during `CloseUdpSession`: {ex}");
            }
        }
    }

    public void PunchAllPeers(CancellationToken cancellationToken) {
        _ = Task.Run(async () => {
            int c = 8;
            while (0 <= Interlocked.Decrement(ref c)) {
                //Debug.LogFormat("Enqueues peerHolePuncher c={0}");
                senderBuffer.Add(peerHolePuncher);
                await Task.Delay(50 + c * 250, cancellationToken);
            }
        }, cancellationToken);
    }

    public void PunchBackendUdpTunnel(CancellationToken cancellationToken) {
        // Will trigger DOWNSYNC_MSG_ACT_PEER_UDP_ADDR for all players, including itself
        _ = Task.Run(async () => {
            int c = 8;
            while (0 <= Interlocked.Decrement(ref c)) {
                //Debug.LogFormat("Enqueues serverHolePuncher c={0}", c);
                senderBuffer.Add(serverHolePuncher);
                await Task.Delay(50 + c * 250, cancellationToken);
            }
        }, cancellationToken);
    }

    ~UdpSessionManager() {
        if (null != senderBuffer) senderBuffer.Dispose();
    }
}
