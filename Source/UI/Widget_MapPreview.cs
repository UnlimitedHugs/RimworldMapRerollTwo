﻿using System;
using Reroll2.Promises;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Reroll2.UI {
	public class Widget_MapPreview : IDisposable {
		private const float SpawnInterpolationDuration = .3f;
		private const float ZoomInterpolationDuration = .5f;
		private const float ControlSpacing = 10;

		private static readonly Color OutlineColor = GenColor.FromHex("616C7A");
		private static readonly Vector2 GenerateButtonSize = new Vector2(150f, 40f);
		private static readonly Vector2 FavoriteControlSize = new Vector2(150f, 24f);

		private readonly string seed;
		private readonly ValueInterpolator spawnInterpolator;
		private readonly ValueInterpolator zoomInterpolator;
		
		private Texture2D previewTex;
		private Rect zoomedOutRect;
		private bool zoomedIn;
		private bool favorite;

		public string Seed {
			get { return seed; }
		}

		public bool WantsOverlayDrawing {
			get { return !zoomInterpolator.finished || zoomedIn; }
		}

		public Widget_MapPreview(IPromise<Texture2D> promise, string seed) {
			promise.Done(OnPromiseResolved);
			this.seed = seed;
			spawnInterpolator = new ValueInterpolator(1);
			zoomInterpolator = new ValueInterpolator(0);
		}

		public void Dispose() {
			UnityEngine.Object.Destroy(previewTex);
			previewTex = null;
		}

		public void Draw(Rect inRect, bool interactive) {
			if (Event.current.type == EventType.Repaint) {
				spawnInterpolator.Update();
				zoomInterpolator.Update();
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
			if (zoomedIn) {
				var contentRect = inRect.ContractedBy(ControlSpacing);

				var btnRect = new Rect(contentRect.xMin, contentRect.yMax - GenerateButtonSize.y, GenerateButtonSize.x, GenerateButtonSize.y);
				if (Widgets.ButtonInvisible(btnRect)) {
					SoundDefOf.Click.PlayOneShotOnCamera();
					Find.WindowStack.WindowOfType<Dialog_MapPreviews>().Close(false);
					RerollToolbox.DoMapReroll(seed);
				}
				if (Mouse.IsOver(btnRect)) {
					Widgets.DrawHighlight(btnRect);
				}
				Widgets.DrawBox(btnRect);
				Text.Anchor = TextAnchor.MiddleCenter;
				Widgets.Label(btnRect, "Reroll2_previews_generateMap".Translate());

				var favoriteCheckPos = new Vector2(contentRect.xMin, btnRect.yMin - ControlSpacing - FavoriteControlSize.y);
				var checkLabelRect = new Rect(favoriteCheckPos.x + FavoriteControlSize.y + ControlSpacing, favoriteCheckPos.y - 7f, FavoriteControlSize.x, GenerateButtonSize.y);
				if (Widgets.ButtonInvisible(checkLabelRect)) {
					favorite = !favorite;
					(favorite?SoundDefOf.CheckboxTurnedOn:SoundDefOf.CheckboxTurnedOff).PlayOneShotOnCamera();
				}
				Widgets.Checkbox(favoriteCheckPos, ref favorite);
				Text.Anchor = TextAnchor.MiddleLeft;
				Widgets.Label(checkLabelRect, "Reroll2_previews_favoriteCheck".Translate());
				Text.Anchor = TextAnchor.UpperLeft;
			}
			if (Widgets.ButtonInvisible(inRect)) {
				ZoomOut();
			}
		}

		private void ZoomIn(Rect inRect) {
			if (zoomedIn || previewTex == null) return;
			zoomedOutRect = inRect;
			zoomInterpolator.StartInterpolation(1, ZoomInterpolationDuration, InterpolationCurves.CubicEaseInOut).SetFinishedCallback(OnZoomInterpolatorFinished);
		}

		private void ZoomOut() {
			if (!zoomedIn) return;
			zoomedIn = false;
			zoomInterpolator.StartInterpolation(0, ZoomInterpolationDuration, InterpolationCurves.CubicEaseInOut);
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
			spawnInterpolator.StartInterpolation(1f, SpawnInterpolationDuration, InterpolationCurves.CubicEaseOut);
		}

		private void DrawOutline(Rect rect) {
			var prevColor = GUI.color;
			GUI.color = OutlineColor;
			Widgets.DrawBox(rect);
			GUI.color = prevColor;
		}
	}
}