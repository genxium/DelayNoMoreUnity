using UnityEngine;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Net.WebSockets;
using shared;
using Google.Protobuf;

public class WsSessionManager {
    // Reference https://github.com/paulbatum/WebSocket-Samples/blob/master/HttpListenerWebSocketEcho/Client/Client.cs
    private const int receiveChunkSize = 2048; // The "RoomDownsyncFrame" would be 1900+ bytes.

    /**
    I'm aware of that "C# ConcurrentQueue" is lock-free, thus safe to be accessed from the MainThread during "Update()" without introducing significant graphic lags. Reference https://devblogs.microsoft.com/pfxteam/faq-are-all-of-the-new-concurrent-collections-lock-free/.

    However a normal "Queue" is used here while it's still considered thread-safe in this particular case (even for multi-core cache, for why multi-core cache could be a source of data contamination in multithread context, see https://app.yinxiang.com/fx/6f48c146-7db8-4a64-bdf0-3c874cd9290d). A "Queue" is in general NOT thread-safe, but when "bool TryDequeue(out T)" is always called in "thread#A" while "void Enqueue(T)" being always called in "thread#B", we're safe, e.g. "thread#A == MainThread && thread#B == WebSocketThread" or viceversa. 
        
    I'm not using "RecvRingBuff" https://github.com/genxium/DelayNoMore/blob/v1.0.14-cc/frontend/build-templates/jsb-link/frameworks/runtime-src/Classes/ring_buff.cpp because WebSocket is supposed to be preserving send/recv order at all time.
    */
    public Queue<WsReq> senderBuffer;
    public Queue<WsResp> recvBuffer;
    private string authToken;
    private int playerId = Battle.TERMINATING_PLAYER_ID;
    private int speciesId;

    public int GetPlayerId() {
        return playerId;
    }

    private static WsSessionManager _instance;

    public static WsSessionManager Instance {
        get {
            if (null == _instance) _instance = new WsSessionManager();
            return _instance;
        }
    }

    private WsSessionManager() {
        ClearCredentials();
        senderBuffer = new Queue<WsReq>();
        recvBuffer = new Queue<WsResp>();
    }

    public void SetCredentials(string theAuthToken, int thePlayerId) {
        authToken = theAuthToken;
        playerId = thePlayerId;
    }

    public void SetSpeciesId(int theSpeciesId) {
        speciesId = theSpeciesId;
    }
    
    public void ClearCredentials() {
        SetCredentials(null, Battle.TERMINATING_PLAYER_ID);
        speciesId = 0;
    }

    public async Task ConnectWsAsync(string wsEndpoint, CancellationToken cancellationToken, CancellationTokenSource cancellationTokenSource) {
        if (null == authToken || shared.Battle.TERMINATING_PLAYER_ID == playerId) {
            Debug.Log(String.Format("ConnectWs not having enough credentials, authToken={0}, playerId={1}", authToken, playerId));
            return;
        }
        senderBuffer.Clear();
        recvBuffer.Clear();
        string fullUrl = wsEndpoint + String.Format("?authToken={0}&playerId={1}&speciesId={2}", authToken, playerId, speciesId);
        using (ClientWebSocket ws = new ClientWebSocket()) {
            try {
                await ws.ConnectAsync(new Uri(fullUrl), cancellationToken);
                Debug.Log("Ws session is opened");
                var openMsg = new WsResp {
                    Ret = ErrCode.Ok,
                    Act = shared.Battle.DOWNSYNC_MSG_WS_OPEN
                };
                recvBuffer.Enqueue(openMsg);
                await Task.WhenAll(Receive(ws, cancellationToken, cancellationTokenSource), Send(ws, cancellationToken));
                Debug.Log(String.Format("Both 'Receive' and 'Send' tasks are ended."));
            } catch (OperationCanceledException ocEx) {
                Debug.LogWarning(String.Format("WsSession is cancelled for 'ConnectAsync'; ocEx.Message={0}", ocEx.Message));
            } catch (Exception ex) {
                Debug.LogWarning(String.Format("WsSession is stopped by exception; ex={0}", ex));
            } finally {
                var closeMsg = new WsResp {
                    Ret = ErrCode.Ok,
                    Act = shared.Battle.DOWNSYNC_MSG_WS_CLOSED
                };
                recvBuffer.Enqueue(closeMsg);
                Debug.LogWarning(String.Format("Enqueued DOWNSYNC_MSG_WS_CLOSED for main thread."));
            }
        }
    }

    private async Task Send(ClientWebSocket ws, CancellationToken cancellationToken) {
        Debug.Log(String.Format("Starts 'Send' loop, ws.State={0}", ws.State));
        WsReq toSendObj;
        try {
            while (WebSocketState.Open == ws.State) {
                while (senderBuffer.TryDequeue(out toSendObj)) {
                    cancellationToken.ThrowIfCancellationRequested();
                    //Debug.Log("Ws session send: before");
                    await ws.SendAsync(new ArraySegment<byte>(toSendObj.ToByteArray()), WebSocketMessageType.Binary, true, cancellationToken);
                    //Debug.Log(String.Format("'Send' loop, sent {0} bytes", toSendObj.ToByteArray().Length));
                }
                cancellationToken.ThrowIfCancellationRequested();
            }
        } catch (OperationCanceledException ocEx) {
            Debug.LogWarning(String.Format("WsSession is cancelled for 'Send'; ocEx.Message={0}", ocEx.Message));
        } catch (Exception ex) {
            Debug.LogWarning(String.Format("WsSession is stopping for 'Send' upon exception; ex={0}", ex));
        } finally {
            Debug.Log(String.Format("Ends 'Send' loop, ws.State={0}", ws.State));
        }
    }

    private async Task Receive(ClientWebSocket ws, CancellationToken cancellationToken, CancellationTokenSource cancellationTokenSource) {
        Debug.Log(String.Format("Starts 'Receive' loop, ws.State={0}, cancellationToken.IsCancellationRequested={1}", ws.State, cancellationToken.IsCancellationRequested));
        byte[] byteBuff = new byte[receiveChunkSize];
        try {
            while (WebSocketState.Open == ws.State) {
                //Debug.Log("Ws session recv: before");
                var result = await ws.ReceiveAsync(new ArraySegment<byte>(byteBuff), cancellationToken);
                //Debug.Log("Ws session recv: after");
                if (WebSocketMessageType.Close == result.MessageType) {
                    Debug.Log(String.Format("WsSession is asked by remote to close in 'Receive'"));
                    if (!cancellationToken.IsCancellationRequested) {
                        cancellationTokenSource.Cancel(); // To cancel the "Send" loop
                    }
                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                    Debug.LogWarning(String.Format("WsSession is closed in 'Receive'#1, ws.State={0}", ws.State));
                    return;
                } else {
                    try {
                        WsResp resp = WsResp.Parser.ParseFrom(byteBuff, 0, result.Count);
                        recvBuffer.Enqueue(resp);
                    } catch (Exception pbEx) {
                        Debug.LogWarning(String.Format("Protobuf parser exception is caught for 'Receive'; ex.Message={0}", pbEx.Message));
                    }
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }
        } catch (OperationCanceledException ocEx) {
            Debug.LogWarning(String.Format("WsSession is cancelled for 'Receive'; ocEx.Message={0}", ocEx.Message));
        } catch (Exception ex) {
            Debug.LogError(String.Format("WsSession is stopping for 'Receive' upon exception; ex={0}", ex));
        } finally {
            Debug.LogWarning(String.Format("Ends 'Receive' loop, ws.State={0}", ws.State));
        }
    }

}
