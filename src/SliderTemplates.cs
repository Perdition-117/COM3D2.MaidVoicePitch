using System.IO;
using System.Xml;
using System.Xml.Serialization;
using ExtensionMethods;
using HarmonyLib;
using static MaidVoicePitch.MaidVoicePitch;

namespace MaidVoicePitch;

internal class SliderTemplates {
	private static readonly string SchemaPath = Path.Combine(PluginPath, "SliderLimits.xml");
	private static readonly XmlSerializer SchemaSerializer = new(typeof(SliderLimits));

	private static readonly SliderLimits SliderLimits;

	static SliderTemplates() {
		using var reader = XmlReader.Create(SchemaPath);
		SliderLimits = (SliderLimits)SchemaSerializer.Deserialize(reader);
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(SceneEdit), nameof(SceneEdit.Start))]
	private static void SceneEdit_Start(SceneEdit __instance) {
		foreach (var slider in SliderLimits.Sliders) {
			var mpn = slider.Name.ToEnum(MPN.null_mpn);
			if (mpn != MPN.null_mpn) {
				var maidProp = __instance.maid.GetProp(mpn);
				maidProp.min = slider.MinValue;
				maidProp.max = slider.MaxValue;
			}
		}
	}
}
