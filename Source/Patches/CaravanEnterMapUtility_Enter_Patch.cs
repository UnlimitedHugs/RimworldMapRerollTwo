using System;
using Harmony;
using RimWorld.Planet;
using Verse;

namespace Reroll2.Patches {
	[HarmonyPatch(typeof(CaravanEnterMapUtility))]
	[HarmonyPatch("Enter")]
	[HarmonyPatch(new []{typeof(Caravan), typeof(Map), typeof(Func<Pawn, IntVec3>), typeof(CaravanDropInventoryMode), typeof(bool)})]
	//public static void Enter(Caravan caravan, Map map, Func<Pawn, IntVec3> spawnCellGetter, CaravanDropInventoryMode dropInventoryMode = CaravanDropInventoryMode.DoNotDrop, bool draftColonists = false)
	public static class CaravanEnterMapUtility_Enter_Patch {
		[HarmonyPrefix]
		public static void RecordPlayerAddedMapThings(Caravan caravan, Map map) {
			Reroll2Controller.Instance.RecordPlayerAddedMapThings(caravan.pawns.Owner, map);
		}
	}
}