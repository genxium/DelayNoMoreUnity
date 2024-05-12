<?xml version="1.0" encoding="UTF-8"?>
<tileset version="1.10" tiledversion="1.10.2" name="doors" tilewidth="96" tileheight="64" tilecount="9" columns="0">
 <grid orientation="orthogonal" width="1" height="1"/>
 <tile id="0">
  <image width="96" height="64" source="BlueGate-0.png"/>
 </tile>
 <tile id="1">
  <image width="96" height="64" source="GrayGate-0.png"/>
 </tile>
 <tile id="2">
  <properties>
   <property name="collisionTypeMask" value="5"/>
   <property name="providesHardPushback" value="1"/>
   <property name="speciesId" value="8"/>
  </properties>
  <image width="96" height="64" source="GreenGate-0.png"/>
  <objectgroup draworder="index" id="2">
   <object id="1" x="0" y="20" width="96" height="44">
    <properties>
     <property name="providesHardPushback" value="1"/>
    </properties>
   </object>
  </objectgroup>
 </tile>
 <tile id="3">
  <image width="96" height="64" source="OliveGate-0.png"/>
 </tile>
 <tile id="4">
  <image width="96" height="64" source="PinkGate-0.png"/>
 </tile>
 <tile id="5">
  <properties>
   <property name="collisionTypeMask" value="5"/>
   <property name="providesHardPushback" value="1"/>
   <property name="speciesId" value="9"/>
  </properties>
  <image width="96" height="64" source="RedGate-0.png"/>
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
   <property name="speciesId" value="3"/>
  </properties>
  <image width="16" height="32" source="TimedDoor1-4.png"/>
 </tile>
 <tile id="7">
  <properties>
   <property name="speciesId" value="4"/>
  </properties>
  <image width="32" height="32" source="WaveTimedDoor-2.png"/>
 </tile>
 <tile id="8">
  <properties>
   <property name="speciesId" value="7"/>
  </properties>
  <image width="16" height="32" source="EscapeDoor1-0.png"/>
 </tile>
</tileset>
