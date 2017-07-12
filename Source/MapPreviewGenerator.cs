﻿using System;
using System.Collections.Generic;
using System.Threading;
using Reroll2.Promises;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Reroll2 {
	/// <summary>
	/// Given a map location and seed, generates an approximate preview texture of how the map would look once generated.
	/// </summary>
	public class MapPreviewGenerator : IDisposable {
		private delegate TerrainDef RiverMakerTerrainAt(IntVec3 c);
		private delegate TerrainDef BeachMakerBeachTerrainAt(IntVec3 c, BiomeDef biome);

		private static readonly Color defaultTerrainColor = GenColor.FromHex("6D5B49");
		private static readonly Color missingTerrainColor = new Color(0.38f, 0.38f, 0.38f);
		private static readonly Color solidStoneColor = GenColor.FromHex("36271C");
		private static readonly Color solidStoneHighlightColor = GenColor.FromHex("4C3426");
		private static readonly Color solidStoneShadowColor = GenColor.FromHex("1C130E");
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

		private Thread workerThread;
		private EventWaitHandle workHandle = new AutoResetEvent(false);
		private EventWaitHandle disposeHandle = new AutoResetEvent(false);
		private EventWaitHandle mainThreadHandle = new AutoResetEvent(false);
		private Queue<QueuedPreviewRequest> queuedRequests = new Queue<QueuedPreviewRequest>();

		public IPromise<Texture2D> QueuePreviewForSeed(string seed, int mapTile, int mapSize) {
			if (disposeHandle == null) {
				throw new Exception("MapPreviewGenerator has already been disposed.");
			}
			var promise = new Promise<Texture2D>();
			if (workerThread == null) {
				workerThread = new Thread(DoThreadWork);
				workerThread.Start();
			}
			queuedRequests.Enqueue(new QueuedPreviewRequest(promise, seed, mapTile, mapSize));
			workHandle.Set();
			return promise;
		}

		private void DoThreadWork() {
			QueuedPreviewRequest request = null;
			try {
				while (queuedRequests.Count > 0 || WaitHandle.WaitAny(new WaitHandle[] {workHandle, disposeHandle}) == 0) {
					if (queuedRequests.Count > 0) {
						var req = queuedRequests.Dequeue();
						request = req;
						Texture2D texture = null;
						WaitForExecutionInMainThread(() => {
							// textures must be instantiated in the main thread
							texture = new Texture2D(req.MapSize, req.MapSize, TextureFormat.RGB24, false);
							texture.Apply();
						});

						try {
							if (texture == null) {
								throw new Exception("Could not create required texture.");
							}
							GeneratePreviewForSeed(req.Seed, req.MapTile, req.MapSize, texture);
						} catch (Exception e) {
							Reroll2Controller.Instance.Logger.Error("Failed to generate map preview: " + e);
							texture = null;
						}
						if (texture != null) {
							WaitForExecutionInMainThread(() => {
								// upload in main thread
								texture.Apply();
							});
						}
						WaitForExecutionInMainThread(() => {
							if (texture == null) {
								req.Promise.Reject(null);
							} else {
								req.Promise.Resolve(texture);
							}
						});
					}
				}
			} catch (Exception e) {
				Reroll2Controller.Instance.Logger.Error("Exception in preview generator thread: " + e);
				if (request != null) {
					request.Promise.Reject(e);
				}
			}
		}

		public void Dispose() {
			if (disposeHandle == null) {
				throw new Exception("MapPreviewGenerator has already been disposed.");
			}
			disposeHandle.Close();
			workHandle.Close();
			mainThreadHandle.Close();
			mainThreadHandle = disposeHandle = workHandle = null;
		}

		/// <summary>
		/// Block until delegate is executed or times out
		/// </summary>
		private void WaitForExecutionInMainThread(Action action) {
			Reroll2Controller.Instance.ExecuteInMainThread(() => {
				action();
				mainThreadHandle.Set();
			});
			mainThreadHandle.WaitOne(1000);
		}

		private static void GeneratePreviewForSeed(string seed, int mapTile, int mapSize, Texture2D targetTexture) {
			var prevSeed = Find.World.info.seedString;
			Find.World.info.seedString = seed;
			
				try {
					var grids = GenerateMapGrids(mapTile, mapSize);
					DeepProfiler.Start("generateMapPreviewTexture");
					var terrainGenstep = new GenStep_Terrain();
					var riverMaker = ReflectionCache.GenStepTerrain_GenerateRiver.Invoke(terrainGenstep, new object[] { grids.Map });
					var beachTerrainAtDelegate =  (BeachMakerBeachTerrainAt)Delegate.CreateDelegate(typeof(BeachMakerBeachTerrainAt), null, ReflectionCache.BeachMaker_BeachTerrainAt);
					var riverTerrainAtDelegate = riverMaker == null ? null : (RiverMakerTerrainAt)Delegate.CreateDelegate(typeof(RiverMakerTerrainAt), riverMaker, ReflectionCache.RiverMaker_TerrainAt);
					ReflectionCache.BeachMaker_Init.Invoke(null, new object[] {grids.Map});

					var mapBounds = CellRect.WholeMap(grids.Map);
					foreach (var cell in mapBounds) {
						const float rockCutoff = .7f;
						var terrainDef = TerrainFrom(cell, grids.Map, grids.ElevationGrid[cell], grids.FertilityGrid[cell], riverTerrainAtDelegate, beachTerrainAtDelegate, false);
						Color pixelColor;
						if (!terrainColors.TryGetValue(terrainDef.defName, out pixelColor)) {
							pixelColor = missingTerrainColor;
						}
						if (grids.ElevationGrid[cell] > rockCutoff) {
							pixelColor = solidStoneColor;
						}
						targetTexture.SetPixel(cell.x, cell.z, pixelColor);
					}

					AddBevelToSolidStone(targetTexture);
					
					foreach (var terrainPatchMaker in grids.Map.Biome.terrainPatchMakers) {
						terrainPatchMaker.Cleanup();
					}
				} finally {
					RockNoises.Reset();
					DeepProfiler.End();
					Find.World.info.seedString = prevSeed;
					ReflectionCache.BeachMaker_Cleanup.Invoke(null, null);
				}
		}

		/// <summary>
		/// Identifies the terrain def that would have been used at the given map location.
		/// Swiped from GenStep_Terrain. Extracted for performance reasons.
		/// </summary>
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

		/// <summary>
		/// Adds highlights and shadows to the solid stone color in the texture
		/// </summary>
		private static void AddBevelToSolidStone(Texture2D tex) {
			for (int x = 0; x < tex.width; x++) {
				for (int y = 0; y < tex.height; y++) {
					var isStone = tex.GetPixel(x, y) == solidStoneColor;
					if (isStone) {
						var colorBelow = y > 0 ? tex.GetPixel(x, y - 1) : Color.clear;
						var isStoneBelow = colorBelow == solidStoneColor || colorBelow == solidStoneHighlightColor || colorBelow == solidStoneShadowColor;
						var isStoneAbove = y < tex.height - 1 && tex.GetPixel(x, y + 1) == solidStoneColor;
						if (!isStoneAbove) {
							tex.SetPixel(x, y, solidStoneHighlightColor);
						} else if (!isStoneBelow) {
							tex.SetPixel(x, y, solidStoneShadowColor);
						}
					}
				}
			}
		}

		/// <summary>
		/// Generate a minimal map with elevation and fertility grids
		/// </summary>
		private static MapElevationFertilityData GenerateMapGrids(int mapTile, int mapSize) {
			DeepProfiler.Start("generateMapPreviewGrids");
			try {
				var mapGeneratorData = (Dictionary<string, object>)ReflectionCache.MapGenerator_Data.GetValue(null);
				mapGeneratorData.Clear();

				var map = CreateMapStub(mapSize, mapTile);
				foreach (var terrainPatchMaker in map.Biome.terrainPatchMakers) {
					terrainPatchMaker.Cleanup();
				}

				Rand.Seed = Gen.HashCombineInt(Find.World.info.Seed, map.Tile);
				RockNoises.Init(map);

				var elevationFertilityGenstep = new GenStep_ElevationFertility();
				elevationFertilityGenstep.Generate(map);
				
				var result = new MapElevationFertilityData(MapGenerator.FloatGridNamed("Elevation", map), MapGenerator.FloatGridNamed("Fertility", map), map);
				mapGeneratorData.Clear();

				return result;
			} finally {
				DeepProfiler.End();
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
			public readonly Map Map;

			public MapElevationFertilityData(MapGenFloatGrid elevationGrid, MapGenFloatGrid fertilityGrid, Map map) {
				ElevationGrid = elevationGrid;
				FertilityGrid = fertilityGrid;
				Map = map;
			}
		}

		private class QueuedPreviewRequest {
			public readonly Promise<Texture2D> Promise;
			public readonly string Seed;
			public readonly int MapTile;
			public readonly int MapSize;

			public QueuedPreviewRequest(Promise<Texture2D> promise, string seed, int mapTile, int mapSize) {
				Promise = promise;
				Seed = seed;
				MapTile = mapTile;
				MapSize = mapSize;
			}
		}
	}
}