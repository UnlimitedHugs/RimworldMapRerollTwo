using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Reroll2.UI {
	public class Dialog_MapPreviews : Window {
		private const float ElementSpacing = 10f;

		private List<TabRecord> tabs;
		private TabRecord previewsTab;
		private TabRecord favoritesTab;
		private TabRecord activeTab;
		private PreviewPageProvider pageProvider;

		public override Vector2 InitialSize {
			get { return new Vector2(600, 700); }
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

			Widgets.ButtonText(new Rect(bottomSection.width - CloseButSize.x, bottomSection.yMax - CloseButSize.y, CloseButSize.x, CloseButSize.y), "CloseButton".Translate());
		}

		private void DoPreviewsContents(Rect inRect) {
			var bottomBar = new Rect(inRect.x, inRect.yMax - CloseButSize.y, inRect.width, CloseButSize.y);
			var previewsArea = new Rect(inRect.x, inRect.y, inRect.width, inRect.height - (bottomBar.height + ElementSpacing));

			pageProvider.DrawPage(previewsArea);

			Widgets.ButtonText(new Rect(bottomBar.xMin, bottomBar.yMin, CloseButSize.x, bottomBar.height), "< Prev page");
			Text.Anchor = TextAnchor.MiddleCenter;
			Widgets.Label(bottomBar, "Page "+(pageProvider.CurrentPage + 1));
			Text.Anchor = TextAnchor.UpperLeft;
			Widgets.ButtonText(new Rect(bottomBar.xMax - CloseButSize.x, bottomBar.yMin, CloseButSize.x, bottomBar.height), "Next page >");
		}

		private void OnTabSelected(int tabIndex) {
			activeTab = tabs[tabIndex];
		}
	}
}