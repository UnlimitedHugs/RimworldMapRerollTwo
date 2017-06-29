using HugsLib;
using HugsLib.Settings;
using HugsLib.Utils;
using Verse;

namespace Reroll2 {
	public class Reroll2Controller : ModBase {
		internal const float MaxResourceBalance = 100f;

		public enum MapRerollType {
			Map, Geyser
		}

		public static Reroll2Controller Instance { get; private set; }
		private MapGeneratorDef lastUsedMapGenerator;
		private bool rerollInProgress;

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
		public SettingHandle<bool> LogConsumedResourcesSetting { get; private set; }
		public SettingHandle<bool> NoVomitingSetting { get; private set; }
		
		private GeyserRerollTool geyserReroll;

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
				RerollToolbox.SendMapStateSetEventToThings(map);
			}
			
			RerollToolbox.TryStopPawnVomiting(map);

			if (mapState.RerollGenerated && rerollInProgress) {
				rerollInProgress = false;
				RerollToolbox.SendMapRerolledEventToThings(map);
				RerollToolbox.KillMapIntroDialog();
				if (PaidRerollsSetting) {
					// adjust map to current remaining resources and charge for the reroll
					RerollToolbox.ReduceMapResources(map, 100 - mapState.ResourceBalance, 100);
					RerollToolbox.SubtractResourcePercentage(map, Resources.Settings.MapRerollSettings.mapRerollCost);
				}
			}
		}

		public void RerollMap() {
			if (rerollInProgress) {
				Logger.Error("Cannot reroll- map reroll already in progress");
				return;
			}
			rerollInProgress = true;
			RerollToolbox.DoMapReroll();
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
		}

		public override void Tick(int currentTick) {
			if (geyserReroll != null) geyserReroll.OnTick();
		}

		public void RecordUsedMapGenerator(MapGeneratorDef def) {
			lastUsedMapGenerator = def;
		}

		private void PrepareSettingsHandles() {
			SettingHandle.ShouldDisplay devModeVisible = () => Prefs.DevMode;

			PaidRerollsSetting = Settings.GetHandle("paidRerolls", "setting_paidRerolls_label".Translate(), "setting_paidRerolls_desc".Translate(), true);
			
			LogConsumedResourcesSetting = Settings.GetHandle("logConsumption", "setting_logConsumption_label".Translate(), "setting_logConsumption_desc".Translate(), false);
			LogConsumedResourcesSetting.VisibilityPredicate = devModeVisible;

			NoVomitingSetting = Settings.GetHandle("noVomiting", "setting_noVomiting_label".Translate(), "setting_noVomiting_desc".Translate(), false);
			NoVomitingSetting.VisibilityPredicate = devModeVisible;
		}
	}
}