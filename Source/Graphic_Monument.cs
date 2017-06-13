using System;
using UnityEngine;
using Verse;

namespace Reroll2 {
	public class Graphic_Monument : Graphic {
		private new GraphicData_Monument data;

		private Material BaseMat { set; get; }
		private Material DiceMat { get; set; }
		private Material DiceRadialMat { get; set; }
		private Material DiceGlowMat { get; set; }
		private Material DiceGlowRadialMat { get; set; }

		public override void Init(GraphicRequest req) {
			data = req.graphicData as GraphicData_Monument;
			if (data == null) {
				throw new Exception("Graphic_Monument requires GraphicData_Monument data type");
			}
			drawSize = req.drawSize;
			InitMaterials(req);
		}

		public override Material MatSingle {
			get { return BaseMat; }
		}

		private void InitMaterials(GraphicRequest req) {
			var baseShader = ShaderDatabase.ShaderFromType(data.baseShaderType);
			BaseMat = MaterialPool.MatFrom(data.baseTexPath, baseShader, req.color);
			DiceMat = MaterialPool.MatFrom(data.diceTexPath, baseShader, req.color);
			DiceRadialMat = MaterialPool.MatFrom(data.diceRadialTexPath, baseShader, req.color);
			var glowShader = ShaderDatabase.ShaderFromType(data.glowShaderType);
			DiceGlowMat = MaterialPool.MatFrom(data.diceGlowTexPath, glowShader, Color.white);
			DiceGlowRadialMat = MaterialPool.MatFrom(data.diceGlowRadialTexPath, glowShader, Color.white);
		}
		
		public override void DrawWorker(Vector3 loc, Rot4 rot, ThingDef thingDef, Thing thing) {
			var rotation = QuatFromRot(rot);
			loc = new Vector3(loc.x + data.drawOffset.x, loc.y, loc.z + data.drawOffset.y);
			var baseMesh = MeshPool.GridPlane(data.baseDrawSize);
			Graphics.DrawMesh(baseMesh, loc, rotation, BaseMat, 0);
			var diceMesh = MeshPool.GridPlane(data.diceDrawSize);
			Graphics.DrawMesh(diceMesh, loc + Altitudes.AltIncVect, rotation, DiceMat, 0);
			Graphics.DrawMesh(diceMesh, loc + Altitudes.AltIncVect*2f, rotation, DiceGlowMat, 0);
			if (ShadowGraphic != null) {
				ShadowGraphic.DrawWorker(loc, rot, thingDef, thing);
			}
		}
	}
}