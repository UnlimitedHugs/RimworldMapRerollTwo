using System;
using System.Collections.Generic;
using HugsLib;
using HugsLib.Settings;
using HugsLib.Utils;
using UnityEngine;
using Verse;

namespace Reroll2 {
	public class Reroll2Controller : ModBase {
		internal const float MaxResourceBalance = 100f;

		public enum MapRerollType {
			Map, Geyser
		}

		public enum MapGeneratorMode {
			AccuratePreviews, OriginalGenerator
		}

		public static Reroll2Controller Instance { get; private set; }

		private readonly MapPreviewGenerator previewGenerator = new MapPreviewGenerator();
		private readonly Queue<Action> scheduledMainThreadActions = new Queue<Action>();
		
		private MapGeneratorDef lastUsedMapGenerator;
		private bool generatorSeedPushed;

		public override string ModIdentifier {
			get { return "Reroll2"; }
		}

		internal new ModLogger Logger {
			get { return base.Logger; }
		}

		private RerollWorldState _worldState;

		public RerollWorldState WorldState {
			get { return _worldState ?? (_worldState = UtilityWorldObjectManager.GetUtilityWorldObject<RerollWorldState>()); }
		}

		public SettingHandle<bool> PaidRerollsSetting { get; private set; }
		public SettingHandle<bool> DeterministicRerollsSetting { get; private set; }
		public SettingHandle<bool> AntiCheeseSetting { get; private set; }
		public SettingHandle<bool> LogConsumedResourcesSetting { get; private set; }
		public SettingHandle<bool> NoVomitingSetting { get; private set; }
		public SettingHandle<MapGeneratorMode> MapGeneratorModeSetting { get; set; }

		private GeyserRerollTool geyserReroll;
		private Texture2D mapPreviewTex;

		private Reroll2Controller() {
			Instance = this;
		}

		public override void Initialize() {
			ReflectionCache.PrepareReflection();
			PrepareSettingsHandles();
		}

		public override void MapComponentsInitializing(Map map) {
			if (Find.GameInitData != null) {
				WorldState.StartingTile = Find.GameInitData.startingTile;
			}
		}

		public override void MapGenerated(Map map) {
			RerollToolbox.StoreGeneratedThingIdsInMapState(map);
			var mapState = RerollToolbox.GetStateForMap(map);
			mapState.UsedMapGenerator = lastUsedMapGenerator;
		}

		public override void MapLoaded(Map map) {
			geyserReroll = new GeyserRerollTool();
			var mapState = RerollToolbox.GetStateForMap(map);
			if (!mapState.RerollGenerated || !PaidRerollsSetting) {
				mapState.ResourceBalance = MaxResourceBalance;
			}
			
			RerollToolbox.TryStopPawnVomiting(map);

			if (mapState.RerollGenerated && mapState.RerollGenerated) {
				RerollToolbox.KillMapIntroDialog();
				if (PaidRerollsSetting) {
					// adjust map to current remaining resources and charge for the reroll
					RerollToolbox.ReduceMapResources(map, 100 - mapState.ResourceBalance, 100);
					RerollToolbox.SubtractResourcePercentage(map, Resources.Settings.MapRerollSettings.mapRerollCost);
				}
			}
			mapPreviewTex = null;
		}

		public void RerollGeysers() {
			geyserReroll.DoReroll();
			if (PaidRerollsSetting) {
				RerollToolbox.SubtractResourcePercentage(Find.VisibleMap, Resources.Settings.MapRerollSettings.geyserRerollCost);
			}
		}

		public bool CanAffordOperation(MapRerollType type) {
			float cost = 0;
			switch (type) {
				case MapRerollType.Map: cost = Resources.Settings.MapRerollSettings.mapRerollCost; break;
				case MapRerollType.Geyser: cost = Resources.Settings.MapRerollSettings.geyserRerollCost; break;
			}
			var mapState = RerollToolbox.GetStateForMap();
			return !PaidRerollsSetting || mapState.ResourceBalance >= cost;
		}

