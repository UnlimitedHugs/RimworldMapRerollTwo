﻿using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Reroll2 {
	public static class RerollToolbox {
		private const sbyte ThingMemoryState = -2;
		private const sbyte ThingDiscardedState = -3;

		public static void DoMapReroll() {
			var oldMap = Find.VisibleMap;
			if (oldMap == null) {
				Reroll2Controller.Instance.Logger.Error("No visible map- cannot reroll");
				return;
			}
			LongEventHandler.QueueLongEvent(() => {
				var oldParent = (MapParent)oldMap.ParentHolder;
				var isOnStartingTile = MapIsOnStartingTile(oldMap, Reroll2Controller.Instance.WorldState);
				var originalTile = MoveMapParentSomewhereElse(oldParent);

				if (isOnStartingTile) Current.Game.InitData = MakeInitData(Reroll2Controller.Instance.WorldState, oldMap);

				var oldMapState = GetStateForMap(oldMap);
				var playerPawns = GetAllPlayerPawnsOnMap(oldMap); // includes animals
				var colonists = GetAllPlayerPawnsOnMap(oldMap).Where(p => p.IsColonist).ToList();
				IEnumerable<Thing> nonGeneratedThings = ResolveThingsFromIds(oldMap, oldMapState.PlayerAddedThingIds).ToList();
				//Logger.Message("Non generated things: " + nonGeneratedThings.ListElements());

				if (oldMapState.ScenarioGeneratedThingIds.Count > 0) {
					ClearRelationsWithPawns(colonists, oldMapState.ScenarioGeneratedThingIds);
					DestroyThingsInWorldById(oldMapState.ScenarioGeneratedThingIds);
				}

				DespawnThings(playerPawns.OfType<Thing>(), oldMap);
				DespawnThings(nonGeneratedThings, oldMap);

				ResetIncidentScenarioParts(Find.Scenario);

				var newParent = PlaceNewMapParent(originalTile);
				var previousSeed = oldMapState.RerollSeed ?? Find.World.info.seedString + originalTile;
				var mapSeed = Reroll2Controller.Instance.DeterministicRerollsSetting ? GenerateNewRerollSeed(previousSeed) : Rand.Int.ToString();
				var newMap = GenerateNewMapWithSeed(newParent, oldMap.Size, mapSeed);
				SwitchToMap(newMap);
				if (isOnStartingTile) {
					Find.Scenario.PostGameStart();
					Current.Game.InitData = null;
				}

				var newMapState = GetStateForMap(newMap);
				newMapState.RerollGenerated = true;
				newMapState.PlayerAddedThingIds = oldMapState.PlayerAddedThingIds;
				newMapState.ResourceBalance = oldMapState.ResourceBalance;
				newMapState.RerollSeed = mapSeed;
				InvokeOnRerollEventReceivers(newMap, receiver => receiver.OnMapStateSet());

				if (!isOnStartingTile) {
					SpawnPawnsOnMap(playerPawns, newMap);
				}
				SpawnThingsOnMap(nonGeneratedThings, newMap);

				DiscardFactionBase(oldParent);
			}, "GeneratingMap", true, GameAndMapInitExceptionHandlers.ErrorWhileGeneratingMap);
		}

		public static RerollMapState GetStateForMap(Map map = null) {
			if (map == null) map = Find.VisibleMap;
			if (map == null) {
				Reroll2Controller.Instance.Logger.Error("Cannot get state from null map. VisibleMap was null, as well: " + Environment.StackTrace);
				return null;
			}
			var comp = map.GetComponent<MapComponent_RerollMapState>();
			if (comp == null) {
				Reroll2Controller.Instance.Logger.Error(String.Format("Could not get MapComponent_RerollMapState from map {0}: {1}", map, Environment.StackTrace));
				return null;
			}
			return comp.State ?? (comp.State = new RerollMapState());
		}

		public static void KillMapIntroDialog() {
			Find.WindowStack.TryRemove(typeof(Dialog_NodeTree), false);
		}

		public static void RecordPlayerAddedMapThings(IThingHolder owner, Map onMap) {
			var state = GetStateForMap(onMap);
			var knownOrInvalidThingIds = new HashSet<int>(state.PlayerAddedThingIds.Union(state.ScenarioGeneratedThingIds));
			var nonColonistThings = ThingOwnerUtility.GetAllThingsRecursively(owner)
				.Where(t => !(t is Pawn) && !knownOrInvalidThingIds.Contains(t.thingIDNumber));
			//Logger.Message("Player added things to map: " + nonColonistThings.ListElements());
			state.PlayerAddedThingIds.AddRange(nonColonistThings.Select(t => t.thingIDNumber));
		}

		public static void StoreGeneratedThingIdsInMapState(Map map) {
			var state = GetStateForMap(map);
			var generatedThingIds = GetMapThingsAndPawnsExceptColonists(map).Select(t => t.thingIDNumber);
			state.ScenarioGeneratedThingIds = generatedThingIds.ToList();
		}

		public static void ReduceMapResources(Map map, float consumePercent, float resourcesPercentBalance) {
			if (resourcesPercentBalance == 0) return;
			var rockDef = Find.World.NaturalRockTypesIn(map.Tile).FirstOrDefault();
			var mapResources = GetAllResourcesOnMap(map);

			var newResourceAmount = Mathf.Clamp(resourcesPercentBalance - consumePercent, 0, 100);
			var originalResAmount = Mathf.CeilToInt(mapResources.Count / (resourcesPercentBalance / 100));
			var percentageChange = resourcesPercentBalance - newResourceAmount;
			var resourceToll = Mathf.CeilToInt(Mathf.Abs(originalResAmount * (percentageChange / 100)));

			var toll = resourceToll;
			if (mapResources.Count > 0) {
				// eat random resources
				while (mapResources.Count > 0 && toll > 0) {
					var resIndex = UnityEngine.Random.Range(0, mapResources.Count);
					var resThing = mapResources[resIndex];

					SneakilyDestroyResource(resThing);
					mapResources.RemoveAt(resIndex);
					if (rockDef != null) {
						// put some rock in their place
						var rock = ThingMaker.MakeThing(rockDef);
						GenPlace.TryPlaceThing(rock, resThing.Position, map, ThingPlaceMode.Direct);
					}
					toll--;
				}
			}
			if (Reroll2Controller.Instance.LogConsumedResourcesSetting && Prefs.DevMode) {
				Reroll2Controller.Instance.Logger.Message("Ordered to consume " + consumePercent + "%, with current resources at " + resourcesPercentBalance + "%. Consuming " +
															resourceToll + " resource spots, " + mapResources.Count + " left");
				if (toll > 0) Reroll2Controller.Instance.Logger.Message("Failed to consume " + toll + " resource spots.");
			}

		}

		public static List<Thing> GetAllResourcesOnMap(Map map) {
			return map.listerThings.AllThings.Where(t => t.def != null && t.def.building != null && t.def.building.mineableScatterCommonality > 0).ToList();
		}

		public static void TryStopPawnVomiting(Map map) {
			if (!Reroll2Controller.Instance.NoVomitingSetting) return;
			foreach (var pawn in GetAllPlayerPawnsOnMap(map)) {
				foreach (var hediff in pawn.health.hediffSet.hediffs) {
					if (hediff.def != HediffDefOf.CryptosleepSickness) continue;
					pawn.health.RemoveHediff(hediff);
					break;
				}
			}
		}

		public static void SubtractResourcePercentage(Map map, float percent) {
			var rerollState = GetStateForMap(map);
			ReduceMapResources(map, percent, rerollState.ResourceBalance);
			rerollState.ResourceBalance = Mathf.Clamp(rerollState.ResourceBalance - percent, 0f, 100f);
		}

		public static void InvokeOnRerollEventReceivers(Map map, Action<IRerollEventReceiver> action) {
			if (map.listerBuildings == null) return;
			var allThings = map.listerThings.AllThings;
			for (var i = 0; i < allThings.Count; i++) {
				var receiver = allThings[i] as IRerollEventReceiver;
				if (receiver != null) {
					action(receiver);
				}
			}
		}

		public static void ResetIncidentScenarioParts(Scenario scenario) {
			foreach (var part in scenario.AllParts) {
				if (part != null && part.GetType() == ReflectionCache.ScenPartCreateIncidentType) {
					ReflectionCache.CreateIncident_IsFinished.SetValue(part, false);
				}
			}
		}

		public static void ReceiveMonumentDeactivationLetter(Building_Monument monument) {
			Find.LetterStack.ReceiveLetter("Reroll2_deactivationLetter".Translate(), "Reroll2_deactivationLetter_text".Translate(), LetterDefOf.BadNonUrgent, monument);
		}

		/// <summary>
		/// destroying a resource outright causes too much overhead: fog, area reveal, pathing, roof updates, etc
		///	we just want to replace it. So, we manually strip it out of the map and do some cleanup.
		/// The following is Thing.Despawn code with the unnecessary (for buildings, ar least) parts stripped out, plus key parts from Building.Despawn 
		/// TODO: This approach may break with future releases (if thing despawning changes), so it's worth checking over.
		/// </summary>
		private static void SneakilyDestroyResource(Thing res) {
			var map = res.Map;
			RegionListersUpdater.DeregisterInRegions(res, map);
			map.spawnedThings.Remove(res);
			map.listerThings.Remove(res);
			map.thingGrid.Deregister(res);
			map.coverGrid.DeRegister(res);
			map.tooltipGiverList.Notify_ThingDespawned(res);
			if (res.def.graphicData != null && res.def.graphicData.Linked) {
				map.linkGrid.Notify_LinkerCreatedOrDestroyed(res);
				map.mapDrawer.MapMeshDirty(res.Position, MapMeshFlag.Things, true, false);
			}
			Find.Selector.Deselect(res);
			res.DirtyMapMesh(map);
			if (res.def.drawerType != DrawerType.MapMeshOnly) {
				map.dynamicDrawManager.DeRegisterDrawable(res);
			}
			ReflectionCache.Thing_State.SetValue(res, res.def.DiscardOnDestroyed ? ThingDiscardedState : ThingMemoryState);
			Find.TickManager.DeRegisterAllTickabilityFor(res);
			map.attackTargetsCache.Notify_ThingDespawned(res);
			StealAIDebugDrawer.Notify_ThingChanged(res);
			// building-specific cleanup
			var b = (Building)res;
			if (res.def.IsEdifice()) map.edificeGrid.DeRegister(b);
			var sustainer = (Sustainer)ReflectionCache.Building_SustainerAmbient.GetValue(res);
			if (sustainer != null) sustainer.End();
			map.mapDrawer.MapMeshDirty(b.Position, MapMeshFlag.Buildings);
			map.glowGrid.MarkGlowGridDirty(b.Position);
			map.listerBuildings.Remove((Building)res);
			map.listerBuildingsRepairable.Notify_BuildingDeSpawned(b);
			map.designationManager.Notify_BuildingDespawned(b);
		}

		private static void DestroyThingsInWorldById(IEnumerable<int> idsToDestroy) {
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

		private static void SpawnPawnsOnMap(IEnumerable<Pawn> pawns, Map map) {
			foreach (var pawn in pawns) {
				if (pawn.Destroyed) continue;
				IntVec3 pos;
				if (!DropCellFinder.TryFindDropSpotNear(map.Center, map, out pos, false, false)) {
					pos = map.Center;
					Reroll2Controller.Instance.Logger.Error("Could not find drop spot for pawn {0} on map {1}", pawn, map);
				}
				GenSpawn.Spawn(pawn, pos, map);
			}
		}

		private static IEnumerable<Thing> ResolveThingsFromIds(Map map, IEnumerable<int> thingIds) {
			var idSet = new HashSet<int>(thingIds);
			return map.listerThings.AllThings.Where(t => idSet.Contains(t.thingIDNumber));
		}

		private static void SpawnThingsOnMap(IEnumerable<Thing> things, Map map) {
			foreach (var thing in things) {
				if (thing.Destroyed || thing.Spawned) continue;
				IntVec3 pos;
				if (!DropCellFinder.TryFindDropSpotNear(map.Center, map, out pos, false, false)) {
					pos = map.Center;
				}
				if (!GenPlace.TryPlaceThing(thing, pos, map, ThingPlaceMode.Near)) {
					GenSpawn.Spawn(thing, pos, map);
					Reroll2Controller.Instance.Logger.Error("Could not find drop spot for thing {0} on map {1}", thing, map);
				}
			}
		}

		private static GameInitData MakeInitData(RerollWorldState state, Map sourceMap) {
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

		private static bool MapIsOnStartingTile(Map map, RerollWorldState state) {
			var mapParent = (MapParent)map.ParentHolder;
			if (mapParent == null) return false;
			return mapParent.Tile == state.StartingTile;
		}

		private static void DiscardFactionBase(MapParent mapParent) {
			Current.Game.DeinitAndRemoveMap(mapParent.Map);
			Find.WorldObjects.Remove(mapParent);
		}

		private static void SwitchToMap(Map newMap) {
			Current.Game.VisibleMap = newMap;
		}

		private static Map GenerateNewMapWithSeed(MapParent mapParent, IntVec3 size, string seed) {
			var prevSeed = Find.World.info.seedString;
			Find.World.info.seedString = seed;
			var newMap = GetOrGenerateMapUtility.GetOrGenerateMap(mapParent.Tile, size, null);
			Find.World.info.seedString = prevSeed;
			return newMap;
		}

		private static void ClearRelationsWithPawns(IEnumerable<Pawn> colonists, IEnumerable<int> thingIds) {
			var pawnIdsToForget = new HashSet<int>(thingIds);
			foreach (var pawn in colonists) {
				foreach (var relation in pawn.relations.DirectRelations.ToArray()) {
					if (relation.otherPawn != null && pawnIdsToForget.Contains(relation.otherPawn.thingIDNumber)) {
						pawn.relations.RemoveDirectRelation(relation);
					}
				}
			}
		}

		private static List<Pawn> GetAllPlayerPawnsOnMap(Map map) {
			return map.mapPawns.PawnsInFaction(Faction.OfPlayer).ToList();
		}

		private static void DespawnThings(IEnumerable<Thing> things, Map referenceMap) {
			foreach (var thing in things) {
				EjectThingFromContainer(thing, referenceMap);
				var pawn = thing as Pawn;
				if (pawn != null && pawn.carryTracker != null && pawn.carryTracker.CarriedThing != null) {
					Thing dropped;
					pawn.carryTracker.TryDropCarriedThing(thing.Position, ThingPlaceMode.Near, out dropped);
				}
				if (thing.Spawned) thing.DeSpawn();
			}
		}

		private static MapParent PlaceNewMapParent(int worldTile) {
			var newParent = (MapParent)WorldObjectMaker.MakeWorldObject(WorldObjectDefOf.FactionBase);
			newParent.Tile = worldTile;
			newParent.SetFaction(Faction.OfPlayer);
			Find.WorldObjects.Add(newParent);
			return newParent;
		}

		private static int MoveMapParentSomewhereElse(MapParent oldParent) {
			var originalTile = oldParent.Tile;
			oldParent.Tile = TileFinder.RandomStartingTile();
			return originalTile;
		}

		private static IEnumerable<Thing> GetMapThingsAndPawnsExceptColonists(Map map) {
			var colonists = GetAllPlayerPawnsOnMap(map).Where(p => p.IsColonist).ToArray();
			return FilterOutWornApparel(GetAllHaulableThingsOnMap(map), colonists).Union(map.mapPawns.AllPawns.Except(colonists).OfType<Thing>());
		}

		private static List<Thing> GetAllHaulableThingsOnMap(Map map) {
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

		private static IEnumerable<Thing> FilterOutWornApparel(IEnumerable<Thing> things, IEnumerable<Pawn> wornByPawns) {
			var apparel = wornByPawns.SelectMany(c => c.apparel.WornApparel).OfType<Thing>();
			return things.Except(apparel);
		}

		private static void EjectThingFromContainer(Thing thing, Map toMap) {
			var holdingMap = thing.Map;
			if (holdingMap == null && thing.holdingOwner != null) {
				thing.holdingOwner.Remove(thing);
				GenSpawn.Spawn(thing, toMap.Center, toMap);
			}
		}

		private static IEnumerable<Thing> FilterOutThingsWithIds(IEnumerable<Thing> things, IEnumerable<int> idsToRemove) {
			var idSet = new HashSet<int>(idsToRemove);
			return things.Where(t => !idSet.Contains(t.thingIDNumber));
		}

		private static string GenerateNewRerollSeed(string previousSeed) {
			const int magicNumber = 3;
			unchecked {
				return ((previousSeed.GetHashCode() << 1) * magicNumber).ToString();
			}
		}
	}
}