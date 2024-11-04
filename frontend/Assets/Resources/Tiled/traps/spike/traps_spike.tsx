<?xml version="1.0" encoding="UTF-8"?>
<tileset version="1.10" tiledversion="1.11.0" name="traps_spike" tilewidth="96" tileheight="96" tilecount="10" columns="0">
 <grid orientation="orthogonal" width="1" height="1"/>
 <tile id="0">
  <properties>
   <property name="collisionTypeMask" value="5"/>
   <property name="prohibitsWallGrabbing" value="1"/>
   <property name="providesHardPushback" value="1"/>
   <property name="speciesId" value="1"/>
  </properties>
  <image source="blockSteel.png" width="64" height="16"/>
  <objectgroup draworder="index" id="2">
   <object id="1" x="0" y="0" width="64" height="16">
    <properties>
     <property name="prohibitsWallGrabbing" value="1"/>
     <property name="providesHardPushback" value="1"/>
    </properties>
   </object>
  </objectgroup>
 </tile>
 <tile id="1">
  <properties>
   <property name="prohibitsWallGrabbing" value="1"/>
   <property name="speciesId" value="2"/>
  </properties>
  <image source="SpikeblockSteel.png" width="64" height="32"/>
  <objectgroup draworder="index" id="2">
   <object id="1" x="0" y="0" width="64" height="16">
    <properties>
     <property name="collisionTypeMask" value="1"/>
     <property name="prohibitsWallGrabbing" value="1"/>
     <property name="providesHardPushback" value="1"/>
    </properties>
   </object>
   <object id="2" x="4" y="18" width="56" height="14">
    <properties>
     <property name="collisionTypeMask" value="4"/>
     <property name="providesDamage" value="1"/>
    </properties>
   </object>
  </objectgroup>
 </tile>
 <tile id="2">
  <properties>
   <property name="prohibitsWallGrabbing" value="1"/>
   <property name="speciesId" value="3"/>
  </properties>
  <image source="SpikeGroundTrap.png" width="64" height="32"/>
  <objectgroup draworder="index" id="2">
   <object id="1" x="0" y="0" width="64" height="32">
    <properties>
     <property name="providesDamage" value="1"/>
    </properties>
   </object>
   <object id="2" x="0" y="16" width="64" height="16">
    <properties>
     <property name="collisionTypeMask" value="1"/>
     <property name="providesHardPushback" value="1"/>
    </properties>
   </object>
   <object id="3" x="0" y="0" width="64" height="32">
    <properties>
     <property name="collisionTypeMask" value="4"/>
     <property name="providesDamage" value="1"/>
    </properties>
   </object>
  </objectgroup>
 </tile>
 <tile id="3">
  <image source="SteelspikeUp.png" width="16" height="32"/>
 </tile>
 <tile id="4">
  <properties>
   <property name="collisionTypeMask" value="5"/>
   <property name="prohibitsWallGrabbing" value="1"/>
   <property name="providesHardPushback" value="1"/>
   <property name="speciesId" value="4"/>
  </properties>
  <image source="verticalBlockSteel.png" width="16" height="64"/>
  <objectgroup draworder="index" id="2">
   <object id="1" x="0" y="0" width="16" height="64">
    <properties>
     <property name="prohibitsWallGrabbing" value="1"/>
     <property name="providesHardPushback" value="1"/>
    </properties>
   </object>
  </objectgroup>
 </tile>
 <tile id="5">
  <properties>
   <property name="speciesId" value="6"/>
  </properties>
  <image source="SawBig.png" width="96" height="96"/>
  <objectgroup draworder="index" id="2">
   <object id="1" x="7" y="7" width="80" height="80">
    <properties>
     <property name="collisionTypeMask" value="4"/>
     <property name="providesDamage" value="1"/>
    </properties>
   </object>
  </objectgroup>
 </tile>
 <tile id="6">
  <properties>
   <property name="speciesId" value="5"/>
  </properties>
  <image source="SawSmall.png" width="64" height="64"/>
  <objectgroup draworder="index" id="2">
   <object id="1" x="8" y="8" width="48" height="48">
    <properties>
     <property name="collisionTypeMask" value="4"/>
     <property name="providesDamage" value="1"/>
    </properties>
   </object>
  </objectgroup>
 </tile>
 <tile id="7">
  <properties>
   <property name="collisionTypeMask" value="98308"/>
   <property name="prohibitsWallGrabbing" value="1"/>
   <property name="providesHardPushback" value="1"/>
   <property name="speciesId" value="16"/>
  </properties>
  <image source="RotaryBarrier.png" width="64" height="16"/>
  <objectgroup draworder="index" id="2">
   <object id="1" x="12" y="0" width="52" height="16">
    <properties>
     <property name="prohibitsWallGrabbing" value="1"/>
     <property name="providesHardPushback" value="1"/>
    </properties>
   </object>
  </objectgroup>
 </tile>
 <tile id="8">
  <properties>
   <property name="collisionTypeMask" value="98308"/>
   <property name="prohibitsWallGrabbing" value="1"/>
   <property name="providesHardPushback" value="1"/>
   <property name="speciesId" value="17"/>
  </properties>
  <image source="verticalRotaryBarrier.png" width="16" height="64"/>
  <objectgroup draworder="index" id="2">
   <object id="1" x="0" y="0" width="16" height="52">
    <properties>
     <property name="prohibitsWallGrabbing" value="1"/>
     <property name="providesHardPushback" value="1"/>
    </properties>
   </object>
  </objectgroup>
 </tile>
 <tile id="9">
  <properties>
   <property name="collisionTypeMask" value="98308"/>
   <property name="prohibitsWallGrabbing" value="1"/>
   <property name="providesHardPushback" value="1"/>
   <property name="speciesId" value="18"/>
  </properties>
  <image source="verticalRotaryBarrierLong.png" width="16" height="72"/>
  <objectgroup draworder="index" id="2">
   <object id="1" x="0" y="0" width="16" height="60">
    <properties>
     <property name="prohibitsWallGrabbing" value="1"/>
     <property name="providesHardPushback" value="1"/>
    </properties>
   </object>
  </objectgroup>
 </tile>
</tileset>
