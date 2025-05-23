using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Net.WebSockets;
using shared;
using Google.Protobuf;
using UnityEngine.Networking;
using Newtonsoft.Json;
using System.Collections.Concurrent;

public class WsSessionManager {
    // Reference https://github.com/paulbatum/WebSocket-Samples/blob/master/HttpListenerWebSocketEcho/Client/Client.cs

    /**
    I'm aware of that "C# ConcurrentQueue" is lock-free, thus safe to be accessed from the MainThread during "Update()" without introducing significant graphic lags. Reference https://devblogs.microsoft.com/pfxteam/faq-are-all-of-the-new-concurrent-collections-lock-free/.

    However a normal "Queue" is used here while it's still considered thread-safe in this particular case (even for multi-core cache, for why multi-core cache could be a source of data contamination in multithread context, see https://app.yinxiang.com/fx/6f48c146-7db8-4a64-bdf0-3c874cd9290d). A "Queue" is in general NOT thread-safe, but when "bool TryDequeue(out T)" is always called in "thread#A" while "void Enqueue(T)" being always called in "thread#B", we're safe, e.g. "thread#A == MainThread && thread#B == WebSocketThread" or viceversa. 
        
    I'm not using "RecvRingBuff" https://github.com/genxium/DelayNoMore/blob/v1.0.14-cc/frontend/build-templates/jsb-link/frameworks/runtime-src/Classes/ring_buff.cpp because WebSocket is supposed to be preserving send/recv order at all time.

    ################################################################################################################################################
    UPDATE 2024-04-16
    ################################################################################################################################################

    The normal "Queue" is planned to be changed into an "AsyncQueue" such that I can call "DequeueAsync" without spurious breaking when empty. All methods of "DequeueAsync" are thread-safe, but I'm not sure whether or not they're lock-free as well. Moreover, I haven't found a way to use https://learn.microsoft.com/en-us/dotnet/api/microsoft.visualstudio.threading.asyncqueue-1?view=visualstudiosdk-2022 yet.

    Fairly speaking, the current

    ```
    while (wsSession is still open && not cancelled) { 
        while (senderBuffer.TryDequeue(out toSendObj)) {
            ...
            // process "toSendObj"
            ...
        }
        // having spurious breaking when empty here would just continue to another loop
    }
    ```

    implementation is immune to spurious breaking when empty too, but quite inefficient in CPU usage.

    ################################################################################################################################################
    UPDATE 2024-04-17
    ################################################################################################################################################

    The normal "Queue" of "senderBuffer" is changed into an "BlockingCollection" whose default underlying data structure is "ConcurrentQueue". The "recvBuffer" is left untouched because
    - its "TryDequeue" is only called by "OnlineMapController.pollAndHandleWsRecvBuffer" which wouldn't cause spurious breaking when empty (i.e. upon transient empty "recvBuffer", it's OK to poll in the next render frame), and
    - its "Enqueue" is driven by "wsSession.ReceiveAsync" which is cancellable.

    To my understanding, "BlockingCollection.TryTake(out dst, timeout, cancellationToken)" is equivalent to "dst = AsyncQueue.DequeueAsync(cancellationToken).Result", at least for my use case.
    */
    public BlockingCollection<WsReq> senderBuffer;
    private int sendBufferReadTimeoutMillis = 512;
    public Queue<WsResp> recvBuffer;
    private string uname;
    private string authToken; // cached for auto-login (TODO: link "save/load" of this value with persistent storage)
    private string playerId = null;
    private uint speciesId = Battle.SPECIES_NONE_CH;
    private int roomId = Battle.ROOM_ID_NONE;
    private bool forReentry = false;
    private bool inArenaPracticeMode = false;
    private int incCnt = 0;
    public bool getInArenaPracticeMode() {
        return inArenaPracticeMode;
    }

    public void setInArenaPracticeMode(bool val) {
        inArenaPracticeMode = val;
    }

    public string GetPlayerId() {
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
        senderBuffer = new BlockingCollection<WsReq>();
        recvBuffer = new Queue<WsResp>();
    }

