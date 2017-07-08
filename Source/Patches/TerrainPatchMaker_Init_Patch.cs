using Harmony;
using RimWorld;
using Verse;

namespace Reroll2.Patches {
	[HarmonyPatch(typeof(TerrainPatchMaker), "Init")]
	internal class TerrainPatchMaker_Init_Patch {
		[HarmonyPrefix]
		public static void DeterministicPatchesSetup(Map map) {
			var deterministicSeed = Gen.HashCombineInt(GenText.StableStringHash(Find.World.info.seedString), map.Tile); 
			Rand.PushState(deterministicSeed);
		}

		[HarmonyPostfix]
		public static void DeterministicPatchesTeardown() {
			Rand.PopState();
		}
	}
}