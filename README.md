# What's this project?
It's a Unity version of [DelayNoMore](https://github.com/genxium/DelayNoMore), a Multiplayer Platformer game demo on websocket with delayed-input Rollback Netcode inspired by GGPO -- but with the backend also rebuilt in C#.

(battle between celluar 4G Android v.s. Wifi Android via internet, UDP peer-to-peer holepunched, [original video here](https://pan.baidu.com/s/1s7Ra0vFkv0B3MlDCezzSyg?pwd=qkep))

![Internet_Dual_1_Merged_SpedUp](./charts/Internet_Dual_1_Merged_SpedUp.gif)

# Notable Features (by far, would add more in the future)
- Automatically correction for "slow ticker", especially "active slow ticker" which is well-known to be a headache for input synchronization
- Peer-to-peer UDP holepunching whenever possible, and will fallback to use the backend as a UDP relay/tunnel if holepunching failed for any participant (kindly note that UDP is always used along side with WebSocket, where the latter is a golden source of frame info)

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

# FAQ
Please refer to [FAQ.md](FAQ.md).