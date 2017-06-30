using System;
using System.Collections.Generic;
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
			None, MapReroll, GeyserReroll
		}

		private enum MonumentState {
			Unclaimed, Active, Inert
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
		private Gizmo_ResourceBalance resourceBalanceGizmo;
		private MonumentState state;
		private ValueInterpolator startupFlicker;

		public void OnMapRerolled() {
			Find.Selector.ClearSelection();
			Find.Selector.Select(this, false);
			state = MonumentState.Active;
			SetFaction(Faction.OfPlayer);
			SpinDownFromMax();
		}

		public void OnMapStateSet() {
			resourceBalanceGizmo = new Gizmo_ResourceBalance(Map);
		}

		public void OnResourceRockMined() {
			if (state != MonumentState.Inert) {
				state = MonumentState.Inert;
				var mapState = RerollToolbox.GetStateForMap(Map);
				mapState.HasActiveMonument = false;
				pendingOperation = PendingOperationType.None;
				RerollToolbox.ReceiveMonumentDeactivationLetter(this);
				SpinDownToZero();
			}
		}

		public override void ExposeData() {
			base.ExposeData();
			Scribe_Deep.Look(ref speedInterpolator, "speedInterpolator");
			Scribe_Values.Look(ref pendingOperation, "pendingOperation", PendingOperationType.None);
			Scribe_Values.Look(ref state, "state", MonumentState.Unclaimed);
		}

		public override void SpawnSetup(Map map, bool respawningAfterLoad) {
			base.SpawnSetup(map, respawningAfterLoad);
			if (speedInterpolator == null) {
				speedInterpolator = new ValueInterpolator(MinSpeed);
			}
			speedInterpolator.SetFinishedCallback(OnSpeedInterpolationFinished);
			if (Props == null) {
				Reroll2Controller.Instance.Logger.Error("Building_Monument requires a BuildingProperties_Monument");
				Destroy();
			}
			if (state == MonumentState.Unclaimed) {
				SetFaction(null);
			}
			if (state != MonumentState.Inert) {
				RerollToolbox.GetStateForMap(map).HasActiveMonument = true;
			}
			LongEventHandler.ExecuteWhenFinished(delegate {
				//var droneInfo = SoundInfo.InMap(this);
				//droneSustainer = Resources.Sound.RerollMonumentDrone.TrySpawnSustainer(droneInfo);
				if (droneSustainer != null) {
					SetSustainerVolume(droneSustainer, 0);
				}
			});
		}
		
		public override void SetFaction(Faction newFaction, Pawn recruiter = null) {
			base.SetFaction(newFaction, recruiter);
			if (state == MonumentState.Unclaimed && newFaction != null && newFaction.IsPlayer) {
				state = MonumentState.Active;
				speedInterpolator.value = 0;
				SpinUp();
				startupFlicker = new ValueInterpolator().SetFinishedCallback(OnBlinkerInterpolationFinished).StartInterpolation(1f, SpeedTransitionDuration, InterpolationCurves.Linear);
				resourceBalanceGizmo = new Gizmo_ResourceBalance(Map, 0);
				pendingOperation = PendingOperationType.None;
				Resources.Sound.RerollMonumentStartup.PlayOneShot(this);
			}
		}

		public override void Draw() {
			if (!Find.TickManager.Paused) {
				if (state == MonumentState.Active || !speedInterpolator.finished) {
					var rotationSpeed = speedInterpolator.Update();
					DiceRotation = (DiceRotation + rotationSpeed * Time.deltaTime) % 360;
					var proportionalRotationSpeed = Mathf.Clamp01((rotationSpeed - MinSpeed) / (MaxSpeed - MinSpeed));
					RadialAlpha = proportionalRotationSpeed;
					GlowColorHue = (GlowColorHue + Mathf.Lerp(HueIncrementPerSecondSlow, HueIncrementPerSecondFast, proportionalRotationSpeed) * Time.deltaTime) % 1f;
					GlowAlpha = MinGlow + proportionalRotationSpeed * (1f - MinGlow);
					if (startupFlicker!=null) {
						var blinkerProgress = startupFlicker.Update();
						GlowAlpha = Rand.Range(blinkerProgress, 1f);
					}
					GlowColorSaturation = proportionalRotationSpeed / 2f + .5f;
					if (!Reroll2Controller.Instance.PaidRerollsSetting) {
						GlowColorSaturation = 0;
					}
					Find.CameraDriver.shaker.DoShake(proportionalRotationSpeed * ScreenShakeMultiplier * Time.deltaTime);
					if (droneSustainer != null && !speedInterpolator.finished) {
						SetSustainerVolume(droneSustainer, proportionalRotationSpeed);
					}
				} else {
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
			if (state == MonumentState.Active) {
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
				if (Reroll2Controller.Instance.PaidRerollsSetting && resourceBalanceGizmo != null) {
					yield return resourceBalanceGizmo;
				}
			}
		}

		private void RerollMapAction() {
			if (HasSufficientBalance(Reroll2Controller.MapRerollType.Map)) {
				pendingOperation = PendingOperationType.MapReroll;
				SpinUp();
			}
		}

		private void RerollGeysersAction() {
			if (HasSufficientBalance(Reroll2Controller.MapRerollType.Geyser)) {
				pendingOperation = PendingOperationType.GeyserReroll;
				SpinUp();
			}
		}

		private bool HasSufficientBalance(Reroll2Controller.MapRerollType rerollType) {
			if (Reroll2Controller.Instance.CanAffordOperation(rerollType)) {
				return true;
			} else {
				Messages.Message("Reroll2_cannotAfford".Translate(), MessageSound.RejectInput);
			}
			return false;
		}

		private void OnSpeedInterpolationFinished(ValueInterpolator interpolator, float finalvalue, float interpolationduration, InterpolationCurves.Curve interpolationcurve) {
			switch (pendingOperation) {
				case PendingOperationType.MapReroll:
					EndSustainer();
					Reroll2Controller.Instance.RerollMap();
					break;
				case PendingOperationType.GeyserReroll:
					Reroll2Controller.Instance.RerollGeysers();
					SpinDownFromMax();
					break;
				case PendingOperationType.None:
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}
			pendingOperation = PendingOperationType.None;
			if (finalvalue == MaxSpeed) {
				SpinDownFromMax();
			}
		}

		private void OnBlinkerInterpolationFinished(ValueInterpolator interpolator, float finalvalue, float interpolationduration, InterpolationCurves.Curve interpolationcurve) {
			startupFlicker = null;
		}

		private void SpinUp() {
			speedInterpolator.StartInterpolation(MaxSpeed, SpeedTransitionDuration, InterpolationCurves.CubicEaseInOut);
		}

		private void SpinDownFromMax() {
			speedInterpolator.value = MaxSpeed;
			speedInterpolator.StartInterpolation(MinSpeed, SpeedTransitionDuration, InterpolationCurves.CubicEaseInOut);
		}

		private void SpinDownToZero() {
			if (speedInterpolator.value > 0f) {
				speedInterpolator.StartInterpolation(0f, SpeedTransitionDuration, InterpolationCurves.CubicEaseOut);
			}
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
	}

}