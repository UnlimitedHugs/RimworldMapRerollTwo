﻿using System.Collections.Generic;
using HugsLib.Utils;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Reroll2.UI {
	public class Dialog_MapPreviews : Window {
		private const float ElementSpacing = 10f;

		private static readonly Vector2 PageButtonSize = new Vector2(160f, 40f);
		private static readonly Vector2 GenerateButtonSize = new Vector2(160f, 40f);
		private static readonly Vector2 FavoriteControlSize = new Vector2(160f, 24f);
		private static readonly Color GenerateButtonColor = new Color(.55f, 1f, .55f);

		private readonly GeneratedPreviewPageProvider previewGenerator;
		private readonly ListPreviewPageProvider favoritesProvider;
		private readonly RerollMapState mapState;

		private List<TabRecord> tabs;
		private TabRecord previewsTab;
		private TabRecord favoritesTab;
		private TabRecord activeTab;
		
		public override Vector2 InitialSize {
			get { return new Vector2(600, 800); }
		}

		public override void Notify_ResolutionChanged() {
			base.Notify_ResolutionChanged();
			SetInitialSizeAndPosition();
		}

		protected override void SetInitialSizeAndPosition() {
			base.SetInitialSizeAndPosition();
			var rerollTab = Find.WindowStack.WindowOfType<MainTabWindow_Reroll>();
			if (rerollTab.windowRect.y < windowRect.yMax) {
				windowRect.yMax = rerollTab.windowRect.y - 1;
			}
		}

		public Dialog_MapPreviews() {
			forcePause = true;
			absorbInputAroundWindow = true;
			draggable = false;
			doCloseX = true;
			doCloseButton = false;
			SetUpTabs();
			mapState = RerollToolbox.GetStateForMap();
			favoritesProvider = new ListPreviewPageProvider();
			previewGenerator = new GeneratedPreviewPageProvider(Find.VisibleMap);
			previewGenerator.OpenPage(0);
		}

		public override void PreClose() {
			previewGenerator.Dispose();
			favoritesProvider.Dispose();
		}

		private void SetUpTabs() {
			activeTab = previewsTab = new TabRecord("Reroll2_previews_previewsTab".Translate(), () => OnTabSelected(0), false);
			favoritesTab = new TabRecord(string.Empty, () => OnTabSelected(1), false);
			tabs = new List<TabRecord>{previewsTab, favoritesTab};
		}

		public override void DoWindowContents(Rect inRect) {
			var contentRect = inRect;
			const float tabMargin = 45f;
			contentRect.yMin += tabMargin;
			var bottomSectionHeight = CloseButSize.y + ElementSpacing;
			var bottomSection = new Rect(inRect.x, inRect.height - bottomSectionHeight, inRect.width, bottomSectionHeight);
			contentRect.yMax -= bottomSection.height;
			for (int i = 0; i < tabs.Count; i++) {
				tabs[i].selected = activeTab == tabs[i];
			}
			favoritesTab.label = "Reroll2_previews_favoritesTab".Translate(favoritesProvider.Count);
			Widgets.DrawMenuSection(contentRect);
			TabDrawer.DrawTabs(contentRect, tabs);
			var tabContentRect = contentRect.ContractedBy(ElementSpacing);
			var bottomBar = new Rect(tabContentRect.x, tabContentRect.yMax - PageButtonSize.y, tabContentRect.width, PageButtonSize.y);
			var previewsArea = new Rect(tabContentRect.x, tabContentRect.y, tabContentRect.width, tabContentRect.height - (bottomBar.height + ElementSpacing));
			if (activeTab == previewsTab) {
				DoPreviewsContents(previewsArea, bottomBar);
			} else if (activeTab == favoritesTab) {
				DoFavoritesContents(previewsArea, bottomBar);
			}

			if (Widgets.ButtonText(new Rect(bottomSection.width - CloseButSize.x, bottomSection.yMax - CloseButSize.y, CloseButSize.x, CloseButSize.y), "CloseButton".Translate())) {
				Close();		
			}
		}

		private void DoPreviewsContents(Rect previewsArea, Rect bottomBar) {
			previewGenerator.Draw(previewsArea);
			DoBottomBarControls(previewGenerator, bottomBar);
		}

		private void DoFavoritesContents(Rect previewsArea, Rect bottomBar) {
			favoritesProvider.Draw(previewsArea);
			DoBottomBarControls(favoritesProvider, bottomBar);
		}

		private void DoBottomBarControls(BasePreviewPageProvider pageProvider, Rect inRect) {
			var currentZoomedPreview = pageProvider.CurrentZoomedInPreview;
			if (currentZoomedPreview != null) {
				var generateBtnRect = new Rect(inRect.xMin, inRect.yMin, GenerateButtonSize.x, inRect.height);
				Reroll2Utility.DrawWithGUIColor(GenerateButtonColor, () => {
					if (Widgets.ButtonText(generateBtnRect, "Reroll2_previews_generateMap".Translate())) {
						SoundDefOf.Click.PlayOneShotOnCamera();
						Close();
						Reroll2Controller.Instance.ExecuteInMainThread(() => {
							previewGenerator.WaitForDisposal();
							RerollToolbox.DoMapReroll(currentZoomedPreview.Seed);
						});
					}
				});

				var favoritesControlRect = new Rect(generateBtnRect.xMax + ElementSpacing, inRect.yMin, FavoriteControlSize.x, inRect.height);
				var favoriteCheckPos = new Vector2(favoritesControlRect.xMin + ElementSpacing, favoritesControlRect.center.y - FavoriteControlSize.y / 2f);
				var checkLabelRect = new Rect(favoriteCheckPos.x + FavoriteControlSize.y + ElementSpacing, favoriteCheckPos.y - 7f, FavoriteControlSize.x, inRect.height);

				bool isPreview = favoritesProvider.Contains(currentZoomedPreview);
				bool checkOn = isPreview;
				if (Widgets.ButtonInvisible(favoritesControlRect)) {
					checkOn = !checkOn;
					(checkOn ? SoundDefOf.CheckboxTurnedOn : SoundDefOf.CheckboxTurnedOff).PlayOneShotOnCamera();
					if (checkOn) {
						favoritesProvider.Add(new Widget_MapPreview(currentZoomedPreview));
					} else {
						favoritesProvider.Remove(currentZoomedPreview);
					}
				}
				Widgets.Checkbox(favoriteCheckPos, ref checkOn);
				if (Mouse.IsOver(favoritesControlRect)) {
					Widgets.DrawHighlight(favoritesControlRect);
				}
				Text.Anchor = TextAnchor.MiddleLeft;
				Widgets.Label(checkLabelRect, "Reroll2_previews_favoriteCheck".Translate());
				Text.Anchor = TextAnchor.UpperLeft;

				var zoomOutBtnRect = new Rect(inRect.xMax - PageButtonSize.x, inRect.yMin, PageButtonSize.x, inRect.height);
				if (Widgets.ButtonText(zoomOutBtnRect, "Reroll2_previews_zoomOut".Translate())) {
					currentZoomedPreview.ZoomOut();
				}
			} else {
				var numPagesToTurn = HugsLibUtility.ControlIsHeld ? 5 : 1;
				if (pageProvider.PageIsAvailable(pageProvider.CurrentPage - numPagesToTurn)) {
					if (Widgets.ButtonText(new Rect(inRect.xMin, inRect.yMin, PageButtonSize.x, inRect.height), "Reroll2_previews_prevPage".Translate())) {
						PageBackwards(pageProvider, numPagesToTurn);
					}
				}

				Text.Anchor = TextAnchor.MiddleCenter;
				Widgets.Label(inRect, "Page " + (pageProvider.CurrentPage + 1));
				Text.Anchor = TextAnchor.UpperLeft;

				if (pageProvider.PageIsAvailable(pageProvider.CurrentPage + numPagesToTurn)) {
					var paidNextBtnLabel = Reroll2Utility.WithCostSuffix("Reroll2_previews_nextPage", PaidOperationType.GeneratePreviews, pageProvider.CurrentPage + numPagesToTurn);
					var nextBtnLabel = activeTab == previewsTab ? paidNextBtnLabel : "Reroll2_previews_nextPage".Translate("");
					if (Widgets.ButtonText(new Rect(inRect.xMax - PageButtonSize.x, inRect.yMin, PageButtonSize.x, inRect.height), nextBtnLabel)) {
						PageForward(pageProvider, numPagesToTurn);
					}
				}
				DoMouseWheelPageTurning(pageProvider);
			}
		}

		private void DoMouseWheelPageTurning(BasePreviewPageProvider pageProvider) {
			if(Event.current.type != EventType.ScrollWheel) return;
			var scrollAmount = Event.current.delta.y;
			if (scrollAmount > 0) {
				// scroll within purchased pages unless shift is held
				if (pageProvider.CurrentPage < mapState.NumPreviewPagesPurchased - 1 || HugsLibUtility.ShiftIsHeld || !Reroll2Controller.Instance.PaidRerollsSetting) {
					PageForward(pageProvider);
				}
			} else if(scrollAmount < 0) {
				PageBackwards(pageProvider);
			}
		}

		public void PageForward(BasePreviewPageProvider pageProvider, int numPages = 1) {
			var pageToOpen = pageProvider.CurrentPage + numPages;
			if (activeTab == previewsTab && RerollToolbox.GetOperationCost(PaidOperationType.GeneratePreviews, pageToOpen) > 0) {
				RerollToolbox.ChargeForOperation(PaidOperationType.GeneratePreviews, pageToOpen);
			}
			pageProvider.OpenPage(pageToOpen);
		}

		public void PageBackwards(BasePreviewPageProvider pageProvider, int numPages = 1) {
			numPages = Mathf.Min(pageProvider.CurrentPage, numPages);
			pageProvider.OpenPage(pageProvider.CurrentPage - numPages);
		}

		private void OnTabSelected(int tabIndex) {
			activeTab = tabs[tabIndex];
		}
	}
}