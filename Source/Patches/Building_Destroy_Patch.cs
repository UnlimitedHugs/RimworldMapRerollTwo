using Harmony;
using Verse;

namespace Reroll2.Patches {
	[HarmonyPatch(typeof(Building), "Destroy")]
	internal static class Building_Destroy_Patch {
		[HarmonyPrefix]
		public static void DetectMinedOre(Building __instance, DestroyMode mode) {
			Reroll2Controller.OnBeforeBuildingDestroyed(__instance, mode);
		}
	}
}