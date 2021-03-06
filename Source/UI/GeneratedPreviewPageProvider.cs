﻿using Verse;

namespace Reroll2.UI {
	public class GeneratedPreviewPageProvider : BasePreviewPageProvider {
		private readonly Map startingMap;
		private MapPreviewGenerator previewGenerator;
		private string lastGeneratedSeed;
		private int numQueuedPreviews;

		public GeneratedPreviewPageProvider(Map currentMap) {
			startingMap = currentMap;
			var mapState = RerollToolbox.GetStateForMap(currentMap);
			lastGeneratedSeed = RerollToolbox.CurrentMapSeed(mapState);
			previewGenerator = new MapPreviewGenerator();
		}

		public int NumQueuedPreviews {
			get { return numQueuedPreviews; }
		}

		public override void OpenPage(int pageIndex) {
			EnsureEnoughPreviewsForPage(pageIndex);
			base.OpenPage(pageIndex);
		}

		public override void Dispose() {
			base.Dispose();
			previewGenerator.Dispose();
		}

		public void WaitForDisposal() {
			previewGenerator.WaitForDisposal();
		}

		public override bool PageIsAvailable(int pageIndex) {
			return pageIndex >= 0;
		}

		private void EnsureEnoughPreviewsForPage(int page) {
			while (previews.Count <= MaxIndexOnPage(page)) {
				previews.Add(CreatePreview());
			}
		}

		private Widget_MapPreview CreatePreview() {
			lastGeneratedSeed = RerollToolbox.GetNextRerollSeed(lastGeneratedSeed);
			var promise = previewGenerator.QueuePreviewForSeed(lastGeneratedSeed, startingMap.Tile, startingMap.Size.x);
			numQueuedPreviews++;
			promise.Finally(() => numQueuedPreviews--);
			return new Widget_MapPreview(promise, lastGeneratedSeed);
		}
	}
}