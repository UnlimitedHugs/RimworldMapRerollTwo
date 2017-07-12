using System;
using System.Collections.Generic;
using HugsLib.Utils;
using UnityEngine;
using Verse;

namespace Reroll2.UI {
	public class PreviewPageProvider : IDisposable {
		private const float PreviewSpacing = 10;

		private readonly Map startingMap;
		private const int PreviewsPerPage = 9;
		private MapPreviewGenerator previewGenerator;
		private List<Widget_MapPreview> previews = new List<Widget_MapPreview>();
		private int currentPage;
		private string lastGeneratedSeed;

		public int CurrentPage {
			get { return currentPage; }
		}

		public PreviewPageProvider(Map currentMap) {
			startingMap = currentMap;
			var mapState = RerollToolbox.GetStateForMap(currentMap);
			lastGeneratedSeed = RerollToolbox.CurrentMapSeed(mapState);
			previewGenerator = new MapPreviewGenerator();
			OpenPage(0);
		}

		public void OpenPage(int pageIndex) {
			EnsureEnoughPreviewsForPage(pageIndex);
			currentPage = pageIndex;
		}

		public void Dispose() {
			previewGenerator.Dispose();
			foreach (var preview in previews) {
				preview.Dispose();
			}
		}

		public void DrawPage(Rect inRect) {
			float rowCount = Mathf.Sqrt(PreviewsPerPage);
			var totalSpacing = PreviewSpacing * (rowCount - 1);
			var previewSize = new Vector2((inRect.width - totalSpacing) / rowCount, (inRect.height - totalSpacing) / rowCount);
			var halfPreviewSize = previewSize / 2f;
			for (int i = MinIndexOnPage(currentPage); i <= MaxIndexOnPage(currentPage); i++) {
				var preview = previews[i];
				var previewRect = new Rect(
					inRect.x + (inRect.width - previewSize.x) * preview.RelativePosition.x,
					inRect.y + (inRect.height - previewSize.y) * preview.RelativePosition.y,
					previewSize.x, previewSize.y);
				preview.Draw(previewRect);
			}
		}

		private int MinIndexOnPage(int page) {
			return page * PreviewsPerPage;
		}

		private int MaxIndexOnPage(int page) {
			return page * PreviewsPerPage + PreviewsPerPage - 1;
		}

		private void EnsureEnoughPreviewsForPage(int page) {
			while (previews.Count <= MaxIndexOnPage(page)) {
				previews.Add(CreatePreview(previews.Count));
			}
		}

		private Widget_MapPreview CreatePreview(int index) {
			lastGeneratedSeed = RerollToolbox.GetNextRerollSeed(lastGeneratedSeed);
			var promise = previewGenerator.QueuePreviewForSeed(lastGeneratedSeed, startingMap.Tile, startingMap.Size.x);
			var relativePosition = GetPreviewPositionFromIndex(index);
			return new Widget_MapPreview(promise, lastGeneratedSeed, relativePosition);
		}

		private Vector2 GetPreviewPositionFromIndex(int previewIndex) {
			float rowCount = Mathf.Sqrt(PreviewsPerPage);
			var indexInRow = previewIndex % rowCount;
			var indexInCol = Mathf.Floor(previewIndex / rowCount);
			return new Vector2(indexInRow / (rowCount - 1f), indexInCol / (rowCount - 1f));
		}

		/*private Vector2 ScaleDirectionFromRelativePosition() {
		}*/
	}
}