using System.IO;
using System.Xml;
using System.Xml.Serialization;
using CM3D2.ExternalSaveData.Managed;
using ExtensionMethods;
using static CM3D2.MaidVoicePitch.Plugin.MaidVoicePitch;

namespace MaidVoicePitch;

internal static class SliderTemplates {
	private static readonly XmlSerializer SchemaSerializer = new(typeof(SliderLimits));
	private static readonly Cache SliderTemplateCache = new();

	public static void Clear() {
		SliderTemplateCache.Clear();
	}

	public static void Update(string pluginName) {
		// エディット画面以外では何もせず終了
		if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "SceneEdit") {
			return;
		}
		foreach (var maid in PluginHelper.GetMaids()) {
			Update(maid, pluginName);
		}
	}

	public static void Update(Maid maid, string pluginName) {
		if (!maid.Visible) {
			return;
		}
		var fileName = ExSaveData.Get(maid, pluginName, "SLIDER_TEMPLATE", DefaultTemplateFile);
		fileName = Path.Combine(PluginPath, fileName);
		if (!File.Exists(fileName)) {
			fileName = DefaultTemplateFile;
			ExSaveData.Set(maid, pluginName, "SLIDER_TEMPLATE", fileName);
		}
		var sliderTemplate = SliderTemplateCache.GetTemplate(fileName);
		var guid = maid.status.guid;
		if (sliderTemplate != null && !sliderTemplate.LoadedMaidGuids.Contains(guid)) {
			sliderTemplate.WriteProps(maid);
			sliderTemplate.LoadedMaidGuids.Add(guid);
		}
	}

	class Cache : TemplateFiles<SliderTemplate> { }

	class SliderTemplate : ITemplateFile {
		public HashSet<string> LoadedMaidGuids = new();
		private SliderLimits _sliderLimits;

		public bool Load(string fileName) {
			var result = false;
			try {
				if (File.Exists(fileName)) {
					using var reader = XmlReader.Create(fileName);
					_sliderLimits = (SliderLimits)SchemaSerializer.Deserialize(reader);
					// Helper.Log($"SliderTemplates.SliderTemplate.Load({fileName}) -> ok");
					result = true;
				}
			} catch (Exception e) {
				LogError(e);
			}
			return result;
		}

		public void WriteProps(Maid maid) {
			foreach (var slider in _sliderLimits.Sliders) {
				var mpn = slider.Name.ToEnum(MPN.null_mpn);
				if (mpn != MPN.null_mpn) {
					var maidProp = maid.GetProp(mpn);
					maidProp.min = slider.MinValue;
					maidProp.max = slider.MaxValue;
				}
			}
		}
	}
}
