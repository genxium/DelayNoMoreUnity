# Latest tag change notes
v2.2.8
- Added basic cutscene support for StoryMode.
- Added slide-dodge (at critical timing) and defence-dodge (cost 1 slot-C).
- Added beam emitting trap (rotary since v2.2.6).
- Added toast UI component.
- Added new debuff `paralyzed`.
- Unified player and npc input handling.
- More versatile level entrance transition.
- Added entry to arena practice mode.

Please checkout the demo videos on YouTube([basic ops](https://youtu.be/nMWBIFb9ZIA), [field tests](https://youtu.be/iOgqfatRcn8)) or BaiduNetDisk([basic ops](https://pan.baidu.com/s/12W4fta34x73c-7ctHGVaVw?pwd=rahg), [field tests](https://pan.baidu.com/s/1iVb2Pc7HHi9bbb3lYl3HrQ?pwd=nrn8)).

_(demos between 4g mobile-hotspot PC v.s. WiFi PC via internet while UDP peer-to-peer holepunch failed, all using input delay = 2 frames i.e. ~32ms)_

![latest_demo](./charts/DLLMU_v2.2.5_spedup.gif)

# How does it work to synchronize across multiple players?
_(how input delay roughly works)_

![input_delay_intro](./charts/InputDelayIntro.jpg)

_(how rollback-and-chase in this project roughly works)_

![server_clients](./charts/ServerClients.jpg)

![rollback_and_chase_intro](./charts/RollbackAndChase.jpg)

_(though using C# for both backend & frontend now, the idea to avoid floating err remains the same as shown below)_
![floating_point_accumulation_err](./charts/AvoidingFloatingPointAccumulationErr.jpg)

# What's this project?
It's a Unity version of [DelayNoMore](https://github.com/genxium/DelayNoMore), a Multiplayer Platformer game demo on websocket with delayed-input Rollback Netcode inspired by GGPO -- but with the backend also rebuilt in C#.

# Notable Features (by far, would add more in the future)
- Automatic correction for "slow ticker", especially "active slow ticker" which is well-known to be a headache for input synchronization
- Peer-to-peer UDP holepunching whenever possible, and will fallback to use the backend as a UDP relay/tunnel if holepunching failed for any participant (kindly note that UDP is always used along side with WebSocket, where the latter is a golden source of frame info)
- Rollback compatible NPC patrolling and vision reaction
- Rollback compatible static and dynamic traps, including a WYSIWYG notation support in Tiled editor (since v1.1.4)
- Rollback compatible monodirectional platform which also supports _slip-down_ operation
- Simple slope dynamics
- Standing and walking on multiple enemy-heads

_(a typical framelog comparison from 2 peers)_

![framelog_comp](./charts/TypicalFrameLogComparison.png)

_(where to find framelog files)_

![framelog_location](./charts/FrameLogLocations.png)

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

# 4. How to properly measure input-prediction performance in a reproducible manner?
It's always non-trivial to mock fluctuating network behaviours, and in this game we might be interested in testing the performance of different input-prediction algorithms, therefore we'd like to mock DETERMINISTIC inputs for a single player including
- `a)` the initial map setup (from tmx), and 
- `b)` the initial character choices of all players, and
- `c)` received `RoomDownsyncFrame`s, `InputDownsyncFrame`s (from websocket) and `InputUpsyncFrame`s (from UDP peers) at EXACTLY THE SAME TIMINGS for different runs of different algorithms in test.

The first two, i.e. `a)` & `b)` are easy to mock and `c)` is possible by mocking [OnlineMapController.pollAndHandleWsRecvBuffer](https://github.com/genxium/DelayNoMoreUnity/blob/v2.2.3/frontend/Assets/Scripts/OnlineMapController.cs#L53) and [OnlineMapController.pollAndHandleUdpRecvBuffer](https://github.com/genxium/DelayNoMoreUnity/blob/v2.2.3/frontend/Assets/Scripts/OnlineMapController.cs#L245).

I should've provided an example of this type of test for the alleged good performance of my algorithm, especially for
- `shared/Battle_dynamics#UpdateInputFrameInPlaceUponDynamics`, and  
- `shared/Battle_dynamics#processInertiaWalking`
, but the performance by far is so nice even in unsuccessful UDP hole-punching cases, thus it's left out as a future roadmap item :) 

# Logging performance concern
`String.Format(...)` can be a serious performance issue when used too frequently. Please remove/comment them when you notice a lag or CPU spike possibly coupled with an intense logging period (it's always recommended to profile beforehand for proof).

# Is it possible to remove all "forceConfirmation"s if player input overwriting is unwanted?  
Yes it's possible to remove/disable both "type#1" and "type#3" in `backend/Battle/Room.cs`. However, it's highly recommended that you reserve the backend dynamics and downsync the RoomDownsyncFrame calculated by backend to all frontends periodically -- the frontend `AbstractMapController.onRoomDownsyncFrame` can handle correction of historic render frames without issue.

The root cause of the need for such periodic RoomDownsyncFrame downsync is that the physics engine uses floating point numbers, and I'm not a fan of determinisitc floating point approach (i.e. there're tradeoffs). If this project is an old style fighting game, then I can rewrite its physics to use rectilinear rectangles only, thus integer only (including snapping) -- yet I want slopes and rotations in the game :)

# How to find all spots of input predictions?
```
proj-root> grep -ri "shouldPredictBtnAHold" --color ./frontend/Assets/Scripts/
proj-root> grep -ri "shouldPredictBtnAHold" --color ./backend/
proj-root> grep -ri "shouldPredictBtnAHold" --color ./shared/
```

# FAQ
Please refer to [FAQ.md](FAQ.md).
