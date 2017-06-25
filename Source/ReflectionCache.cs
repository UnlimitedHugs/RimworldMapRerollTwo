using System;
using System.Collections.Generic;
using System.Reflection;
using Harmony;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.Sound;

namespace Reroll2 {
	public static class ReflectionCache {

		public static FieldInfo Sustainer_SubSustainers { get; private set; }
		public static FieldInfo SubSustainer_Samples { get; private set; }

		public static void PrepareReflection() {
			Sustainer_SubSustainers = ReflectField("subSustainers", typeof(Sustainer), typeof(List<SubSustainer>));
			SubSustainer_Samples = ReflectField("samples", typeof(SubSustainer), typeof(List<SampleSustainer>));
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
			}
			return field;
		}
	}
}