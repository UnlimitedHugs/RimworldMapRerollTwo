using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Reroll2.UI {
	public class PreviewPageProvider : IDisposable {
		private const float PreviewSpacing = 10;
		private const float PageFlipDuration = .5f;

		private readonly Map startingMap;
		private const int PreviewsPerPage = 9;
		private MapPreviewGenerator previewGenerator;
		private List<Widget_MapPreview> previews = new List<Widget_MapPreview>();
		private int currentPage;
		private string lastGeneratedSeed;
		private Widget_MapPreview overlayPreview;
		private ValueInterpolator pageInterpolator;
		private int outgoingPage = -1;

		public int CurrentPage {
			get { return currentPage; }
		}

		public int NumPagesAvailable {
			get { return previews.Count / PreviewsPerPage; }
		}

		public Widget_MapPreview CurrentZoomedInPreview {
			get { return overlayPreview != null && overlayPreview.IsFullyZoomedIn ? overlayPreview : null; }
		}

		public PreviewPageProvider(Map currentMap) {
			startingMap = currentMap;
			var mapState = RerollToolbox.GetStateForMap(currentMap);
			lastGeneratedSeed = RerollToolbox.CurrentMapSeed(mapState);
			previewGenerator = new MapPreviewGenerator();
			pageInterpolator = new ValueInterpolator(1f);
			OpenPage(0);
		}

		public void OpenPage(int pageIndex) {
			overlayPreview = null;
			EnsureEnoughPreviewsForPage(pageIndex);
			if (pageIndex != currentPage) {
				outgoingPage = currentPage;
				currentPage = pageIndex;
				pageInterpolator.value = 0f;
				pageInterpolator.StartInterpolation(1f, PageFlipDuration, InterpolationCurves.CubicEaseInOut).SetFinishedCallback(OnPageFlipFinished);
			}
		}

		private void OnPageFlipFinished(ValueInterpolator interpolator, float finalValue, float interpolationDuration, InterpolationCurves.Curve interpolationCurve) {
			interpolator.value = finalValue;
			outgoingPage = -1;
		}

		public void Dispose() {
			previewGenerator.Dispose();
			foreach (var preview in previews) {
				preview.Dispose();
			}
		}

		private bool PageTransitionInProgress {
			get { return outgoingPage >= 0; }
		}

		public void Draw(Rect inRect) {
			if (Event.current.type == EventType.Repaint) {
				pageInterpolator.Update();
			}
			var offscreenPageOffset = inRect.width + PreviewSpacing * 2f;
			var interpolatedOffset = pageInterpolator.value * offscreenPageOffset;
			var backFlip = outgoingPage > currentPage;
			var outgoingOffset = backFlip ? interpolatedOffset : -interpolatedOffset;
			var currentOffset = backFlip ? interpolatedOffset - offscreenPageOffset : offscreenPageOffset - interpolatedOffset;
			if (PageTransitionInProgress) {
				var outgoingRect = new Rect(inRect.x + outgoingOffset, inRect.y, inRect.width, inRect.height);
				DrawPage(outgoingPage, outgoingRect);
			}
			var currentPageRect = new Rect(inRect.x + currentOffset, inRect.y, inRect.width, inRect.height);
			DrawPage(currentPage, currentPageRect);
		}

		private void DrawPage(int page, Rect inRect) {
			float rowCount = Mathf.Sqrt(PreviewsPerPage);
			var totalSpacing = PreviewSpacing * (rowCount - 1);
			var previewSize = new Vector2((inRect.width - totalSpacing) / rowCount, (inRect.height - totalSpacing) / rowCount);
			bool anyOverlayDrawingRequired = false;
			for (int i = MinIndexOnPage(page); i <= MaxIndexOnPage(page); i++) {
				var preview = previews[i];
				var previewPosition = GetPreviewPositionFromIndex(i);
				var previewRect = new Rect(
					inRect.x + (inRect.width - previewSize.x) * previewPosition.x,
					inRect.y + (inRect.height - previewSize.y) * previewPosition.y,
					previewSize.x, previewSize.y);
				var isInteractive = overlayPreview == null && !PageTransitionInProgress;
				preview.Draw(previewRect, isInteractive);
				if (preview.WantsOverlayDrawing) {
					overlayPreview = preview;
					anyOverlayDrawingRequired = true;
				}
			}
			if (!anyOverlayDrawingRequired) {
				overlayPreview = null;
			}
			if (overlayPreview != null) {
				overlayPreview.DrawOverlay(inRect);
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
				previews.Add(CreatePreview());
			}
		}

		private Widget_MapPreview CreatePreview() {
			lastGeneratedSeed = RerollToolbox.GetNextRerollSeed(lastGeneratedSeed);
			var promise = previewGenerator.QueuePreviewForSeed(lastGeneratedSeed, startingMap.Tile, startingMap.Size.x);
			return new Widget_MapPreview(promise, lastGeneratedSeed);
		}

		private Vector2 GetPreviewPositionFromIndex(int previewIndex) {
			var indexOnPage = previewIndex % PreviewsPerPage;
			float rowCount = Mathf.Sqrt(PreviewsPerPage);
			var indexInRow = indexOnPage % rowCount;
			var indexInCol = Mathf.Floor(indexOnPage / rowCount);
			return new Vector2(indexInRow / (rowCount - 1f), indexInCol / (rowCount - 1f));
		}
	}
}