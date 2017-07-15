using System.Collections.Generic;
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

		private readonly PreviewPageProvider pageProvider;
		private readonly RerollMapState mapState;

		private List<TabRecord> tabs;
		private TabRecord previewsTab;
		private TabRecord favoritesTab;
		private TabRecord activeTab;
		private bool favorite;

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
			pageProvider = new PreviewPageProvider(Find.VisibleMap);
		}

		public override void PostClose() {
			pageProvider.Dispose();
		}

		private void SetUpTabs() {
			activeTab = previewsTab = new TabRecord("Reroll2_previews_previewsTab".Translate(), () => OnTabSelected(0), false);
			favoritesTab = new TabRecord("Reroll2_previews_favoritesTab".Translate(), () => OnTabSelected(1), false);
			tabs = new List<TabRecord>{previewsTab, favoritesTab};
		}

		public override void DoWindowContents(Rect inRect) {
			var contentRect = inRect;
			contentRect.yMin += 45f;
			var bottomSectionHeight = CloseButSize.y + ElementSpacing;
			var bottomSection = new Rect(inRect.x, inRect.height - bottomSectionHeight, inRect.width, bottomSectionHeight);
			contentRect.yMax -= bottomSection.height;
			for (int i = 0; i < tabs.Count; i++) {
				tabs[i].selected = activeTab == tabs[i];
			}
			Widgets.DrawMenuSection(contentRect);
			TabDrawer.DrawTabs(contentRect, tabs);

			DoPreviewsContents(contentRect.ContractedBy(ElementSpacing));

			if (Widgets.ButtonText(new Rect(bottomSection.width - CloseButSize.x, bottomSection.yMax - CloseButSize.y, CloseButSize.x, CloseButSize.y), "CloseButton".Translate())) {
				Close();		
			}
		}

		private void DoPreviewsContents(Rect inRect) {
			var bottomBar = new Rect(inRect.x, inRect.yMax - PageButtonSize.y, inRect.width, PageButtonSize.y);
			var previewsArea = new Rect(inRect.x, inRect.y, inRect.width, inRect.height - (bottomBar.height + ElementSpacing));

			pageProvider.Draw(previewsArea);
			var currentZoomedPreview = pageProvider.CurrentZoomedInPreview;
			if (currentZoomedPreview != null) {
				var generateBtnRect = new Rect(bottomBar.xMin, bottomBar.yMin, GenerateButtonSize.x, bottomBar.height);
				Reroll2Utility.DrawWithGUIColor(GenerateButtonColor, () => {
					if (Widgets.ButtonText(generateBtnRect, "Reroll2_previews_generateMap".Translate())) {
						SoundDefOf.Click.PlayOneShotOnCamera();
						Reroll2Controller.Instance.ExecuteInMainThread(() => {
							Find.WindowStack.WindowOfType<Dialog_MapPreviews>().Close(false);
							RerollToolbox.DoMapReroll(currentZoomedPreview.Seed);
						});
					}
				});

				var favoriteCheckPos = new Vector2(generateBtnRect.xMax + ElementSpacing * 2f, bottomBar.center.y - FavoriteControlSize.y / 2f);
				var checkLabelRect = new Rect(favoriteCheckPos.x + FavoriteControlSize.y + ElementSpacing, favoriteCheckPos.y - 7f, FavoriteControlSize.x, bottomBar.height);
				if (Widgets.ButtonInvisible(checkLabelRect)) {
					favorite = !favorite;
					(favorite ? SoundDefOf.CheckboxTurnedOn : SoundDefOf.CheckboxTurnedOff).PlayOneShotOnCamera();
				}
				Widgets.Checkbox(favoriteCheckPos, ref favorite);
				Text.Anchor = TextAnchor.MiddleLeft;
				Widgets.Label(checkLabelRect, "Reroll2_previews_favoriteCheck".Translate());
				Text.Anchor = TextAnchor.UpperLeft;

				var zoomOutBtnRect = new Rect(bottomBar.xMax - PageButtonSize.x, bottomBar.yMin, PageButtonSize.x, bottomBar.height);
				if (Widgets.ButtonText(zoomOutBtnRect, "Reroll2_previews_zoomOut".Translate())) {
					currentZoomedPreview.ZoomOut();
				}
			} else {
				var numPagesToTurn = HugsLibUtility.ControlIsHeld ? 5 : 1;
				if (Widgets.ButtonText(new Rect(bottomBar.xMin, bottomBar.yMin, PageButtonSize.x, bottomBar.height), "Reroll2_previews_prevPage".Translate())) {
					PageBackwards(numPagesToTurn);
				}
				Text.Anchor = TextAnchor.MiddleCenter;
				Widgets.Label(bottomBar, "Page " + (pageProvider.CurrentPage + 1));
				Text.Anchor = TextAnchor.UpperLeft;
				var nextBtnLabel = Reroll2Utility.WithCostSuffix("Reroll2_previews_nextPage", PaidOperationType.GeneratePreviews, pageProvider.CurrentPage + numPagesToTurn);
				if (Widgets.ButtonText(new Rect(bottomBar.xMax - PageButtonSize.x, bottomBar.yMin, PageButtonSize.x, bottomBar.height), nextBtnLabel)) {
					PageForward(numPagesToTurn);
				}
				DoMouseWheelPageTurning();
			}
		}

		private void DoMouseWheelPageTurning() {
			if(Event.current.type != EventType.ScrollWheel) return;
			var scrollAmount = Event.current.delta.y;
			if (scrollAmount > 0) {
				// scroll within purchased pages unless shift is held
				if (pageProvider.CurrentPage < mapState.NumPreviewPagesPurchased - 1 || HugsLibUtility.ShiftIsHeld) {
					PageForward();
				}
			} else if(scrollAmount < 0) {
				PageBackwards();
			}
		}

		public void PageForward(int numPages = 1) {
			var pageToOpen = pageProvider.CurrentPage + numPages;
			if (RerollToolbox.GetOperationCost(PaidOperationType.GeneratePreviews, pageToOpen) > 0) {
				RerollToolbox.ChargeForOperation(PaidOperationType.GeneratePreviews, pageToOpen);
			}
			pageProvider.OpenPage(pageToOpen);
		}

		public void PageBackwards(int numPages = 1) {
			numPages = Mathf.Min(pageProvider.CurrentPage, numPages);
			pageProvider.OpenPage(pageProvider.CurrentPage - numPages);
		}

		private void OnTabSelected(int tabIndex) {
			activeTab = tabs[tabIndex];
		}
	}
}