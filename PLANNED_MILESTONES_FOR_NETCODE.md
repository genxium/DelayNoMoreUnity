In the [CocosCreator version](https://github.com/genxium/DelayNoMore), I had a real struggle figuring out what features are necessary for a smooth multiplayer experience and in what order should they be implemented.  
 
Here's the order of the necessary steps for taking the "(relatively) easy mode" this time.
1. No recovery upon reconnection & Websocket Only  + Lockstep
    - No BackendDynamics is needed in this version 
    - We need the `RoomManager` on backend to put connected players into correct rooms 
    - We need the prediction algorithm of [change#1](https://github.com/genxium/DelayNoMore/blob/c582071f4f2e3dd7e83d65562c7c99981252c358/jsexport/battle/battle.go#L647) & [change#2](https://github.com/genxium/DelayNoMore/blob/c582071f4f2e3dd7e83d65562c7c99981252c358/frontend/assets/scripts/Map.js#L1446) to help frontend predict movements smoothly 
    - We need the Lockstep-ish implementation in [spot#1](https://github.com/genxium/DelayNoMore/blob/c582071f4f2e3dd7e83d65562c7c99981252c358/frontend/assets/scripts/Map.js#L1120) & [spot#2](https://github.com/genxium/DelayNoMore/blob/c582071f4f2e3dd7e83d65562c7c99981252c358/frontend/assets/scripts/Map.js#L1195) to cope with the [potential avalanche from `ACTIVE SLOW TICKER` in ConcerningEdgeCases](https://github.com/genxium/DelayNoMore/blob/c582071f4f2e3dd7e83d65562c7c99981252c358/ConcerningEdgeCases.md)  
2. Add UDP capability (including the backend tunnel)
3. Add BackendDynamics & type#1 & type#2 force confirmation
