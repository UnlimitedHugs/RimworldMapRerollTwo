using System.Collections.Generic;
using Verse;

namespace Reroll2 {
	public class RerollMapState : IExposable {
		private List<int> _scenarioGeneratedThingIds;

		public List<int> ScenarioGeneratedThingIds {
			get { return _scenarioGeneratedThingIds ?? (_scenarioGeneratedThingIds = new List<int>()); }
		}

		public void ExposeData() {
			Scribe_Collections.Look(ref _scenarioGeneratedThingIds, "scenarioGeneratedThingIds", LookMode.Value);
		}
	}
}