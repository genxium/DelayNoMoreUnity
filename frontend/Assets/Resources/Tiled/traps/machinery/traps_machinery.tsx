<?xml version="1.0" encoding="UTF-8"?>
<tileset version="1.10" tiledversion="1.11.0" name="traps_machinery" tilewidth="53" tileheight="50" tilecount="7" columns="0">
 <grid orientation="orthogonal" width="1" height="1"/>
 <tile id="0">
  <properties>
   <property name="collisionTypeMask" value="5"/>
   <property name="speciesId" value="11"/>
  </properties>
  <image source="Fort.png" width="53" height="50"/>
 </tile>
 <tile id="1">
  <properties>
   <property name="speciesId" value="10"/>
  </properties>
  <image source="Jumper-0.png" width="21" height="16"/>
  <objectgroup draworder="index" id="2">
   <object id="1" x="0" y="6" width="21" height="10">
    <properties>
     <property name="collisionTypeMask" value="1"/>
    </properties>
   </object>
  </objectgroup>
 </tile>
 <tile id="2">
  <properties>
   <property name="collisionTypeMask" value="5"/>
   <property name="prohibitsWallGrabbing" value="1"/>
   <property name="providesHardPushback" value="1"/>
   <property name="speciesId" value="12"/>
   <property name="static" value="1"/>
  </properties>
  <image source="LongConveyorToL_1.png" width="47" height="12"/>
  <objectgroup draworder="index" id="2">
   <object id="1" x="0" y="0" width="47" height="12">
    <properties>
     <property name="prohibitsWallGrabbing" value="1"/>
     <property name="providesHardPushback" value="1"/>
    </properties>
   </object>
  </objectgroup>
 </tile>
 <tile id="3">
  <properties>
   <property name="collisionTypeMask" value="5"/>
   <property name="prohibitsWallGrabbing" value="1"/>
   <property name="providesHardPushback" value="1"/>
   <property name="speciesId" value="13"/>
   <property name="static" value="1"/>
  </properties>
  <image source="LongConveyorToR_2.png" width="47" height="12"/>
  <objectgroup draworder="index" id="2">
   <object id="1" x="0" y="0" width="47" height="12">
    <properties>
     <property name="prohibitsWallGrabbing" value="1"/>
     <property name="providesHardPushback" value="1"/>
    </properties>
   </object>
  </objectgroup>
 </tile>
 <tile id="4">
  <properties>
   <property name="collisionTypeMask" value="5"/>
   <property name="prohibitsWallGrabbing" value="1"/>
   <property name="providesHardPushback" value="1"/>
   <property name="speciesId" value="14"/>
   <property name="static" value="1"/>
  </properties>
  <image source="ShortConveyorToL_1.png" width="22" height="12"/>
  <objectgroup draworder="index" id="2">
   <object id="1" x="0" y="0" width="22" height="12">
    <properties>
     <property name="prohibitsWallGrabbing" value="1"/>
     <property name="providesHardPushback" value="1"/>
    </properties>
   </object>
  </objectgroup>
 </tile>
 <tile id="5">
  <properties>
   <property name="collisionTypeMask" value="5"/>
   <property name="prohibitsWallGrabbing" value="1"/>
   <property name="providesHardPushback" value="1"/>
   <property name="speciesId" value="15"/>
   <property name="static" value="1"/>
  </properties>
  <image source="ShortConveyorToR_1.png" width="22" height="12"/>
  <objectgroup draworder="index" id="2">
   <object id="1" x="0" y="0" width="22" height="12">
    <properties>
     <property name="prohibitsWallGrabbing" value="1"/>
     <property name="providesHardPushback" value="1"/>
    </properties>
   </object>
  </objectgroup>
 </tile>
 <tile id="6">
  <properties>
   <property name="collisionTypeMask" value="5"/>
   <property name="speciesId" value="19"/>
  </properties>
  <image source="SmallBallEmitter_1.png" width="22" height="13"/>
 </tile>
</tileset>
