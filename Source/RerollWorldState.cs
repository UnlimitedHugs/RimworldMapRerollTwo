﻿using HugsLib.Utils;
using Verse;

namespace Reroll2 {
	public class RerollWorldState : UtilityWorldObject {
		public int StartingTile = -1;

		public override void ExposeData() {
			base.ExposeData();
			Scribe_Values.Look(ref StartingTile, "startingTile", -1);
		}
	}
}