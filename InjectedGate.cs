﻿using System.Collections.Generic;
using ActionGame.Point;
using UnityEngine;

namespace KK_Navigation
{
    public class InjectedGate : GateInfo
    {
        public int linkMapNo;
        public string linkName;

        private static int _freeGateID = 200;

        public InjectedGate() : base((List<string>)null)
        {
            ID = _freeGateID++; // Autogenerated
            //this.mapNo = gate.mapNo; // Generated based off info from "Target Gate"
            //this.linkID = gate.linkID; // Generated based off info from "Target Gate"
            //this.linkMapNo; // Map ID; Yes, this is how it works.
            //this.pos = Gate[0] // Position
            //this.ang = Gate[1] // Angles
            //this.Name = gate.name; // Name
            //this.playerPos = Spawn[0] // Position
            //this.playerAng = Spawn[1] // Angles
            //this.playerHitPos = Collision[0] // Center
            //this.playerHitSize = Collision[1] // Size
            //this.heroineHitPos = Collision[0] // Center
            //this.heroineHitSize = Collision[1] // Size
            this.iconPos = new Vector3(0.0f, 0.0f, 0.0f);
            this.iconHitPos = new Vector3(0.0f, 0.0f, 0.0f);
            this.iconHitSize = new Vector3(1.0f, 1.0f, 1.0f);
            //this.moveType = Use On Collision
            this.seType = -1;
        }
    }
}