		public bool GeyserRerollInProgress {
			get { return geyserReroll.RerollInProgress; }
		}

		public override void Update() {
			if (geyserReroll != null) geyserReroll.OnUpdate();
			while (scheduledMainThreadActions.Count > 0) {
				scheduledMainThreadActions.Dequeue()();
			}

		}

		public override void Tick(int currentTick) {
			if (geyserReroll != null) geyserReroll.OnTick();
		}

		public override void OnGUI() {
			if (GUI.Button(new Rect(10, 10, 100, 30), "View this")) {
				var currentMap = Find.VisibleMap;
				var state = RerollToolbox.GetStateForMap(currentMap);
				var seed = state.RerollSeed ?? Find.World.info.seedString;
				previewGenerator.QueuePreviewForSeed(seed, currentMap.Tile, currentMap.Size.x).Done(t => mapPreviewTex = t);
			}
			
			if (GUI.Button(new Rect(10, 50, 100, 30), "Preview next")) {
				var currentMap = Find.VisibleMap;
				var state = RerollToolbox.GetStateForMap(currentMap);
				var seed = RerollToolbox.GetNextRerollSeed(RerollToolbox.CurrentMapSeed(state));
				for (int i = 0; i < 9; i++) {
					previewGenerator.QueuePreviewForSeed(seed+i, currentMap.Tile, currentMap.Size.x).Done(t => mapPreviewTex = t);	
				}
			}
			if (mapPreviewTex != null) {
				GUI.DrawTexture(new Rect(10, 90, 400, 400), mapPreviewTex, ScaleMode.ScaleToFit, true);
			}
		}

		public void RecordUsedMapGenerator(MapGeneratorDef def) {
			lastUsedMapGenerator = def;
		}

		public void TryPushDeterministicRandState(Map map, int seed) {
			if (MapGeneratorModeSetting.Value == MapGeneratorMode.AccuratePreviews) {
				var deterministicSeed = Gen.HashCombineInt(GenText.StableStringHash(Find.World.info.seedString+seed), map.Tile);
				Rand.PushState(deterministicSeed);
				generatorSeedPushed = true;
			}
		}

		public void TryPopDeterministicRandState() {
			if (generatorSeedPushed) {
				generatorSeedPushed = false;
				Rand.PopState();
			}
		}

		public void ExecuteInMainThread(Action action) {
			scheduledMainThreadActions.Enqueue(action);
		}

		private void PrepareSettingsHandles() {
			SettingHandle.ShouldDisplay devModeVisible = () => Prefs.DevMode;

			PaidRerollsSetting = Settings.GetHandle("paidRerolls", "setting_paidRerolls_label".Translate(), "setting_paidRerolls_desc".Translate(), true);
			
			DeterministicRerollsSetting = Settings.GetHandle("deterministicRerolls", "setting_deterministicRerolls_label".Translate(), "setting_deterministicRerolls_desc".Translate(), true);

			AntiCheeseSetting = Settings.GetHandle("antiCheese", "setting_antiCheese_label".Translate(), "setting_antiCheese_desc".Translate(), true);
			AntiCheeseSetting.VisibilityPredicate = devModeVisible;

			LogConsumedResourcesSetting = Settings.GetHandle("logConsumption", "setting_logConsumption_label".Translate(), "setting_logConsumption_desc".Translate(), false);
			LogConsumedResourcesSetting.VisibilityPredicate = devModeVisible;

			NoVomitingSetting = Settings.GetHandle("noVomiting", "setting_noVomiting_label".Translate(), "setting_noVomiting_desc".Translate(), false);
			NoVomitingSetting.VisibilityPredicate = devModeVisible;

			MapGeneratorModeSetting = Settings.GetHandle("mapGeneratorMode", "setting_mapGeneratorMode_label".Translate(), "setting_mapGeneratorMode_desc".Translate(), MapGeneratorMode.AccuratePreviews, null, "setting_mapGeneratorMode_");
		}
	}
}