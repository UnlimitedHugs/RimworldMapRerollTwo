using System;
using System.Collections.Generic;
using System.Linq;
using Harmony;
using Promises;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Reroll2 {
	public static class MapPreviewGenerator {
		private static Color defaultTerrainColor = GenColor.FromHex("594A3B");
		private static Color missingTerrainColor = Color.gray;
		private static Color solidStoneColor = GenColor.FromHex("36271C");
		private static Color waterColorDeep = GenColor.FromHex("3A434D");
		private static Color waterColorShallow = GenColor.FromHex("434F50");

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
				try {
					var grids = GetMapGridsFromSeed(seed, mapTile, mapSize, mapGenerator);
					DeepProfiler.Start("generateMapPreviewTexture");
					var tex = new Texture2D(grids.MapBounds.Width, grids.MapBounds.Height);
					var terrainGenstep = new GenStep_Terrain();
					var riverMaker = AccessTools.Method(typeof(GenStep_Terrain), "GenerateRiver").Invoke(terrainGenstep, new object[] {grids.Map});
					var terrainFromMethod = AccessTools.Method(typeof(GenStep_Terrain), "TerrainFrom");
					//TerrainFrom(IntVec3 c, Map map, float elevation, float fertility, RiverMaker river, bool preferSolid)
					foreach (var cell in grids.MapBounds) {
						var rockCutoff = .7f;
						var terrainDef  = (TerrainDef)terrainFromMethod.Invoke(terrainGenstep, new[] { cell, grids.Map, grids.ElevationGrid[cell], grids.FertilityGrid[cell], riverMaker, false });
						Color pixelColor;
						if (!terrainColors.TryGetValue(terrainDef.defName, out pixelColor)) {
							pixelColor = missingTerrainColor;
						}
						//pixelColor = ((Texture2D)terrainDef.DrawMatSingle.mainTexture).GetPixel(0,0);
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
					promise.Reject();
					Reroll2Controller.Instance.Logger.Error("Failed to generate map preview: "+e);
				} finally {
					RockNoises.Reset();
					DeepProfiler.End();
				}
			});
			return promise;
		}

		private static MapElevationFertilityData GetMapGridsFromSeed(string seed, int mapTile, int mapSize, MapGeneratorDef mapGenerator) {
			var prevProgramState = Current.ProgramState;
			var prevSeed = Find.World.info.seedString;
			Find.World.info.seedString = seed;
			Current.ProgramState = ProgramState.MapInitializing;
			DeepProfiler.Start("generateMapPreviewGrids");
			try {
				var genstepsInOrder = mapGenerator.GenSteps.OrderBy(g => g.order).ThenBy(g => g.index).ToList();
				var terrainGenstepIndex = genstepsInOrder.FindIndex(def => def.genStep is GenStep_ElevationFertility);
				if (terrainGenstepIndex < 0) {
					throw new Exception("Cannot generate preview- map generator does not have a GenStep_ElevationFertility: " + mapGenerator);
				}
				var mapGeneratorData = Traverse.Create(typeof(MapGenerator)).Field("data").GetValue<Dictionary<string, object>>();
				mapGeneratorData.Clear();

				var map = CreateMapStub(mapSize, mapTile);
				foreach (var terrainPatchMaker in map.Biome.terrainPatchMakers) {
					terrainPatchMaker.Cleanup();
				}

				Rand.Seed = Gen.HashCombineInt(GenText.StableStringHash(seed), map.Tile);
				RockNoises.Init(map);
				var significantGensteps = genstepsInOrder.Take(terrainGenstepIndex + 1).Select(g => g.genStep);
				foreach (var genstep in significantGensteps) {
					genstep.Generate(map);
				}

				var result = new MapElevationFertilityData(MapGenerator.FloatGridNamed("Elevation", map), MapGenerator.FloatGridNamed("Fertility", map), CellRect.WholeMap(map), map);

				//RockNoises.Reset();
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

			return map;
		}

		private class MapElevationFertilityData {
			public readonly MapGenFloatGrid ElevationGrid;
			public readonly MapGenFloatGrid FertilityGrid;
			public readonly CellRect MapBounds;
			public readonly Map Map;

			public MapElevationFertilityData(MapGenFloatGrid elevationGrid, MapGenFloatGrid fertilityGrid, CellRect mapBounds, Map map) {
				ElevationGrid = elevationGrid;
				FertilityGrid = fertilityGrid;
				MapBounds = mapBounds;
				Map = map;
			}
		}
	}
}