    public void SetCredentials(string theUname, string theAuthToken, string thePlayerId) {
        uname = theUname; 
        authToken = theAuthToken;
        playerId = thePlayerId;
    }

    public void SetSpeciesId(uint theSpeciesId) {
        speciesId = theSpeciesId;
    }

    public uint GetSpeciesId() {
        return speciesId;
    }

    public string GetUname() {
        return uname;
    }

    public void SetRoomId(int theRoomId) {
        roomId = theRoomId;
    }

    public int GetRoomId() {
        return roomId;
    }

    public void SetForReentry(bool val) {
        forReentry = val;
    }

    public bool GetForReentry() {
        return forReentry;
    }

    public void ClearCredentials() {
        SetCredentials(null, null, null);
        speciesId = Battle.SPECIES_NONE_CH;
        roomId = Battle.ROOM_ID_NONE;
        forReentry = false;
        incCnt = 0;
    }

    public bool IsPossiblyLoggedIn() {
        return (null != playerId);
    }

    private string battlesrvEndpoint = Env.Instance.getWsEndpoint();
    public async Task ConnectWsAsync(CancellationToken cancellationToken, CancellationTokenSource cancellationTokenSource, CancellationTokenSource guiCanProceedSignalSource) {
        if (null == authToken || null == playerId) {
            string errMsg = $"ConnectWs not having enough credentials, authToken={authToken}, playerId={playerId}, please go back to LoginPage!";
            throw new Exception(errMsg);
        }
        ++incCnt;
        string fullUrl = battlesrvEndpoint + $"?authToken={Uri.EscapeDataString(authToken)}&playerId={playerId}&speciesId={speciesId}&roomId={roomId}&forReentry={forReentry}&&incCnt={incCnt}";
        Debug.Log($"About to connect Ws to {fullUrl}, please wait...");
        using (ClientWebSocket ws = new ClientWebSocket()) {
            try {
                /**
                 * The previous "System.Threading.Timer" implementation has a caveat: if "OnlineMapController.wsTask" is still in "WaitingForActivation" status when cancelled, the whole GUI will be stuck.
                 */
                var successWithoutTimeout = ws.ConnectAsync(new Uri(fullUrl), cancellationToken).Wait(5000, cancellationToken);
                if (!successWithoutTimeout) {
                    Debug.LogWarning($"Ws connection to {fullUrl} timed out @incCnt={incCnt}, would not start 'Receive' or 'Send' tasks.");
                    if (!cancellationToken.IsCancellationRequested) {
                        cancellationTokenSource.Cancel();
                        Debug.LogWarning($"Ws connection to {fullUrl} timed out @incCnt={incCnt}, cancelled");
                    }
                } else {
                    var openMsg = new WsResp {
                        Ret = ErrCode.Ok,
                        Act = Battle.DOWNSYNC_MSG_WS_OPEN
                    };
                    recvBuffer.Enqueue(openMsg);
                    guiCanProceedSignalSource.Cancel();
                    /*
                    [WARNING] 
                    C# ClientWebSocket caveats

                    - Even upon proactive network adapter switching off, there is no in-time event callback to favor "immediate UI prompt to remind the player", hence frontend might have to wait for some extreme timeout callback before prompting to player.
                    
                    - Listening to "OS network condition changed hook" can help show the prompt more quickly than a timeout event, but it requires elevated access/privileges which might irritate certain players.
                    */
                    await Task.WhenAll(Receive(ws, cancellationToken, cancellationTokenSource), Send(ws, cancellationToken));
                    Debug.Log($"Both WebSocket 'Receive' and 'Send' tasks are ended @incCnt={incCnt}.");
                }
            } catch (OperationCanceledException ocEx) {
                Debug.LogWarning($"WsSession is cancelled for 'ConnectAsync' @incCnt={incCnt}; ocEx.Message={ocEx.Message}");
            } catch (Exception ex) {
                Debug.LogWarning($"WsSession is stopped by exception; ex={ex}");
                // [WARNING] Edge case here, if by far we haven't signaled "guiCanProceedSignalSource", it means that the "characterSelectPanel" is still awaiting this signal to either proceed to battle or prompt an error message.  
                if (!guiCanProceedSignalSource.IsCancellationRequested) {
                    string errMsg = ("ConnectWs failed before battle starts, authToken=" + authToken + " playerId=" + playerId + ", please go back to LoginPage!");
                    throw new Exception(errMsg);
                } else {
                    var exMsg = new WsResp {
                        Ret = ErrCode.UnknownError,
                        ErrMsg = ex.Message
                    };
                    recvBuffer.Enqueue(exMsg);
                }
            } finally {
                try {
                    if (null != ws) {
                        ws.Abort();
                    }
                } catch (Exception exUponProactiveClose) {
                    Debug.LogWarning($"Ws connection exception upon proactive aborting @incCnt={incCnt}: {exUponProactiveClose}");
                }
                var closeMsg = new WsResp {
                    Ret = ErrCode.Ok,
                    Act = Battle.DOWNSYNC_MSG_WS_CLOSED
                };
                recvBuffer.Enqueue(closeMsg);

                if (!cancellationToken.IsCancellationRequested) {
                    try {
                        cancellationTokenSource.Cancel();
                    } catch (Exception ex) {
                        Debug.LogError($"Error cancelling ws session token source as a safe wrapping while it was checked not cancelled by far @incCnt={incCnt}: {ex}");
                    }
                }
                Debug.LogWarning($"Enqueued DOWNSYNC_MSG_WS_CLOSED for main thread @incCnt={incCnt}.");
            }
        }
    }

