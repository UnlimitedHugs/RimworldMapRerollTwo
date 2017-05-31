using Harmony;
using RimWorld;

namespace Reroll2.Patches {
	//private void PodOpen()
	[HarmonyPatch(typeof(ActiveDropPod), "PodOpen")]
	public class ActiveDropPod_PodOpen_Patch {
		[HarmonyPrefix]
		public static void RecordPodContents(ActiveDropPod __instance) {
			Reroll2Controller.Instance.RecordPlayerAddedMapThings(__instance.Contents.ParentHolder, __instance.Map);
		}
	}
}