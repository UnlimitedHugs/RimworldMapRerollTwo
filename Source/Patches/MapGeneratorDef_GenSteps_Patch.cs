﻿using Harmony;
using Verse;

namespace Reroll2.Patches {
	/// <summary>
	/// This is required to intercept and record the map generator used for the generation of a specific map.
	/// </summary>
	[HarmonyPatch(typeof(MapGeneratorDef))]
	[HarmonyPatch("GenSteps", PropertyMethod.Getter)]
	internal static class MapGeneratorDef_GenSteps_Patch {
		[HarmonyPostfix]
		public static void RecordUsedMapGenerator(MapGeneratorDef __instance) {
			Reroll2Controller.Instance.RecordUsedMapGenerator(__instance);
		}
	}
}