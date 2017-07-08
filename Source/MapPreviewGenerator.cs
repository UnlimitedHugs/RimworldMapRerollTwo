﻿using System;
using System.Collections.Generic;
using Harmony;
using Promises;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Reroll2 {
	public static class MapPreviewGenerator {
		private delegate TerrainDef TerrainFromMethod(IntVec3 c, Map map, float elevation, float fertility, object riverMaker, bool preferSolid);
		private delegate TerrainDef RiverMakerTerrainAt(IntVec3 c);
		private delegate TerrainDef BeachMakerBeachTerrainAt(IntVec3 c, BiomeDef biome);

		private static readonly Color defaultTerrainColor = GenColor.FromHex("594A3B");
		private static readonly Color missingTerrainColor = new Color(0.38f, 0.38f, 0.38f);
		private static readonly Color solidStoneColor = GenColor.FromHex("36271C");
		private static readonly Color waterColorDeep = GenColor.FromHex("3A434D");
		private static readonly Color waterColorShallow = GenColor.FromHex("434F50");

		private static readonly Dictionary<string, Color> terrainColors = new Dictionary<string, Color> {
			{"Sand", GenColor.FromHex("806F54")},
			{"Soil", defaultTerrainColor},
			{"MarshyTerrain", GenColor.FromHex("3F412B")},
			{"SoilRich", GenColor.FromHex("42362A")},
			{"Gravel", defaultTerrainColor},
			{"Mud", GenColor.FromHex("403428")},
			{"Marsh", GenColor.FromHex("363D30")},
			{"MossyTerrain", defaultTerrainColor},
			{"Ice", GenColor.FromHex("9CA7AC")},
			{"WaterDeep", waterColorDeep},
			{"WaterOceanDeep", waterColorDeep},
			{"WaterMovingDeep", waterColorDeep},
			{"WaterShallow", waterColorShallow},
			{"WaterOceanShallow", waterColorShallow},
			{"WaterMovingShallow", waterColorShallow}
		};

		public static IPromise<Texture2D> MakePreviewForSeed(string seed, int mapTile, int mapSize, MapGeneratorDef mapGenerator) {
			var promise = new Deferred<Texture2D>();
			LongEventHandler.ExecuteWhenFinished(() => {
				var beachMakerType = AccessTools.TypeByName("BeachMaker");
				try {
					var grids = GetMapGridsFromSeed(seed, mapTile, mapSize);
					DeepProfiler.Start("generateMapPreviewTexture");
					var terrainGenstep = new GenStep_Terrain();
					var riverMaker = AccessTools.Method(typeof(GenStep_Terrain), "GenerateRiver").Invoke(terrainGenstep, new object[] { grids.Map });
					var tex = new Texture2D(grids.Map.Size.x, grids.Map.Size.z);
					var beachTerrainAtDelegate =  (BeachMakerBeachTerrainAt)Delegate.CreateDelegate(typeof(BeachMakerBeachTerrainAt), null, AccessTools.Method(beachMakerType, "BeachTerrainAt"));
					var riverTerrainAtDelegate = riverMaker == null ? null : (RiverMakerTerrainAt)Delegate.CreateDelegate(typeof(RiverMakerTerrainAt), riverMaker, AccessTools.Method(riverMaker.GetType(), "TerrainAt"));
					AccessTools.Method(beachMakerType, "Init").Invoke(null, new object[] {grids.Map});
					
					foreach (var cell in CellRect.WholeMap(grids.Map)) {
						const float rockCutoff = .7f;
						//var terrainDef  = (TerrainDef)terrainFromMethod.Invoke(terrainGenstep, new[] { cell, grids.Map, grids.ElevationGrid[cell], grids.FertilityGrid[cell], riverMaker, false });
						//var terrainDef = getTerrain(cell, grids.Map, grids.ElevationGrid[cell], grids.FertilityGrid[cell], riverMaker, false);
						var terrainDef = TerrainFrom(cell, grids.Map, grids.ElevationGrid[cell], grids.FertilityGrid[cell], riverTerrainAtDelegate, beachTerrainAtDelegate, false);
						Color pixelColor;
						if (!terrainColors.TryGetValue(terrainDef.defName, out pixelColor)) {
							pixelColor = missingTerrainColor;
						}
						if (grids.ElevationGrid[cell] > rockCutoff) {
							pixelColor = solidStoneColor;
						}
						tex.SetPixel(cell.x, cell.z, pixelColor);
					}
					tex.Apply();
					foreach (var terrainPatchMaker in grids.Map.Biome.terrainPatchMakers) {
						terrainPatchMaker.Cleanup();
					}
					promise.Resolve(tex);
				} catch (Exception e) {
					Reroll2Controller.Instance.Logger.Error("Failed to generate map preview: "+e);
					promise.Reject();
				} finally {
					RockNoises.Reset();
					DeepProfiler.End();
					AccessTools.Method(beachMakerType, "Cleanup").Invoke(null, null);
				}
			});
			return promise;
		}

		private static TerrainDef TerrainFrom(IntVec3 c, Map map, float elevation, float fertility, RiverMakerTerrainAt riverTerrainAt, BeachMakerBeachTerrainAt beachTerrainAt, bool preferSolid) {
			TerrainDef riverTerrain = null;
			if (riverTerrainAt != null) {
				riverTerrain = riverTerrainAt(c);
			}
			if (riverTerrain == null && preferSolid) {
				return GenStep_RocksFromGrid.RockDefAt(c).naturalTerrain;
			}
			TerrainDef beachTerrain = beachTerrainAt(c, map.Biome);
			if (beachTerrain == TerrainDefOf.WaterOceanDeep) {
				return beachTerrain;
			}
			if (riverTerrain == TerrainDefOf.WaterMovingShallow || riverTerrain == TerrainDefOf.WaterMovingDeep) {
				return riverTerrain;
			}
			if (beachTerrain != null) {
				return beachTerrain;
			}
			if (riverTerrain != null) {
				return riverTerrain;
			}
			for (int i = 0; i < map.Biome.terrainPatchMakers.Count; i++) {
				beachTerrain = map.Biome.terrainPatchMakers[i].TerrainAt(c, map);
				if (beachTerrain != null) {
					return beachTerrain;
				}
			}
			if (elevation > 0.55f && elevation < 0.61f) {
				return TerrainDefOf.Gravel;
			}
			if (elevation >= 0.61f) {
				return GenStep_RocksFromGrid.RockDefAt(c).naturalTerrain;
			}
			beachTerrain = TerrainThreshold.TerrainAtValue(map.Biome.terrainsByFertility, fertility);
			if (beachTerrain != null) {
				return beachTerrain;
			}
			return TerrainDefOf.Sand;
		}

		private static MapElevationFertilityData GetMapGridsFromSeed(string seed, int mapTile, int mapSize) {
			var prevProgramState = Current.ProgramState;
			var prevSeed = Find.World.info.seedString;
			Find.World.info.seedString = seed;
			Current.ProgramState = ProgramState.MapInitializing;
			DeepProfiler.Start("generateMapPreviewGrids");
			try {
				var mapGeneratorData = Traverse.Create(typeof(MapGenerator)).Field("data").GetValue<Dictionary<string, object>>();
				mapGeneratorData.Clear();

				var map = CreateMapStub(mapSize, mapTile);
				foreach (var terrainPatchMaker in map.Biome.terrainPatchMakers) {
					terrainPatchMaker.Cleanup();
				}

				Rand.Seed = Gen.HashCombineInt(GenText.StableStringHash(seed), map.Tile);
				RockNoises.Init(map);

				var elevationFertilityGenstep = new GenStep_ElevationFertility();
				elevationFertilityGenstep.Generate(map);
				
				/*var terrainGenstep = new GenStep_Terrain();
				terrainGenstep.Generate(map);*/

				var result = new MapElevationFertilityData(MapGenerator.FloatGridNamed("Elevation", map), MapGenerator.FloatGridNamed("Fertility", map), map);
				mapGeneratorData.Clear();

				return result;
			} finally {
				DeepProfiler.End();
				Current.ProgramState = prevProgramState;
				Find.World.info.seedString = prevSeed;
			}
		}

		/// <summary>
		/// Make an absolute bare minimum map instance for grid generation.
		/// </summary>
		private static Map CreateMapStub(int mapSize, int mapTile) {
			var parent = new MapParent {Tile = mapTile};
			var map = new Map {
				info = {
					parent = parent,
					Size = new IntVec3(mapSize, 1, mapSize)
				}
			};
			map.cellIndices = new CellIndices(map);
/*			map.edificeGrid = new EdificeGrid(map);
			map.terrainGrid = new TerrainGrid(map);
			map.roofGrid = new RoofGrid(map);
			map.roofCollapseBuffer = new RoofCollapseBuffer();
			map.floodFiller = new FloodFiller(map);
			map.mapDrawer = new MapDrawer(map);
			map.pathGrid = new PathGrid(map);
			map.regionGrid = new RegionGrid(map);
			map.thingGrid = new ThingGrid(map);
			map.reachability = new Reachability(map);
			map.regionDirtyer = new RegionDirtyer(map);
			map.thingGrid = new ThingGrid(map);
			map.snowGrid = new SnowGrid(map);*/

			return map;
		}

		private class MapElevationFertilityData {
			public readonly MapGenFloatGrid ElevationGrid;
			public readonly MapGenFloatGrid FertilityGrid;
			public readonly Map Map;

			public MapElevationFertilityData(MapGenFloatGrid elevationGrid, MapGenFloatGrid fertilityGrid, Map map) {
				ElevationGrid = elevationGrid;
				FertilityGrid = fertilityGrid;
				Map = map;
			}
		}
	}
}