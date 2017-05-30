using HugsLib.Utils;
using Reroll2;
using Verse;

namespace Reroll2 {
	/// <summary>
	/// Wrapper to allow RerollMapState to be stored inside a map
	/// </summary>
	public class MapComponent_RerollMapState : MapComponent {
		public RerollMapState State;
		
		public MapComponent_RerollMapState(Map map) : base(map) {
			this.EnsureIsActive();
		}

		public override void ExposeData() {
			base.ExposeData();
			Scribe_Deep.Look(ref State, "state");
		}
	}
}