using UnityEngine;
using Verse;

namespace Reroll2 {
	public class GraphicData_Monument : GraphicData {
		public Vector2 drawOffset;
		public Vector2 baseDrawSize = Vector2.one;
		public Vector2 diceDrawSize = Vector2.one;
		public ShaderType baseShaderType;
		public ShaderType diceShaderType;
		public ShaderType glowShaderType;
		public string baseTexPath;
		public string diceTexPath;
		public string diceRadialTexPath;
		public string diceGlowTexPath;
		public string diceGlowRadialTexPath;

		public GraphicData_Monument() {
			graphicClass = typeof(Graphic_Monument);
		}

		/*public Texture2D BaseTex { get; private set; }
		public Texture2D DiceTex { get; private set; }
		public Texture2D DiceRadialTex { get; private set; }
		public Texture2D DiceGlowTex { get; private set; }
		public Texture2D DiceGlowRadialTex { get; private set; }*/
		
		/*public void PostLoad() {
			texPath = baseTexPath;
			LongEventHandler.ExecuteWhenFinished(() => {
				BaseTex = ContentFinder<Texture2D>.Get(baseTexPath);
				DiceTex = ContentFinder<Texture2D>.Get(diceTexPath);
				DiceRadialTex = ContentFinder<Texture2D>.Get(diceRadialTexPath);
				DiceGlowTex = ContentFinder<Texture2D>.Get(diceGlowTexPath);
				DiceGlowRadialTex = ContentFinder<Texture2D>.Get(diceGlowRadialTexPath);
			});
		}*/
	}
}