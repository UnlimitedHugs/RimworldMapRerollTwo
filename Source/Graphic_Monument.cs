using System;
using UnityEngine;
using Verse;

namespace Reroll2 {
	public class Graphic_Monument : Graphic {
		private new GraphicData_Monument data;

		private Material BaseMat { set; get; }

		public override void Init(GraphicRequest req) {
			data = req.graphicData as GraphicData_Monument;
			if (data == null) {
				throw new Exception("Graphic_Monument requires GraphicData_Monument data type");
			}
			drawSize = req.drawSize;
			BaseMat = MaterialPool.MatFrom(data.baseTexPath, ShaderDatabase.ShaderFromType(data.baseShaderType), req.color);
		}

		public override Material MatSingle {
			get { return BaseMat; }
		}

		public override void DrawWorker(Vector3 loc, Rot4 rot, ThingDef thingDef, Thing thing) {
			var monument = thing as Building_Monument;
			if (monument == null) {
				const string errorMessage = "Graphic_Monument can only be applied to Building_Monument";
				Log.ErrorOnce(errorMessage, errorMessage.GetHashCode());
				return;
			}
			var materials = monument.Materials;
			if (materials == null) {
				materials = monument.Materials = new Graphic_Monument_Materials(data, thing.DrawColor);
			}
			var rotation = Quaternion.Euler(0, monument.DiceRotation, 0);
			loc = new Vector3(loc.x + data.drawOffset.x, loc.y, loc.z + data.drawOffset.y);
			// base
			var baseMesh = MeshPool.GridPlane(data.baseDrawSize);
			Graphics.DrawMesh(baseMesh, loc, Quaternion.identity, materials.BaseMat, 0);

			// dice 
			var diceMesh = MeshPool.GridPlane(data.diceDrawSize);
			Graphics.DrawMesh(diceMesh, loc + Altitudes.AltIncVect*2f, rotation, materials.DiceMat, 0);

			// dice blur
			materials.DiceRadialMat.color = new Color(1f, 1f, 1f, monument.RadialAlpha);
			Graphics.DrawMesh(diceMesh, loc + Altitudes.AltIncVect*3f, rotation, materials.DiceRadialMat, 0);

			// glow 
			var hsv = Color.HSVToRGB(monument.GlowColorHue, monument.GlowColorSaturation, 1f);
			materials.DiceGlowMat.color = new Color(hsv.r, hsv.g, hsv.b, Mathf.Lerp(monument.GlowAlpha, 0, monument.RadialAlpha));
			Graphics.DrawMesh(diceMesh, loc + Altitudes.AltIncVect*4f, rotation, materials.DiceGlowMat, 0);

			// glow blur
			materials.DiceGlowRadialMat.color = new Color(hsv.r, hsv.g, hsv.b, monument.GlowAlpha*monument.RadialAlpha);
			Graphics.DrawMesh(diceMesh, loc + Altitudes.AltIncVect*5f, rotation, materials.DiceGlowRadialMat, 0);

			if (ShadowGraphic != null) {
				ShadowGraphic.DrawWorker(loc, rot, thingDef, thing);
			}
		}
	}

	/// <summary>
	/// For storage in the building instance. 
	/// This is better than instantiating hundreds of materials in MaterialPool (color*alpha)
	/// </summary>
	public class Graphic_Monument_Materials {
		public readonly Material BaseMat;
		public readonly Material DiceMat;
		public readonly Material DiceRadialMat;
		public readonly Material DiceGlowMat;
		public readonly Material DiceGlowRadialMat;

		public Graphic_Monument_Materials(GraphicData_Monument data, Color colorModifier) {
			var baseShader = ShaderDatabase.ShaderFromType(data.baseShaderType);
			BaseMat = ScheduleTextureForMaterial(new Material(baseShader), data.baseTexPath);
			var diceShader = ShaderDatabase.ShaderFromType(data.diceShaderType);
			DiceMat = ScheduleTextureForMaterial(new Material(diceShader), data.diceTexPath);
			DiceMat.color = Color.white;
			DiceRadialMat = ScheduleTextureForMaterial(new Material(diceShader), data.diceRadialTexPath);
			var glowShader = ShaderDatabase.ShaderFromType(data.glowShaderType);
			DiceGlowMat = ScheduleTextureForMaterial(new Material(glowShader), data.diceGlowTexPath);
			DiceGlowRadialMat = ScheduleTextureForMaterial(new Material(glowShader), data.diceGlowRadialTexPath);
		}

		private Material ScheduleTextureForMaterial(Material mat, string texturePath) {
			LongEventHandler.ExecuteWhenFinished(() => {
				mat.name = mat.shader.name + "_" + texturePath;
				mat.mainTexture = ContentFinder<Texture2D>.Get(texturePath);
			});
			return mat;
		}
	}
}