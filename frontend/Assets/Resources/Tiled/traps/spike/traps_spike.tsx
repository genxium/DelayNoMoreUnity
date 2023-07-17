<?xml version="1.0" encoding="UTF-8"?>
<tileset version="1.10" tiledversion="1.10.1" name="traps_spike" tilewidth="64" tileheight="32" tilecount="4" columns="0">
 <grid orientation="orthogonal" width="1" height="1"/>
 <tile id="0">
  <properties>
   <property name="collisionTypeMask" value="1"/>
   <property name="providesHardPushback" value="1"/>
   <property name="speciesId" value="1"/>
  </properties>
  <image width="64" height="16" source="blockSteel.png"/>
  <objectgroup draworder="index">
   <object id="1" x="0" y="0" width="64" height="16">
    <properties>
     <property name="providesHardPushback" value="1"/>
    </properties>
   </object>
  </objectgroup>
 </tile>
 <tile id="1">
  <properties>
   <property name="speciesId" value="2"/>
  </properties>
  <image width="64" height="32" source="SpikeblockSteel.png"/>
  <objectgroup draworder="index" id="2">
   <object id="1" x="0" y="0" width="64" height="16">
    <properties>
     <property name="collisionTypeMask" value="1"/>
     <property name="providesHardPushback" value="1"/>
    </properties>
   </object>
   <object id="2" x="0" y="16" width="64" height="16">
    <properties>
     <property name="collisionTypeMask" value="4"/>
     <property name="providesDamage" value="1"/>
    </properties>
   </object>
  </objectgroup>
 </tile>
 <tile id="2">
  <properties>
   <property name="collisionTypeMask" value="4"/>
   <property name="providesDamage" value="1"/>
   <property name="speciesId" value="3"/>
  </properties>
  <image width="64" height="32" source="SpikeGroundTrap.png"/>
 </tile>
 <tile id="3">
  <image width="16" height="32" source="SteelspikeUp.png"/>
 </tile>
</tileset>