    private async Task Send(ClientWebSocket ws, CancellationToken cancellationToken) {
        Debug.Log($"Starts 'Send' loop @incCnt={incCnt}, ws.State={ws.State}");
        WsReq toSendObj;
        try {
            while (WebSocketState.Open == ws.State && !cancellationToken.IsCancellationRequested) {
                if (senderBuffer.TryTake(out toSendObj, sendBufferReadTimeoutMillis, cancellationToken)) {
                    //Debug.Log("Ws session send: before");
                    var content = new ArraySegment<byte>(toSendObj.ToByteArray());
                    if (Battle.BACKEND_WS_RECV_BYTELENGTH < content.Count) {
                        Debug.LogWarning($"[content too big!] contentByteLength={content.Count} > BACKEND_WS_RECV_BYTELENGTH={Battle.BACKEND_WS_RECV_BYTELENGTH}");
                    }
                    await ws.SendAsync(content, WebSocketMessageType.Binary, true, cancellationToken);
                }
            }
        } catch (OperationCanceledException ocEx) {
            Debug.LogWarning($"WsSession is cancelled for 'Send' @incCnt={incCnt}; ocEx.Message={ocEx.Message}");
        } catch (Exception ex) {
            Debug.LogWarning($"WsSession is stopping for 'Send' @incCnt={incCnt} upon exception; ex={ex}");
        } finally {
            while (senderBuffer.TryTake(out _, sendBufferReadTimeoutMillis, cancellationToken)) { }
            Debug.Log($"Ends 'Send' loop @incCnt={incCnt}, ws.State={ws.State}");
        }
    }

