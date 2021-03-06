﻿using Harmony;
using RimWorld;
using Verse;

namespace Reroll2.Patches {
	[HarmonyPatch(typeof(TerrainPatchMaker), "Init")]
	internal class TerrainPatchMaker_Init_Patch {
		[HarmonyPrefix]
		public static void DeterministicPatchesSetup(Map map) {
			Reroll2Controller.Instance.TryPushDeterministicRandState(map, 2);
		}

		[HarmonyPostfix]
		public static void DeterministicPatchesTeardown() {
			Reroll2Controller.Instance.TryPopDeterministicRandState();
		}
	}
}