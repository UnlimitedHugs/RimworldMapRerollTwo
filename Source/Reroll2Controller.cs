using System;
using System.Collections.Generic;
using System.Linq;
using HugsLib;
using HugsLib.Utils;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Reroll2 {
	public class Reroll2Controller : ModBase {
		public static Reroll2Controller Instance { get; private set; }

		private int capturedStartingTile = -1;

		public override string ModIdentifier {
			get { return "Reroll2"; }
		}

		internal new ModLogger Logger {
			get { return base.Logger; }
		}

		private RerollWorldState WorldState { get; set; }

		private Reroll2Controller() {
			Instance = this;
		}

		public override void MapComponentsInitializing(Map map) {
			var initData = Find.GameInitData;
			if (initData != null) {
				capturedStartingTile = initData.startingTile;
			}
		}

		public override void WorldLoaded() {
			WorldState = UtilityWorldObjectManager.GetUtilityWorldObject<RerollWorldState>();
			if (capturedStartingTile >= 0) {
				WorldState.StartingTile = capturedStartingTile;
			}
		}

		public override void MapLoaded(Map map) {
			KillMapIntroDialog();
		}

		public override void OnGUI() {
			if (Widgets.ButtonText(new Rect(50f, 50f, 200f, 30f), "Reroll")) {
				DoReroll();
			}
		}

		public RerollMapState GetStateForMap(Map map = null) {
			if (map == null) map = Find.VisibleMap;
			if (map == null) {
				Logger.Error("Cannot get state from null map. VisibleMap was null, as well: "+Environment.StackTrace);
				return null;
			}
			var comp = map.GetComponent<MapComponent_RerollMapState>();
			if (comp == null) {
				Logger.Error(string.Format("Could not get MapComponent_RerollMapState from map {0}: {1}", map, Environment.StackTrace));
				return null;
			}
			return comp.State ?? (comp.State = new RerollMapState());
		}

		private void DoReroll() {
			var oldMap = Find.VisibleMap;
			var oldParent = (MapParent) oldMap.ParentHolder;
			var isOnStartingTile = IsOnStartingTile(oldMap, WorldState);
			var originalTile = MoveMapParentSomewhereElse(oldParent);

			if (isOnStartingTile) Current.Game.InitData = MakeInitData(WorldState, oldMap);

			var colonists = GetAllColonistsOnMap(oldMap);
			PreparePawnsForReroll(colonists);

			var oldMapState = GetStateForMap(oldMap);
			if (oldMapState.ScenarioGeneratedThingIds.Count>0) {
				ClearRelationsWithPawns(colonists, oldMapState.ScenarioGeneratedThingIds.Except(colonists.Select(p => p.thingIDNumber)));
				DestroyEquipmentOnPawns(colonists, oldMapState.ScenarioGeneratedThingIds);
			}

			var newParent = PlaceNewMapParent(originalTile);
			var mapSeed = Rand.Int.ToString();
			var newMap = GenerateNewMapWithSeed(newParent, oldMap.Size, mapSeed);

			SwitchToMap(newMap);

			if (isOnStartingTile) Find.Scenario.PostGameStart();
			if (isOnStartingTile) Current.Game.InitData = null;

			DiscardFactionBase(oldParent);	
		}

		private GameInitData MakeInitData(RerollWorldState state, Map sourceMap) {
			return new GameInitData {
				permadeath = Find.GameInfo.permadeathMode,
				mapSize = sourceMap.Size.x,
				playerFaction = Faction.OfPlayer,
				startingSeason = Season.Undefined,
				startedFromEntry = true,
				startingTile = state.StartingTile,
				startingPawns = GetAllColonistsOnMap(sourceMap)
			};
		}

		private bool IsOnStartingTile(Map map, RerollWorldState state) {
			var mapParent = (MapParent) map.ParentHolder;
			if (mapParent == null) return false;
			return mapParent.Tile == state.StartingTile;
		}

		private void DiscardFactionBase(MapParent mapParent) {
			Current.Game.DeinitAndRemoveMap(mapParent.Map);
			Find.WorldObjects.Remove(mapParent);
		}

		private void SwitchToMap(Map newMap) {
			Current.Game.VisibleMap = newMap;
		}

		private Map GenerateNewMapWithSeed(MapParent mapParent, IntVec3 size, string seed) {
			var prevSeed = Find.World.info.seedString;
			Find.World.info.seedString = seed;
			var newMap = GetOrGenerateMapUtility.GetOrGenerateMap(mapParent.Tile, size, null);
			Find.World.info.seedString = prevSeed;
			return newMap;
		}

		public void ClearRelationsWithPawns(IEnumerable<Pawn> colonists, IEnumerable<int> thingIds) {
			var pawnIdsToForget = new HashSet<int>(thingIds);
			foreach (var pawn in colonists) {
				foreach (var relation in pawn.relations.DirectRelations.ToArray()) {
					if (relation.otherPawn != null && pawnIdsToForget.Contains(relation.otherPawn.thingIDNumber)) {
						pawn.relations.RemoveDirectRelation(relation);
					}
				}
			}
		}

		/*public HashSet<Thing> EnumerateEquipmentOnMap(Map map) {
			var apparel = map.listerThings.ThingsInGroup(ThingRequestGroup.Apparel);
			var weapons = map.listerThings.ThingsInGroup(ThingRequestGroup.Weapon);
			return new HashSet<Thing>(apparel.Union(weapons));
		}*/

		public void DestroyEquipmentOnPawns(List<Pawn> pawns, IEnumerable<int> thingIds) {
			var thingIdsToDetect = new HashSet<int>(thingIds);
			foreach (var pawn in pawns) {
				foreach (var equipment in pawn.equipment.AllEquipmentListForReading.ToArray()) {
					if (thingIdsToDetect.Contains(equipment.thingIDNumber)) {
						pawn.equipment.DestroyEquipment(equipment);
					}
				}
				foreach (var apparel in pawn.apparel.WornApparel.ToArray()) {
					if (thingIdsToDetect.Contains(apparel.thingIDNumber)) {
						pawn.apparel.Remove(apparel);
						apparel.Destroy();
					}
				}
			}
		}

		// get all spawned and podded colonists
		public List<Pawn> GetAllColonistsOnMap(Map map) {
			return GetAllThingsOnMap(map).OfType<Pawn>().Where(p => p.IsColonist).ToList();
		}

		// get spawned things, as well as things in caskets and drop pods
		public IEnumerable<Thing> GetAllThingsOnMap(Map map) {
			List<Thing> tmpThings = new List<Thing>();
			var allThings = map.listerThings.AllThings.ToList();
			// swiped from MapPawns.FreeColonistsSpawnedOrInPlayerEjectablePodsCount
			var list = map.listerThings.ThingsInGroup(ThingRequestGroup.ThisOrAnyCompIsThingHolder);
			for (int j = 0; j < list.Count; j++) {
				var thing = list[j];
				var casket = thing as Building_CryptosleepCasket;
				if ((casket != null && casket.def.building.isPlayerEjectable) || thing is IActiveDropPod || thing.TryGetComp<CompTransporter>() != null) {
					var holder = thing.TryGetComp<CompTransporter>() ?? ((IThingHolder)thing);
					ThingOwnerUtility.GetAllThingsRecursively(holder, tmpThings);
					for (int k = 0; k < tmpThings.Count; k++){
						var owned = tmpThings[k];
						if (owned != null) {
							allThings.Add(owned);
						}
					}
				}
			}
			return allThings;
		}

		private void PreparePawnsForReroll(IEnumerable<Pawn> pawns) {
			foreach (var pawn in pawns) {
				EjectPawnFromContainer(pawn);
				if (pawn.Spawned) pawn.DeSpawn();
			}
		}

		private void EjectPawnFromContainer(Pawn pawn) {
			if (pawn.holdingOwner != null) {
				pawn.holdingOwner.Remove(pawn);
			}
		}

		private int MoveMapParentSomewhereElse(MapParent oldParent) {
			var originalTile = oldParent.Tile;
			oldParent.Tile = TileFinder.RandomStartingTile();
			return originalTile;
		}

		private MapParent PlaceNewMapParent(int worldTile) {
			var newParent = (MapParent)WorldObjectMaker.MakeWorldObject(WorldObjectDefOf.FactionBase);
			newParent.Tile = worldTile;
			newParent.SetFaction(Faction.OfPlayer);
			Find.WorldObjects.Add(newParent);
			return newParent;
		}

		public static void KillMapIntroDialog() {
			Find.WindowStack.TryRemove(typeof(Dialog_NodeTree), false);
		}
	}
}