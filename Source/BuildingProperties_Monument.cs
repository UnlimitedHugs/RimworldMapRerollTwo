using RimWorld;
using Verse;

namespace Reroll2 {
	public class BuildingProperties_Monument : BuildingProperties {
		public bool destructionLightningStrike = true;
		public float destructionExplosionRadius = 6;
		public DamageDef destructionExplosionDamage = DamageDefOf.Flame;
		public SoundDef destructionSound;
	}
}