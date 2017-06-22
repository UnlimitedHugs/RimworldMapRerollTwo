using HugsLib;
using HugsLib.Utils;
using UnityEngine;
using Verse;

namespace Reroll2 {
	public class Reroll2Controller : ModBase {
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

		private GeyserRerollTool geyserReroll;

		private Reroll2Controller() {
			Instance = this;
		}

		public override void MapComponentsInitializing(Map map) {
			if (Find.GameInitData != null) {
				WorldState.StartingTile = Find.GameInitData.startingTile;
			}
		}

		public override void MapGenerated(Map map) {
			RerollToolbox.StoreGeneratedThingIdsInMapState(map);
			RerollToolbox.GetStateForMap(map).UsedMapGenerator = lastUsedMapGenerator;
		}

		public override void MapLoaded(Map map) {
			geyserReroll = new GeyserRerollTool();
			if (RerollToolbox.GetStateForMap(map).RerollGenerated && rerollInProgress) {
				rerollInProgress = false;
				RerollToolbox.KillMapIntroDialog();
				RerollToolbox.TrySelectMonument(map);
			}
		}

		public override void OnGUI() {
			if (Widgets.ButtonText(new Rect(50f, 50f, 200f, 30f), "Map")) {
				RerollToolbox.DoMapReroll();
			}
			if (Widgets.ButtonText(new Rect(50f, 50f+30f+10f, 200f, 30f), "Geysers")) {
				geyserReroll.DoReroll();
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
	}
}