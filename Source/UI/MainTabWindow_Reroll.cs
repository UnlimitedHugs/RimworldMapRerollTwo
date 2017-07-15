using System;
using RimWorld;
using RimWorld.Planet;
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
			balanceWidget = new Widget_ResourceBalance(resourceBalance);
		}

		public override void DoWindowContents(Rect inRect) {
			GUILayout.BeginArea(inRect);
			Text.Font = GameFont.Small;
			if (Find.World.renderer.wantedMode != WorldRenderMode.None) {
				GUILayout.Label("Reroll2_visibleMapRequired".Translate());
				return;
			}
			if (Find.VisibleMap == null || !Find.VisibleMap.IsPlayerHome) {
				GUILayout.Label("Reroll2_settledMapRequired".Translate());
				return;
			}
			GUILayout.BeginHorizontal();
			DoRerollTabButton(Resources.Textures.UIRerollMap, Reroll2Utility.WithCostSuffix("Reroll2_rerollMap", PaidOperationType.GeneratePreviews), null, () => {
				if (RerollToolbox.GetOperationCost(PaidOperationType.GeneratePreviews) > 0) {
					RerollToolbox.ChargeForOperation(PaidOperationType.GeneratePreviews);
				}
				Find.WindowStack.Add(new Dialog_MapPreviews());
			});
			GUILayout.Space(ControlSpacing);
			DoRerollTabButton(Resources.Textures.UIRerollGeysers, Reroll2Utility.WithCostSuffix("Reroll2_rerollGeysers", PaidOperationType.RerollGeysers), null, () => {
				if (!Reroll2Controller.Instance.GeyserRerollInProgress) {
					Reroll2Controller.Instance.RerollGeysers();
				} else {
					Messages.Message("Reroll2_rerollInProgress".Translate(), MessageSound.RejectInput);
				}
			});
			GUILayout.Space(ControlSpacing);
			balanceWidget.DrawLayout();
			GUILayout.EndHorizontal();
			GUILayout.EndArea();
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