using System;
using Reroll2.Promises;
using UnityEngine;
using Verse;

namespace Reroll2.UI {
	public class Widget_MapPreview : IDisposable {
		private static readonly Color OutlineColor = GenColor.FromHex("616C7A");

		private readonly IPromise<Texture2D> promise;
		private readonly string usedSeed;
		private readonly Vector2 relativePosition;
		
		private Texture2D previewTex;
		
		public Vector2 RelativePosition {
			get { return relativePosition; }
		}

		public Widget_MapPreview(IPromise<Texture2D> promise, string usedSeed, Vector2 relativePosition) {
			this.promise = promise;
			promise.Done(OnPromiseResolved);
			this.usedSeed = usedSeed;
			this.relativePosition = relativePosition;
		}

		public void Dispose() {
			UnityEngine.Object.Destroy(previewTex);
			previewTex = null;
		}

		public void Draw(Rect inRect) {
			if (previewTex != null) {
				var prevColor = GUI.color;
				GUI.color = OutlineColor;
				Widgets.DrawBox(inRect);
				GUI.color = prevColor;
				GUI.DrawTexture(inRect.ContractedBy(1f), previewTex);
			}
		}

		private void OnPromiseResolved(Texture2D tex) {
			previewTex = tex;
		}
	}
}