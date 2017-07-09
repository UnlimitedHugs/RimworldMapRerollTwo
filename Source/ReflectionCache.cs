﻿using System;
using System.Collections.Generic;
using System.Reflection;
using Harmony;
using HugsLib.Utils;
using RimWorld;
using Verse;
using Verse.Sound;

namespace Reroll2 {
	public static class ReflectionCache {

		public static Type ScenPartCreateIncidentType { get; private set; }
		public static Type BeachMakerType { get; private set; }
		public static Type RiverMakerType { get; private set; }

		public static FieldInfo Sustainer_SubSustainers { get; private set; }
		public static FieldInfo SubSustainer_Samples { get; private set; }
		public static FieldInfo Thing_State { get; private set; }
		public static FieldInfo Building_SustainerAmbient { get; private set; }
		public static FieldInfo Scenario_Parts { get; private set; }
		public static FieldInfo CreateIncident_IsFinished { get; private set; }
		public static FieldInfo MapGenerator_Data { get; private set; }
		public static MethodInfo GenStepTerrain_GenerateRiver { get; private set; }
		public static MethodInfo BeachMaker_Init { get; private set; }
		public static MethodInfo BeachMaker_Cleanup { get; private set; }
		public static MethodInfo BeachMaker_BeachTerrainAt { get; private set; }
		public static MethodInfo RiverMaker_TerrainAt { get; private set; }
		
		public static void PrepareReflection() {
			Sustainer_SubSustainers = ReflectField("subSustainers", typeof(Sustainer), typeof(List<SubSustainer>));
			SubSustainer_Samples = ReflectField("samples", typeof(SubSustainer), typeof(List<SampleSustainer>));
			Thing_State = ReflectField("mapIndexOrState", typeof(Thing), typeof(sbyte));
			Building_SustainerAmbient = ReflectField("sustainerAmbient", typeof(Building), typeof(Sustainer));
			Scenario_Parts = ReflectField("parts", typeof(Scenario), typeof(List<ScenPart>));

			ScenPartCreateIncidentType = ReflectType("RimWorld.ScenPart_CreateIncident", typeof(ScenPart).Assembly);
			if (ScenPartCreateIncidentType != null) {
				CreateIncident_IsFinished = ReflectField("isFinished", ScenPartCreateIncidentType, typeof(bool));
			}

			MapGenerator_Data = ReflectField("data", typeof(MapGenerator), typeof(Dictionary<string, object>));

			BeachMakerType = ReflectType("RimWorld.BeachMaker", typeof(GenStep_Terrain).Assembly);
			if (BeachMakerType != null) {
				BeachMaker_Init = ReflectMethod("Init", BeachMakerType, typeof(void), new[] {typeof(Map)});
				BeachMaker_Cleanup = ReflectMethod("Cleanup", BeachMakerType, typeof(void), new Type[0]);
				BeachMaker_BeachTerrainAt = ReflectMethod("BeachTerrainAt", BeachMakerType, typeof(TerrainDef), new[] { typeof(IntVec3), typeof(BiomeDef) });
			}

			RiverMakerType = ReflectType("RimWorld.RiverMaker");
			if (RiverMakerType != null) {
				GenStepTerrain_GenerateRiver = ReflectMethod("GenerateRiver", typeof(GenStep_Terrain), RiverMakerType, new[] {typeof(Map)});
				RiverMaker_TerrainAt = ReflectMethod("TerrainAt", RiverMakerType, typeof(TerrainDef), new[] {typeof(IntVec3)});
			}
		}

		private static Type ReflectType(string nameWithNamespace, Assembly assembly = null) {
			Type type;
			if (assembly == null) {
				type = GenTypes.GetTypeInAnyAssembly(nameWithNamespace);
			} else {
				type = assembly.GetType(nameWithNamespace, false, false);
			}
			if (type == null) {
				Reroll2Controller.Instance.Logger.Error("Failed to reflect required type \"{0}\"", nameWithNamespace);
			}
			return type;
		}

		private static FieldInfo ReflectField(string name, Type parentType, Type expectedFieldType) {
			var field = AccessTools.Field(parentType, name);
			if (field == null) {
				Reroll2Controller.Instance.Logger.Error("Failed to reflect required field \"{0}\" in type \"{1}\".", name, parentType);
			} else if (expectedFieldType != null && field.FieldType != expectedFieldType) {
				Reroll2Controller.Instance.Logger.Error("Reflect field \"{0}\" did not match expected field type of \"{1}\".", name, expectedFieldType);
				field = null;
			}
			return field;
		}

		private static MethodInfo ReflectMethod(string name, Type parentType, Type expectedReturnType, Type[] expectedParameterTypes) {
			var method = AccessTools.Method(parentType, name);
			if (method == null) {
				Reroll2Controller.Instance.Logger.Error("Failed to reflect required method \"{0}\" in type \"{1}\".", name, parentType);
			} else if (!method.MethodMatchesSignature(expectedReturnType, expectedParameterTypes)) {
				Reroll2Controller.Instance.Logger.Error("Reflect method \"{0}\" did not match expected signature.", name);
				method = null;
			}
			return method;
		}
	}
}