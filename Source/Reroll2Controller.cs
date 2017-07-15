﻿using System;
using System.Collections.Generic;
using HugsLib;
using HugsLib.Settings;
using HugsLib.Utils;
using Verse;

namespace Reroll2 {
	public class Reroll2Controller : ModBase {
		internal const float MaxResourceBalance = 100f;
		
		public enum MapGeneratorMode {
			AccuratePreviews, OriginalGenerator
		}

		public static Reroll2Controller Instance { get; private set; }

		private readonly Queue<Action> scheduledMainThreadActions = new Queue<Action>();
		
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

		private MapGeneratorDef lastUsedMapGenerator;
		private GeyserRerollTool geyserReroll;
		private bool generatorSeedPushed;
		private bool pauseScheduled;

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

			if (pauseScheduled) {
				pauseScheduled = false;
				ExecuteInMainThread(() => Find.TickManager.CurTimeSpeed = TimeSpeed.Paused);
			}

			RerollToolbox.TryStopPawnVomiting(map);

			if (mapState.RerollGenerated && mapState.RerollGenerated) {
				RerollToolbox.KillMapIntroDialog();
				if (PaidRerollsSetting) {
					// adjust map to current remaining resources and charge for the reroll
					RerollToolbox.ReduceMapResources(map, 100 - mapState.ResourceBalance, 100);
				}
			}
		}

		public void RerollGeysers() {
			geyserReroll.DoReroll();
			if (PaidRerollsSetting) {
				RerollToolbox.SubtractResourcePercentage(Find.VisibleMap, Resources.Settings.MapRerollSettings.geyserRerollCost);
			}
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

		public void PauseOnNextLoad() {
			pauseScheduled = true;
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