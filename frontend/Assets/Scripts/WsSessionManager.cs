using UnityEngine;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Net.WebSockets;
using System.Linq;
using Unity.VisualScripting;

public class WsSessionManager : MonoBehaviour {
    // Reference https://github.com/paulbatum/WebSocket-Samples/blob/master/HttpListenerWebSocketEcho/Client/Client.cs
    private const int sendChunkSize = 256;
    private const int receiveChunkSize = 64;

    /**
    A "Queue" is in general NOT thread-safe, but when "bool TryDequeue(out T)" is always called in "thread#A" while "void Enqueue(T)" being always called in "thread#B", we're safe, e.g. "thread#A == UIThread && thread#B == WebSocketThread" or viceversa. 
        
    I'm not using "RecvRingBuff" from https://github.com/genxium/DelayNoMore/blob/v1.0.14/frontend/build-templates/jsb-link/frameworks/runtime-src/Classes/ring_buff.cpp because WebSocket is supposed to be preserving send/recv order at all time.
    */
    public Queue<byte[]> senderBuffer, recvBuffer; 

    private static WsSessionManager ins = null;

    public static WsSessionManager Instance {
        get {
            if (null == ins) {
                ins = new WsSessionManager();
            }
            return ins;
        }
    }

    string authToken { get;set; }
    int playerId { get; set; } = shared.Battle.TERMINATING_PLAYER_ID;

    public async Task ConnectWs(string wsEndpoint) {
        if (null == authToken || shared.Battle.TERMINATING_PLAYER_ID == playerId) {
            Debug.Log(String.Format("ConnectWs not having enough credentials, authToken={0}, playerId={1}", authToken, playerId));
            return;
        }
        senderBuffer.Clear();
        recvBuffer.Clear();
        string fullUrl = wsEndpoint + String.Format("?authToken={0}&playerId={1}", authToken, playerId);
        using (ClientWebSocket ws = new ClientWebSocket()) {
            await ws.ConnectAsync(new Uri(fullUrl), CancellationToken.None);
            await Task.WhenAll(Receive(ws), Send(ws));
        }
    }

    private async Task Send(ClientWebSocket ws) {
        byte[] byteBuff = new byte[sendChunkSize];
        while (ws.State == WebSocketState.Open) {
            while (senderBuffer.TryDequeue(out byteBuff)) {
                await ws.SendAsync(new ArraySegment<byte>(byteBuff), WebSocketMessageType.Binary, false, CancellationToken.None);
            }
        }
    }

    private async Task Receive(ClientWebSocket ws) {
        byte[] byteBuff = new byte[receiveChunkSize];
        while (ws.State == WebSocketState.Open) {                
            var result = await ws.ReceiveAsync(new ArraySegment<byte>(byteBuff), CancellationToken.None);
            if (result.MessageType == WebSocketMessageType.Close) {
                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
            } else {
                byte[] clone = byteBuff.ToArray();
                recvBuffer.Enqueue(clone);               
            }
        }
    }

}
