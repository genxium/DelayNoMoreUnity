using UnityEngine;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Net.WebSockets;
using System.Linq;

public class WsSessionManager {
    public delegate void OnWsSessionEvtCallbackType(int resultCode);

    // Reference https://github.com/paulbatum/WebSocket-Samples/blob/master/HttpListenerWebSocketEcho/Client/Client.cs
    private const int sendChunkSize = 256;
    private const int receiveChunkSize = 64;

    /**
    A "Queue" is in general NOT thread-safe, but when "bool TryDequeue(out T)" is always called in "thread#A" while "void Enqueue(T)" being always called in "thread#B", we're safe, e.g. "thread#A == UIThread && thread#B == WebSocketThread" or viceversa. 
        
    I'm not using "RecvRingBuff" from https://github.com/genxium/DelayNoMore/blob/v1.0.14/frontend/build-templates/jsb-link/frameworks/runtime-src/Classes/ring_buff.cpp because WebSocket is supposed to be preserving send/recv order at all time.
    */
    public Queue<byte[]> senderBuffer, recvBuffer;
    private string authToken;
    private int playerId = shared.Battle.TERMINATING_PLAYER_ID;

    private static WsSessionManager _instance;

    public static WsSessionManager Instance {
        get {
            if (null == _instance) _instance = new WsSessionManager();
            return _instance;
        }
    }

    private WsSessionManager() {
        ClearCredentials();
        senderBuffer = new Queue<byte[]>();
        recvBuffer = new Queue<byte[]>();
    }

    public void SetCredentials(string theAuthToken, int thePlayerId) {
        authToken = theAuthToken;
        playerId = thePlayerId;
    }

    public void ClearCredentials() {
        SetCredentials(null, shared.Battle.TERMINATING_PLAYER_ID);
    }

    public async void ConnectWs(string wsEndpoint, CancellationToken cancellationToken, OnWsSessionEvtCallbackType onOpen, OnWsSessionEvtCallbackType onClose) {
        if (null == authToken || shared.Battle.TERMINATING_PLAYER_ID == playerId) {
            Debug.Log(String.Format("ConnectWs not having enough credentials, authToken={0}, playerId={1}", authToken, playerId));
            return;
        }
        senderBuffer.Clear();
        recvBuffer.Clear();
        string fullUrl = wsEndpoint + String.Format("?authToken={0}&playerId={1}", authToken, playerId);
        using (ClientWebSocket ws = new ClientWebSocket()) {
            try {
                await ws.ConnectAsync(new Uri(fullUrl), cancellationToken);
                UnityEngine.WSA.Application.InvokeOnUIThread(() => {
                    onOpen(shared.ErrCode.Ok);
                }, true);
                await Task.WhenAll(Receive(ws, cancellationToken), Send(ws, cancellationToken));
            } catch (OperationCanceledException ocEx) {
                Debug.LogWarning(String.Format("WsSession is cancelled for 'ConnectAsync'; ocEx.Message={0}", ocEx.Message));
            }
        }
        UnityEngine.WSA.Application.InvokeOnUIThread(() => {
            onClose(shared.ErrCode.Ok);
        }, true);
    }

    private async Task Send(ClientWebSocket ws, CancellationToken cancellationToken) {
        byte[] byteBuff = new byte[sendChunkSize];
        try {
            while (ws.State == WebSocketState.Open) {
                while (senderBuffer.TryDequeue(out byteBuff)) {
                    await ws.SendAsync(new ArraySegment<byte>(byteBuff), WebSocketMessageType.Binary, false, cancellationToken);
                }
            }
        } catch (OperationCanceledException ocEx) {
            Debug.LogWarning(String.Format("WsSession is cancelled for 'Send'; ocEx.Message={0}", ocEx.Message));
        }
        
    }

    private async Task Receive(ClientWebSocket ws, CancellationToken cancellationToken) {
        byte[] byteBuff = new byte[receiveChunkSize];
        try {
            while (ws.State == WebSocketState.Open) {
                var result = await ws.ReceiveAsync(new ArraySegment<byte>(byteBuff), cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close) {
                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                } else {
                    byte[] clone = byteBuff.ToArray();
                    recvBuffer.Enqueue(clone);
                }
            }
        } catch (OperationCanceledException ocEx) {
            Debug.LogWarning(String.Format("WsSession is cancelled for 'Receive'; ocEx.Message={0}", ocEx.Message));
        }
    }

}