    private async Task Receive(ClientWebSocket ws, CancellationToken cancellationToken, CancellationTokenSource cancellationTokenSource) {
        Debug.Log($"Starts 'Receive' loop @incCnt={incCnt}, ws.State={ws.State}, cancellationToken.IsCancellationRequested={cancellationToken.IsCancellationRequested}");
        byte[] byteBuff = new byte[Battle.FRONTEND_WS_RECV_BYTELENGTH];
        var arrSegBytes = new ArraySegment<byte>(byteBuff);
        //DateTime openedAt = DateTime.Now;
        try {
            while (WebSocketState.Open == ws.State) {
                // FIXME: Without a "read timeout" parameter, it's unable to detect slow or halted ws session here!
                var result = await ws.ReceiveAsync(arrSegBytes, cancellationToken);
                if (WebSocketMessageType.Close == result.MessageType) {
                    Debug.Log("WsSession is asked by remote to close in 'Receive'");
                    if (!cancellationToken.IsCancellationRequested) {
                        cancellationTokenSource.Cancel(); // To cancel the "Send" loop
                    }
                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                    Debug.LogWarning($"WsSession is closed in 'Receive'#1, ws.State={ws.State}");
                    return;
                } else {
                    try {
                        WsResp resp = WsResp.Parser.ParseFrom(byteBuff, 0, result.Count);
                        if (ErrCode.Ok != resp.Ret) {
                            Debug.LogWarning($"@incCnt={incCnt}, unexpected Ret={resp.Ret}, b64 content={Convert.ToBase64String(byteBuff, 0, result.Count)}");
                        }
                        recvBuffer.Enqueue(resp);
                    } catch (Exception pbEx) {
                        Debug.LogWarning($"Protobuf parser exception is caught @incCnt={incCnt} for 'Receive'; ex.Message={pbEx.Message}");
                    }
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }
        } catch (OperationCanceledException ocEx) {
            Debug.LogWarning($"WsSession is cancelled @incCnt={incCnt} for 'Receive'; ocEx.Message={ocEx.Message}");
        } catch (Exception ex) {
            Debug.LogError($"WsSession is stopping @incCnt={incCnt} for 'Receive' upon exception; ex={ex}");
            if (!cancellationToken.IsCancellationRequested) {
                cancellationTokenSource.Cancel(); // To cancel the "Send" loop
            }
        } finally {
            recvBuffer.Clear();
            Debug.LogWarning($"Ends 'Receive' loop @incCnt={incCnt}, ws.State={ws.State}");
        }
    }

#nullable enable
    public delegate void OnLoginResult(int retCode, string? uname, string? playerId, string? authToken);
    public IEnumerator doCachedAutoTokenLoginAction(string httpHost, OnLoginResult? onLoginCallback) {
        string uri = httpHost + "/Auth/Token/Login";
        WWWForm form = new WWWForm();
        form.AddField("token", authToken); 
        form.AddField("playerId", playerId); 
        using (UnityWebRequest webRequest = UnityWebRequest.Post(uri, form)) {
            // Request and wait for the desired page.
            yield return webRequest.SendWebRequest();

            switch (webRequest.result) {
                case UnityWebRequest.Result.ConnectionError:
                case UnityWebRequest.Result.DataProcessingError:
                    Debug.LogError("Error: " + webRequest.error);
                    if (null != onLoginCallback) {
                        onLoginCallback(ErrCode.UnknownError, null, null, null);
                    }
                    break;
                case UnityWebRequest.Result.ProtocolError:
                    Debug.LogError("HTTP Error: " + webRequest.error);
                    if (null != onLoginCallback) {
                        onLoginCallback(ErrCode.UnknownError, null, null, null);
                    }
                    break;
                case UnityWebRequest.Result.Success:
                    var res = JsonConvert.DeserializeObject<AuthResult>(webRequest.downloadHandler.text);
                    Debug.Log($"Received: {res}");
                    if (null != res) {
                        int retCode = res.RetCode;
                        if (ErrCode.Ok == retCode) {
                            var uname = res.Uname;
                            Debug.Log($"Token/Login succeeded, uname: {uname}");
                            if (null != onLoginCallback) {
                                onLoginCallback(ErrCode.Ok, uname, playerId, authToken);
                            }
                        } else {
                            ClearCredentials();
                            if (null != onLoginCallback) {
                                onLoginCallback(retCode, null, null, null);
                            }
                        }
                    } else {
                        ClearCredentials();
                        if (null != onLoginCallback) {
                            onLoginCallback(ErrCode.UnknownError, null, null, null);
                        }
                    }

                    break;
            }
        }
    }
#nullable disable

    ~WsSessionManager() {
        if (null != senderBuffer) senderBuffer.Dispose(); 
    }
}
