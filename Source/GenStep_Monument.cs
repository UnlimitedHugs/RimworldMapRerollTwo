using Verse;

namespace Reroll2 {
	public class GenStep_Monument : GenStep {
		private const int NumPlaceAttempts = 10;
		
		public override void Generate(Map map) {
			if (!map.IsPlayerHome) return;
			for (int i = 1; i <= NumPlaceAttempts; i++) {
				var searchRadius = ((map.Size.x / 2) / NumPlaceAttempts) * i;
				if (TryPlaceMonument(map, searchRadius)) {
					break;
				}
			}
		}

		private bool TryPlaceMonument(Map map, int searchRadius) {
			var cell = TryFindRandomCellForMoument(map, searchRadius);
			if (!cell.IsValid) return false;
			GenSpawn.Spawn(Resources.Thing.RerollMonument, cell, map);
			return true;
		}

		private IntVec3 TryFindRandomCellForMoument(Map map, int searchRadius) {
			IntVec3 cell;
			var monumentDef = Resources.Thing.RerollMonument;
			var found = CellFinder.TryFindRandomCellNear(map.Center, map, searchRadius,
				pos => !GenSpawn.WouldWipeAnythingWith(GenAdj.OccupiedRect(pos, Rot4.North, monumentDef.Size), monumentDef, map, t => t is Building)
				, out cell);
			return found ? cell : IntVec3.Invalid;
		}
	}
}