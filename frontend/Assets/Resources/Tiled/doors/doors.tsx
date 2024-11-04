<?xml version="1.0" encoding="UTF-8"?>
<tileset version="1.10" tiledversion="1.11.0" name="doors" tilewidth="498" tileheight="322" tilecount="9" columns="0">
 <grid orientation="orthogonal" width="1" height="1"/>
 <tile id="0">
  <image source="BlueGate-0.png" width="96" height="64"/>
 </tile>
 <tile id="1">
  <image source="GrayGate-0.png" width="96" height="64"/>
 </tile>
 <tile id="2">
  <properties>
   <property name="collisionTypeMask" value="5"/>
   <property name="providesHardPushback" value="1"/>
   <property name="speciesId" value="8"/>
  </properties>
  <image source="GreenGate-0.png" width="96" height="64"/>
  <objectgroup draworder="index" id="2">
   <object id="1" x="0" y="20" width="96" height="44">
    <properties>
     <property name="providesHardPushback" value="1"/>
    </properties>
   </object>
  </objectgroup>
 </tile>
 <tile id="3">
  <image source="OliveGate-0.png" width="96" height="64"/>
 </tile>
 <tile id="4">
  <image source="PinkGate-0.png" width="96" height="64"/>
 </tile>
 <tile id="5">
  <properties>
   <property name="collisionTypeMask" value="5"/>
   <property name="providesHardPushback" value="1"/>
   <property name="speciesId" value="9"/>
  </properties>
  <image source="RedGate-0.png" width="96" height="64"/>
  <objectgroup draworder="index" id="2">
   <object id="1" x="0" y="20" width="96" height="44">
    <properties>
     <property name="providesHardPushback" value="1"/>
    </properties>
   </object>
  </objectgroup>
 </tile>
 <tile id="6">
  <properties>
   <property name="speciesId" value="5"/>
  </properties>
  <image source="TimedWaveDoor1-0.png" width="16" height="32"/>
 </tile>
 <tile id="7">
  <properties>
   <property name="speciesId" value="6"/>
  </properties>
  <image source="IndiWaveDoor1-0.png" width="16" height="32"/>
 </tile>
 <tile id="8">
  <properties>
   <property name="speciesId" value="7"/>
  </properties>
  <image source="SyncWaveDoor1-1.png" width="16" height="32"/>
 </tile>
</tileset>
