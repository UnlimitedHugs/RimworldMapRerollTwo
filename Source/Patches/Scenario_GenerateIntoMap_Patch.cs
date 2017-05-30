using System.Collections.Generic;
using System.Linq;
using Harmony;
using HugsLib.Utils;
using RimWorld;
using Verse;

namespace Reroll2.Patches {
	//public void GenerateIntoMap(Map map)
	[HarmonyPatch(typeof(Scenario), "GenerateIntoMap")]
	public static class Scenario_GenerateIntoMap_Patch {
		[HarmonyPrefix]
		public static void SnapshotThingsBeforeScenario(Map map, ref IEnumerable<Thing> __state) {
			__state = GetAllThings(map).ToArray();
		}

		[HarmonyPostfix]
		public static void SnapshotThingsAfterScenario(Map map, ref IEnumerable<Thing> __state) {
			if (__state == null) {
				Reroll2Controller.Instance.Logger.Error("Could not capture things after Scenario.GenerateIntoMap. Another mod likely cancelled our prefix.");
				return;
			}
			var addedThingIds = GetAllThings(map).Except(__state).Select(t => t.thingIDNumber);
			var rerollState = Reroll2Controller.Instance.GetStateForMap(map);
			rerollState.ScenarioGeneratedThingIds.Clear();
			rerollState.ScenarioGeneratedThingIds.AddRange(addedThingIds);
		}

		private static IEnumerable<Thing> GetAllThings(Map map) {
			return Reroll2Controller.Instance.GetAllThingsOnMap(map);
		} 
	}
}