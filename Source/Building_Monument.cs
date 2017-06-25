using System;
using System.Collections.Generic;
using HugsLib.Utils;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Reroll2 {
	public class Building_Monument : Building, IRerollEventReceiver {
		private const float MaxSpeed = 360f*4f;
		private const float MinSpeed = 15f;
		private const float MinGlow = .5f;
		private const float HueIncrementPerSecondSlow = .03f;
		private const float HueIncrementPerSecondFast = 8.64f;
		private const float SpeedTransitionDuration = 2f;
		private const float ScreenShakeMultiplier = .55f;

		private enum PendingOperationType {
			None,
			MapReroll,
			GeyserReroll
		}

		// 0-1
		public float GlowAlpha { get; private set; }
		
		// 0-1
		public float GlowColorHue { get; private set; }
		
		// 0-1
		public float GlowColorSaturation { get; private set; }

		// 0-1
		public float RadialAlpha { get; private set; }

		// 0-360
		public float DiceRotation { get; private set; }

		public Graphic_Monument_Materials Materials { get; set; }

		private BuildingProperties_Monument Props {
			get { return def.building as BuildingProperties_Monument; }
		}

		private bool OperationInProgress {
			get { return pendingOperation != PendingOperationType.None; }
		}

		private ValueInterpolator speedInterpolator;
		private PendingOperationType pendingOperation;
		private Sustainer droneSustainer;
		private bool isActive;

		public void OnMapRerolled() {
			Find.Selector.ClearSelection();
			Find.Selector.Select(this, false);
			isActive = true;
			SetFaction(Faction.OfPlayer);
			SpinDown();
		}

		public override void ExposeData() {
			base.ExposeData();
			Scribe_Deep.Look(ref speedInterpolator, "speedInterpolator");
			Scribe_Values.Look(ref pendingOperation, "pendingOperation", PendingOperationType.None);
			Scribe_Values.Look(ref isActive, "isActive", false);
		}

		public override void SpawnSetup(Map map, bool respawningAfterLoad) {
			base.SpawnSetup(map, respawningAfterLoad);
			if (speedInterpolator == null) {
				speedInterpolator = new ValueInterpolator(MinSpeed);
			}
			speedInterpolator.SetFinishedCallback(OnSpeedInterpolationFinsihed);
			if (!respawningAfterLoad) {
				SpinDown();
			}
			if (Props == null) {
				Reroll2Controller.Instance.Logger.Error("Building_Monument requires a BuildingProperties_Monument");
				Destroy();
			}
			LongEventHandler.ExecuteWhenFinished(delegate {
				var droneInfo = SoundInfo.InMap(this, MaintenanceType.None);
				droneSustainer = Resources.Sound.RerollMonumentDrone.TrySpawnSustainer(droneInfo);
				if (droneSustainer != null) {
					SetSustainerVolume(droneSustainer, 0);
				}
			});
		}

		public override void SetFaction(Faction newFaction, Pawn recruiter = null) {
			var oldFaction = factionInt;
			base.SetFaction(newFaction, recruiter);
			if (!isActive && oldFaction != newFaction && newFaction.IsPlayer) {
				isActive = true;
				speedInterpolator.value = 0;
				SpinUp();
				pendingOperation = PendingOperationType.None;
				Resources.Sound.RerollMonumentStartup.PlayOneShot(this);
			}
		}

		/*public override void Tick() {
			base.Tick();
			if (droneSustainer != null) {
				droneSustainer.Maintain();
			}
		}*/

		public override void Draw() {
			if (!Find.TickManager.Paused) {
				if (isActive) {
					var rotationSpeed = speedInterpolator.Update();
					DiceRotation = (DiceRotation + rotationSpeed * Time.deltaTime) % 360;
					var proportionalRotationSpeed = Mathf.Clamp01((rotationSpeed - MinSpeed) / (MaxSpeed - MinSpeed));
					RadialAlpha = proportionalRotationSpeed;
					GlowColorHue = (GlowColorHue + Mathf.Lerp(HueIncrementPerSecondSlow, HueIncrementPerSecondFast, proportionalRotationSpeed) * Time.deltaTime) % 1f;
					GlowAlpha = MinGlow + proportionalRotationSpeed * (1f - MinGlow);
					GlowColorSaturation = proportionalRotationSpeed / 2f + .5f;
					Find.CameraDriver.shaker.DoShake(proportionalRotationSpeed * ScreenShakeMultiplier * Time.deltaTime);
					if (droneSustainer != null && !speedInterpolator.finished) {
						SetSustainerVolume(droneSustainer, proportionalRotationSpeed);
					}
				} else {
					DiceRotation = 0;
					RadialAlpha = 0;
					GlowAlpha = 0;
				}
			}
			base.Draw();
		}

		public override void Destroy(DestroyMode mode = DestroyMode.Vanish) {
			if (mode == DestroyMode.KillFinalize) {
				if (Props.destructionLightningStrike) {
					Map.weatherManager.eventHandler.AddEvent(new WeatherEvent_LightningStrike(Map, Position));
				}
				if (Props.destructionExplosionRadius > 0) {
					GenExplosion.DoExplosion(Position, Map, Props.destructionExplosionRadius, DamageDefOf.Flame, this, null, null, null, ThingDefOf.RockRubble, .4f);
				}
				if (Props.destructionSound!=null) {
					Props.destructionSound.PlayOneShotOnCamera(Map);
				}
			}
			EndSustainer();
			base.Destroy(mode);
		}

		private void EndSustainer() {
			if (droneSustainer != null && !droneSustainer.Ended) {
				droneSustainer.End();
			}
		}

		public override IEnumerable<Gizmo> GetGizmos() {
			foreach (var gizmo in base.GetGizmos()) {
				yield return gizmo;
			}
			if (isActive) {
				var controlsDisabled = OperationInProgress || Reroll2Controller.Instance.GeyserRerollInProgress;
				var disabledReason = controlsDisabled ? "Reroll2_rerollInProgress".Translate() : null;
				yield return new Command_Action {
					defaultLabel = "Reroll2_rerollMap".Translate(),
					disabled = controlsDisabled,
					disabledReason = disabledReason,
					icon = Resources.Textures.UIRerollMap,
					action = RerollMapAction
				};
				yield return new Command_Action {
					defaultLabel = "Reroll2_rerollGeysers".Translate(),
					disabled = controlsDisabled,
					disabledReason = disabledReason,
					icon = Resources.Textures.UIRerollGeysers,
					action = RerollGeysersAction
				};
			}
		}

		private void RerollMapAction() {
			pendingOperation = PendingOperationType.MapReroll;
			SpinUp();
		}

		private void RerollGeysersAction() {
			pendingOperation = PendingOperationType.GeyserReroll;
			SpinUp();
		}

		private void OnSpeedInterpolationFinsihed(ValueInterpolator interpolator, float finalvalue, float interpolationduration, InterpolationCurves.Curve interpolationcurve) {
			switch (pendingOperation) {
				case PendingOperationType.MapReroll:
					EndSustainer();
					Reroll2Controller.Instance.RerollMap();
					break;
				case PendingOperationType.GeyserReroll:
					Reroll2Controller.Instance.RerollGeysers();
					SpinDown();
					break;
				case PendingOperationType.None:
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}
			pendingOperation = PendingOperationType.None;
			if (finalvalue == MaxSpeed) {
				SpinDown();
			}
		}

		private void SpinUp() {
			speedInterpolator.StartInterpolation(MaxSpeed, SpeedTransitionDuration, InterpolationCurves.CubicEaseInOut);
		}

		private void SpinDown() {
			speedInterpolator.value = MaxSpeed;
			speedInterpolator.StartInterpolation(MinSpeed, SpeedTransitionDuration, InterpolationCurves.CubicEaseInOut);
		}

		private void SetSustainerVolume(Sustainer sus, float volume) {
			var subSustainers = (List<SubSustainer>)ReflectionCache.Sustainer_SubSustainers.GetValue(sus);
			if (subSustainers == null) return;
			for (var i = 0; i < subSustainers.Count; i++) {
				var sub = subSustainers[i];
				var samples = (List<SampleSustainer>)ReflectionCache.SubSustainer_Samples.GetValue(sub);
				if (samples == null) continue;
				for (var j = 0; j < samples.Count; j++) {
					var sample = samples[j];
					sample.resolvedVolume = volume;
				}
			}
		}

		public override string GetInspectString() {
			return RadialAlpha.ToString();
		}
	}

}