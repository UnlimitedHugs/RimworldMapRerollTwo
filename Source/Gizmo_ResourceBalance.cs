using UnityEngine;
using Verse;

namespace Reroll2 {
	public class Gizmo_ResourceBalance : Gizmo {
		private const float InterpolationDuration = 2f;

		private readonly Map map;
		private ValueInterpolator interpolator;
		private float lastSeenBalance;

		public Gizmo_ResourceBalance(Map map, float customStartValue = -1) {
			this.map = map;
			var startValue = customStartValue >= 0 ? customStartValue : GetResourceBalance();
			lastSeenBalance = startValue;
			interpolator = new ValueInterpolator(startValue);
		}

		public override float Width {
			get {
				return 192f;
			}
		}

		public override GizmoResult GizmoOnGUI(Vector2 topLeft) {
			UpdateInterpolator();
			var overRect = new Rect(topLeft.x, topLeft.y, Width, 75f);
			Find.WindowStack.ImmediateWindow(9462875, overRect, WindowLayer.GameUI, delegate {
				var rect = overRect.AtZero().ContractedBy(6f);
				var rect2 = rect;
				rect2.height = overRect.height / 2f;
				Text.Font = GameFont.Tiny;
				Widgets.Label(rect2, "Reroll2_remainingResource".Translate());
				var rect3 = rect;
				rect3.yMin = overRect.height / 2f;
				float fillPercent = Mathf.Clamp(interpolator.value, 0, Reroll2Controller.MaxResourceBalance);
				Widgets.FillableBar(rect3, fillPercent/Reroll2Controller.MaxResourceBalance, Resources.Textures.ResourceBarFull, Resources.Textures.ResourceBarEmpty, false);
				Text.Font = GameFont.Small;
				Text.Anchor = TextAnchor.MiddleCenter;
				Widgets.Label(rect3, string.Format("{0:F1}%", interpolator.value));
				Text.Anchor = TextAnchor.UpperLeft;
			});
			return new GizmoResult(GizmoState.Clear);
		}

		private float GetResourceBalance() {
			return RerollToolbox.GetStateForMap(map).ResourceBalance;
		}

		private void UpdateInterpolator() {
			if (Event.current.type != EventType.Repaint) return;
			var balance = GetResourceBalance();
			interpolator.UpdateIfUnpaused();
			if (balance != lastSeenBalance) {
				lastSeenBalance = balance;
				interpolator.StartInterpolation(balance, InterpolationDuration, InterpolationCurves.CubicEaseInOut);
			}
		}
	}
}
