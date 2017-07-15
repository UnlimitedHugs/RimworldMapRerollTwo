﻿using System;
using Reroll2.Promises;
using UnityEngine;
using Verse;

namespace Reroll2.UI {
	public class Widget_MapPreview : IDisposable, IEquatable<Widget_MapPreview> {
		private const float SpawnInterpolationDuration = .3f;
		private const float ZoomInterpolationDuration = .5f;

		private static readonly Color OutlineColor = GenColor.FromHex("616C7A");

		private readonly string seed;
		private readonly IPromise<Texture2D> promise;
		
		private ValueInterpolator spawnInterpolator;
		private ValueInterpolator zoomInterpolator;
		private Texture2D previewTex;
		private Rect zoomedOutRect;
		private bool zoomedIn;

		public string Seed {
			get { return seed; }
		}

		public bool WantsOverlayDrawing {
			get { return !zoomInterpolator.finished || zoomedIn; }
		}

		public bool IsFullyZoomedIn {
			get { return zoomedIn; }
		}

		public Widget_MapPreview(IPromise<Texture2D> promise, string seed) {
			PrepareComponents();
			this.promise = promise;
			promise.Done(OnPromiseResolved);
			this.seed = seed;
		}

		public Widget_MapPreview(Widget_MapPreview copyFrom) {
			PrepareComponents();
			promise = copyFrom.promise;
			promise.Done(OnPromiseResolved);
			seed = copyFrom.seed;
		}

		private void PrepareComponents() {
			spawnInterpolator = new ValueInterpolator();
			zoomInterpolator = new ValueInterpolator();
		}

		public void Dispose() {
			UnityEngine.Object.Destroy(previewTex);
			previewTex = null;
		}

		public void Draw(Rect inRect, int index, bool interactive) {
			if (Event.current.type == EventType.Repaint) {
				spawnInterpolator.Update();
				zoomInterpolator.Update();
				if (spawnInterpolator.value < 1) {
					Widget_RerollPreloader.Draw(inRect.center, index);
				}
			}
			DrawOutline(inRect);
			if (previewTex != null) {
				var texScale = spawnInterpolator.value;
				var texRect = inRect.ScaledBy(texScale).ContractedBy(1f);
				GUI.DrawTexture(texRect, previewTex);
				if (interactive) {
					if (Mouse.IsOver(inRect)) {
						Widgets.DrawHighlight(texRect);
						if (Widgets.ButtonInvisible(inRect)) {
							ZoomIn(inRect);
						}
					}
				}
			}
		}

		public void DrawOverlay(Rect inRect) {
			var fullRect = LerpRect(zoomedOutRect, inRect, zoomInterpolator.value);
			DrawOutline(fullRect);
			GUI.DrawTexture(fullRect.ContractedBy(1f), previewTex);
			if (Widgets.ButtonInvisible(inRect)) {
				ZoomOut();
			}
		}

		public void ZoomOut() {
			if (!zoomedIn) return;
			zoomedIn = false;
			zoomInterpolator.StartInterpolation(0, ZoomInterpolationDuration, InterpolationCurves.Cubic.InOut);
		}

		private void ZoomIn(Rect inRect) {
			if (zoomedIn || previewTex == null) return;
			zoomedOutRect = inRect;
			zoomInterpolator.StartInterpolation(1, ZoomInterpolationDuration, InterpolationCurves.Cubic.InOut).SetFinishedCallback(OnZoomInterpolatorFinished);
		}

		private Rect LerpRect(Rect a, Rect b, float t) {
			return new Rect(Mathf.Lerp(a.x, b.x, t), Mathf.Lerp(a.y, b.y, t), Mathf.Lerp(a.width, b.width, t), Mathf.Lerp(a.height, b.height, t));
		}

		private void OnZoomInterpolatorFinished(ValueInterpolator interpolator, float finalValue, float interpolationDuration, InterpolationCurves.Curve interpolationCurve) {
			zoomedIn = finalValue == 1;
		}

		private void OnPromiseResolved(Texture2D tex) {
			previewTex = tex;
			spawnInterpolator.value = 0f;
			spawnInterpolator.StartInterpolation(1f, SpawnInterpolationDuration, InterpolationCurves.Cubic.Out);
		}

		private void DrawOutline(Rect rect) {
			Reroll2Utility.DrawWithGUIColor(OutlineColor, () => Widgets.DrawBox(rect));
		}

		public bool Equals(Widget_MapPreview other) {
			if (ReferenceEquals(null, other)) return false;
			if (ReferenceEquals(this, other)) return true;
			return string.Equals(seed, other.seed);
		}

		public override bool Equals(object obj) {
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != GetType()) return false;
			return Equals((Widget_MapPreview)obj);
		}

		public override int GetHashCode() {
			return (seed != null ? seed.GetHashCode() : 0);
		}
	}
}