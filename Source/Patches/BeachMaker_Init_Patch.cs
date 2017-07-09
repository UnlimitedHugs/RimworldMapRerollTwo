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
			Reroll2Controller.Instance.TryPushDeterministicRandState(map, 1);
		}

		[HarmonyPostfix]
		public static void DeterministicBeachTeardown() {
			Reroll2Controller.Instance.TryPopDeterministicRandState();
		}
	}
}