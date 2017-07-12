using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace Reroll2.UI {
	public class MainTabWindow_Reroll : MainTabWindow {
		private const float ControlPadding = 6f;
		private const float ControlSpacing = 6f;
		private readonly Color buttonOutlineColorNormal = GenColor.FromHex("1D4B6E");
		private readonly Color buttonOutlineColorHover = GenColor.FromHex("616C7A");

		private Widget_ResourceBalance balanceWidget;

		public override Vector2 RequestedTabSize {
			get {
				return new Vector2(600f, 100f);
			}
		}

		public override void Notify_ResolutionChanged() {
			base.Notify_ResolutionChanged();
			SetInitialSizeAndPosition();
		}

		protected override void SetInitialSizeAndPosition() {
			base.SetInitialSizeAndPosition();
			windowRect.x = Verse.UI.screenWidth / 2f - windowRect.width / 2f;
		}

		public override void PreOpen() {
			base.PreOpen();
			var map = Find.VisibleMap;
			var resourceBalance = map == null ? 0f : RerollToolbox.GetStateForMap(map).ResourceBalance;
			Log.Message(resourceBalance+" "+map);
			balanceWidget = new Widget_ResourceBalance(resourceBalance);
		}

		public override void DoWindowContents(Rect inRect) {
			GUILayout.BeginArea(inRect);
			GUILayout.BeginHorizontal();
			DoRerollTabButton(Resources.Textures.UIRerollMap, "Reroll map", null, () => {
				if (CanAffordOperation(Reroll2Controller.MapRerollType.Geyser)) {
					Reroll2Controller.Instance.RerollMap();
				}
			});
			GUILayout.Space(ControlSpacing);
			DoRerollTabButton(Resources.Textures.UIRerollGeysers, "Reroll geysers", null, () => {
				if (CanAffordOperation(Reroll2Controller.MapRerollType.Geyser)) {
					if (!Reroll2Controller.Instance.GeyserRerollInProgress) {
						Reroll2Controller.Instance.RerollGeysers();
					} else {
						Messages.Message("Reroll2_rerollInProgress".Translate(), MessageSound.RejectInput);
					}
				}
			});
			GUILayout.Space(ControlSpacing);
			balanceWidget.DrawLayout();
			GUILayout.EndHorizontal();
			GUILayout.EndArea();
		}

		private bool CanAffordOperation(Reroll2Controller.MapRerollType rerollType) {
			if (Reroll2Controller.Instance.CanAffordOperation(rerollType)) {
				return true;
			} else {
				Messages.Message("Reroll2_cannotAfford".Translate(), MessageSound.RejectInput);
			}
			return false;
		}

		private void DoRerollTabButton(Texture2D icon, string label, string tooltip, Action callback) {
			const float width = 150;
			var prevColor = GUI.color;
			if (GUILayout.Button(string.Empty, Widgets.EmptyStyle, GUILayout.Width(width), GUILayout.ExpandHeight(true))) {
				callback();
			}
			var controlRect = GUILayoutUtility.GetLastRect();
			var contentsRect = controlRect.ContractedBy(ControlPadding);
			
			var hovering = Mouse.IsOver(controlRect);
			GUI.color = hovering ? buttonOutlineColorHover : buttonOutlineColorNormal;
			Widgets.DrawBox(controlRect);
			GUI.color = prevColor;
			if (icon == null) {
				icon = BaseContent.BadTex;
			}
			
			var iconScale = .75f;
			var iconSize = new Vector2(64f, 64f)*iconScale;
			var iconRect = new Rect(contentsRect.x, contentsRect.y+contentsRect.height/2f-iconSize.y/2f, iconSize.x, iconSize.y);
			Widgets.DrawTextureFitted(iconRect, icon, 1f);

			var labelRect = new Rect(iconRect.xMax + ControlPadding, contentsRect.y, contentsRect.width - (iconRect.width + ControlPadding * 3), contentsRect.height);
			var prevAnchor = Text.Anchor;
			Text.Anchor = TextAnchor.MiddleCenter;
			Widgets.Label(labelRect, label);
			Text.Anchor = prevAnchor;
			if (hovering && !tooltip.NullOrEmpty()) {
				TooltipHandler.TipRegion(controlRect, tooltip);
			}
		}
	}
}