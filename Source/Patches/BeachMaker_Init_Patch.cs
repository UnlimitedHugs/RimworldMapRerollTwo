using System.Reflection;
using Harmony;
using Verse;

namespace Reroll2.Patches {
	[HarmonyPatch]
	internal class BeachMaker_Init_Patch {
		[HarmonyTargetMethod]
		public static MethodInfo GetMethod(HarmonyInstance inst) {
			return AccessTools.Method(AccessTools.TypeByName("BeachMaker"), "Init");
		}

		[HarmonyPrefix]
		public static void DeterministicBeachSetup(Map map) {
			var deterministicSeed = Gen.HashCombineInt(GenText.StableStringHash(Find.World.info.seedString), map.Tile);
			Rand.PushState(deterministicSeed);
		}

		[HarmonyPostfix]
		public static void DeterministicBeachTeardown() {
			Rand.PopState();
		}
	}
}