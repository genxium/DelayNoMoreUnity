# What's this project?
It's a Unity version of [DelayNoMore](https://github.com/genxium/DelayNoMore), a Multiplayer Platformer game demo on websocket with delayed-input Rollback Netcode inspired by GGPO -- but with the backend also rebuilt in C#.

(battle between Wifi Android v.s. Wifi Android via internet (and UDP peer-to-peer holepunch failed), input delay = 2 frames i.e. ~32ms, [slope dynamics video here](https://pan.baidu.com/s/1ANH2nlcT09mHFJcuvDPZlA?pwd=ycuk) & [multi-enemy-head-walk video here](https://pan.baidu.com/s/1A1u3d4G943FLmdOFAblTJw?pwd=gez7))

_slope dynamics_

![Internet_Dual_SlopeDynamics](./charts/Internet_Dual_SlopeDynamics.gif)

_multi-enemy-head-walk_

![Internet_Dual_SoftPushbacks](./charts/Internet_Dual_SoftPushbacks.gif)

# Notable Features (by far, would add more in the future)
- Automatically correction for "slow ticker", especially "active slow ticker" which is well-known to be a headache for input synchronization
- Peer-to-peer UDP holepunching whenever possible, and will fallback to use the backend as a UDP relay/tunnel if holepunching failed for any participant (kindly note that UDP is always used along side with WebSocket, where the latter is a golden source of frame info)
- Frame logging toggle for both frontend & backend (i.e. `backend/Battle/Room.frameLogEnabled`), useful for debugging out of sync entities when developing new features -- **however, if you updated the battle dynamics and found certain introduced out-of-sync spots difficult to fix, please consider turning to `backend-dynamics` and broadcast reference-render-frames regularly, [the original Golang version backend](https://github.com/genxium/DelayNoMore/blob/v1.0.15/jsexport/battle/battle.go#L593) is a good reference for implementing `backend-dynamics`**
- Rollback compatible NPC patrolling and vision reaction
- Simple slope dynamics 
- Standing and walking on multiple enemy-heads

_(a typical framelog comparison from 2 peers)_

![framelog_comp](./charts/TypicalFrameLogComparison.png)

# How does it work to synchronize across multiple players?
_(how input delay roughly works)_

![input_delay_intro](./charts/InputDelayIntro.jpg)

_(how rollback-and-chase in this project roughly works)_

![server_clients](./charts/ServerClients.jpg)

![rollback_and_chase_intro](./charts/RollbackAndChase.jpg)

_(though using C# for both backend & frontend now, the idea to avoid floating err remains the same as shown below)_
![floating_point_accumulation_err](./charts/AvoidingFloatingPointAccumulationErr.jpg)

# 1. Building & running

## 1.1 Tools to install 
### Backend
- [.NET Framework 7.0](https://dotnet.microsoft.com/en-us/download/dotnet/7.0)
```bash
proj-root/backend> dotnet run
```

### Frontend
- [Unity 2021.3](https://unity.com/releases/editor/qa/lts-releases)

Open `OfflineScene` to try out basic operations.

Open `LoginScene` after launching the backend to try out multiplayer mode. Available test accounts are listed in [DevEnvResources.sqlite](./backend/DevEnvResources.sqlite). The steps are very similar to [that of DelayNoMore CocosCreator version](https://github.com/genxium/DelayNoMore#frontend-2).

### Unit test
Referencing [this document from Microsoft](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-with-dotnet-test) by far.

# 2. Thanks
- To [dravenx](https://opengameart.org/users/dravenx) for providing the [spikey-stuff](https://opengameart.org/content/spikey-stuff).

# 3. Changing endpoints of UnityHub download and changing its http(s) proxy
Kindly note that the proxy setting is not very helpful here when download is slow (alternatively, sometimes the download is just timed out due to DNS issue, you might also wanna have a try on changing DNS only), changing the endpoints from `https` to `http` is critical.

![changing_unity_hub_http](./charts/UnityHubNetworkProxying.png)

References
- https://docs.unity3d.com/2022.1/Documentation/Manual/upm-config-network.html#Hub

# FAQ
Please refer to [FAQ.md](FAQ.md).
