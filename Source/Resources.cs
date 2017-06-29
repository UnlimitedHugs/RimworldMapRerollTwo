// ReSharper disable UnassignedField.Global
using HugsLib.Utils;
using RimWorld;
using UnityEngine;
using Verse;

namespace Reroll2 {
	/// <summary>
	/// Auto-filled repository of all external resources referenced in the code
	/// </summary>
	public static class Resources {
		[DefOf]
		public static class Thing {
			public static ThingDef RerollMonument;
		}

		[DefOf]
		public static class Sound {
			public static SoundDef RerollSteamVent;
			public static SoundDef RerollMonumentStartup;
			public static SoundDef RerollMonumentDrone;
		}
		
		[DefOf]
		public static class Settings {
			public static RerollSettingsDef MapRerollSettings;
		}

		[StaticConstructorOnStartup]
		public static class Textures {
			public static readonly Texture2D ResourceBarFull = SolidColorMaterials.NewSolidColorTexture(new Color(0.2f, 0.2f, 0.24f));
			public static readonly Texture2D ResourceBarEmpty = SolidColorMaterials.NewSolidColorTexture(Color.clear);
			public static Texture2D UIRerollMap;
			public static Texture2D UIRerollGeysers;

			static Textures() {
				foreach (var fieldInfo in typeof(Textures).GetFields(HugsLibUtility.AllBindingFlags)) {
					if(fieldInfo.IsInitOnly) continue;
					fieldInfo.SetValue(null, ContentFinder<Texture2D>.Get(fieldInfo.Name));
				}
			}
		}
	}
}