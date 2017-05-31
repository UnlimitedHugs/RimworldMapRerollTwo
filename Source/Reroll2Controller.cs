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

		public override string ModIdentifier {
			get { return "Reroll2"; }
		}

		internal new ModLogger Logger {
			get { return base.Logger; }
		}

		private RerollWorldState _worldState;
		private RerollWorldState WorldState {
			get { return _worldState ?? (_worldState = UtilityWorldObjectManager.GetUtilityWorldObject<RerollWorldState>()); }
		}

		private Reroll2Controller() {
			Instance = this;
		}

		public override void MapComponentsInitializing(Map map) {
			if (Find.GameInitData != null) {
				WorldState.StartingTile = Find.GameInitData.startingTile;
			}
		}

		public void MapGenerated(Map map) {
			StoreGeneratedThingIdsInMapState(map);
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

		public void RecordPlayerAddedMapThings(IThingHolder owner, Map onMap) {
			var state = GetStateForMap(onMap);
			var knownOrInvalidThingIds = new HashSet<int>(state.PlayerAddedThingIds.Union(state.ScenarioGeneratedThingIds));
			var nonColonistThings = ThingOwnerUtility.GetAllThingsRecursively(owner)
				.Where(t => !(t is Pawn) && !knownOrInvalidThingIds.Contains(t.thingIDNumber));
			//Logger.Message("Player added things to map: " + nonColonistThings.ListElements());
			state.PlayerAddedThingIds.AddRange(nonColonistThings.Select(t => t.thingIDNumber));
		}

		private void DoReroll() {
			var oldMap = Find.VisibleMap;
			var oldParent = (MapParent) oldMap.ParentHolder;
			var isOnStartingTile = IsOnStartingTile(oldMap, WorldState);
			var originalTile = MoveMapParentSomewhereElse(oldParent);

			if (isOnStartingTile) Current.Game.InitData = MakeInitData(WorldState, oldMap);

			var oldMapState = GetStateForMap(oldMap);
			var playerPawns = GetAllPlayerPawnsOnMap(oldMap); // includes animals
			var colonists = GetAllPlayerPawnsOnMap(oldMap).Where(p => p.IsColonist).ToList();
			IEnumerable<Thing> nonGeneratedThings = ResolveThingsFromIds(oldMap, oldMapState.PlayerAddedThingIds).ToList();
			//Logger.Message("Non generated things: " + nonGeneratedThings.ListElements());

			if (oldMapState.ScenarioGeneratedThingIds.Count>0) {
				ClearRelationsWithPawns(colonists, oldMapState.ScenarioGeneratedThingIds);
				DestroyThingsInWorldById(oldMapState.ScenarioGeneratedThingIds);
			}

			DespawnThings(playerPawns.OfType<Thing>());
			DespawnThings(nonGeneratedThings);

			var newParent = PlaceNewMapParent(originalTile);
			var mapSeed = Rand.Int.ToString();
			var newMap = GenerateNewMapWithSeed(newParent, oldMap.Size, mapSeed);
			SwitchToMap(newMap);
			if (isOnStartingTile) {
				Find.Scenario.PostGameStart();
				Current.Game.InitData = null;
			}

			var newMapState = GetStateForMap(newMap);
			newMapState.RerollGenerated = true;
			newMapState.PlayerAddedThingIds = oldMapState.PlayerAddedThingIds;

			if (!isOnStartingTile) {
				SpawnPawnsOnMap(playerPawns, newMap);
			}
			SpawnThingsOnMap(nonGeneratedThings, newMap);
			
			DiscardFactionBase(oldParent);	
		}

		private IEnumerable<Thing> ResolveThingsFromIds(Map map, IEnumerable<int> thingIds) {
			var idSet = new HashSet<int>(thingIds);
			return map.listerThings.AllThings.Where(t => idSet.Contains(t.thingIDNumber));
		} 

		private bool MapIsOnStartingTile(Map map) {
			return map.Tile != WorldState.StartingTile;
		}

		private IEnumerable<Thing> FilterOutThingsWithIds(IEnumerable<Thing> things, IEnumerable<int> idsToRemove) {
			var idSet = new HashSet<int>(idsToRemove);
			return things.Where(t => !idSet.Contains(t.thingIDNumber));
		}

		private void DestroyThingsInWorldById(IEnumerable<int> idsToDestroy) {
			var idSet = new HashSet<int>(idsToDestroy);
			var things = new List<Thing>();
			ThingOwnerUtility.GetAllThingsRecursively(Find.World, things);
			for (int i = 0; i < things.Count; i++) {
				var t = things[i];
				if (idSet.Contains(t.thingIDNumber) && !t.Destroyed) {
					t.Destroy();
				}
			}
		}

		private void SpawnPawnsOnMap(IEnumerable<Pawn> pawns, Map map) {
			foreach (var pawn in pawns) {
				if (pawn.Destroyed) continue;
				IntVec3 pos;
				if (!DropCellFinder.TryFindDropSpotNear(map.Center, map, out pos, false, false)) {
					pos = map.Center;
					Logger.Error("Could not find drop spot for pawn {0} on map {1}", pawn, map);
				}
				GenSpawn.Spawn(pawn, pos, map);
			}
		}

		private void SpawnThingsOnMap(IEnumerable<Thing> things, Map map) {
			foreach (var thing in things) {
				if (thing.Destroyed || thing.Spawned) continue;
				IntVec3 pos;
				if (!DropCellFinder.TryFindDropSpotNear(map.Center, map, out pos, false, false)) {
					pos = map.Center;
				}
				if (!GenPlace.TryPlaceThing(thing, pos, map, ThingPlaceMode.Near)) {
					GenSpawn.Spawn(thing, pos, map);
					Logger.Error("Could not find drop spot for thing {0} on map {1}", thing, map);
				}
			}
		}

		private GameInitData MakeInitData(RerollWorldState state, Map sourceMap) {
			return new GameInitData {
				permadeath = Find.GameInfo.permadeathMode,
				mapSize = sourceMap.Size.x,
				playerFaction = Faction.OfPlayer,
				startingSeason = Season.Undefined,
				startedFromEntry = true,
				startingTile = state.StartingTile,
				startingPawns = GetAllPlayerPawnsOnMap(sourceMap).Where(p => p.IsColonist).ToList()
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

		/*public void DestroyEquipmentOnPawns(List<Pawn> pawns, IEnumerable<int> thingIds) {
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
		}*/

		// get all spawned and podded colonists
		public List<Pawn> GetAllPlayerPawnsOnMap(Map map) {
			return map.mapPawns.PawnsInFaction(Faction.OfPlayer).ToList();
		}

		public void StoreGeneratedThingIdsInMapState(Map map) {
			var state = GetStateForMap(map);
			var generatedThingIds = GetMapThingsAndPawnsExceptColonists(map).Select(t => t.thingIDNumber);
			state.ScenarioGeneratedThingIds = generatedThingIds.ToList();
		}

		public IEnumerable<Thing> GetMapThingsAndPawnsExceptColonists(Map map) {
			var colonists = GetAllPlayerPawnsOnMap(map).Where(p => p.IsColonist).ToArray();
			return FilterOutWornApparel(GetAllHaulableThingsOnMap(map), colonists)
				.Union(map.mapPawns.AllPawns.Except(colonists).OfType<Thing>());
		}

		public List<Thing> GetAllHaulableThingsOnMap(Map map) {
			var things = new List<Thing>();
			var matchingThings = new List<Thing>();
			ThingOwnerUtility.GetAllThingsRecursively(map, things);
			for (int i = 0; i < things.Count; i++) {
				var thing = things[i];
				if (thing != null && thing.def != null && thing.def.EverHaulable) {
					matchingThings.Add(thing);
				}
			}
			return matchingThings;
		}

		public IEnumerable<Thing> FilterOutWornApparel(IEnumerable<Thing> things, IEnumerable<Pawn> wornByPawns) {
			var apparel = wornByPawns.SelectMany(c => c.apparel.WornApparel).OfType<Thing>();
			return things.Except(apparel);
		}

		private void DespawnThings(IEnumerable<Thing> things) {
			foreach (var thing in things) {
				EjectThingFromContainer(thing);
				var pawn = thing as Pawn;
				if (pawn != null && pawn.carryTracker != null && pawn.carryTracker.CarriedThing != null) {
					Thing dropped;
					pawn.carryTracker.TryDropCarriedThing(thing.Position, ThingPlaceMode.Near, out dropped);
				}
				if (thing.Spawned) thing.DeSpawn();
			}
		}

		private void EjectThingFromContainer(Thing thing) {
			if (thing.holdingOwner != null && !(thing.holdingOwner.Owner is Map)) {
				thing.holdingOwner.Remove(thing);
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