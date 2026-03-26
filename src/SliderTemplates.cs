using System.IO;
using System.Xml;
using CM3D2.ExternalSaveData.Managed;
using ExtensionMethods;
using static CM3D2.MaidVoicePitch.Plugin.MaidVoicePitch;

namespace MaidVoicePitch;

internal static class SliderTemplates {
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
		public class Slider {
			public float Min { get; set; }
			public float Max { get; set; }
		}

		private readonly Dictionary<string, Slider> _sliders = new();
		public HashSet<string> LoadedMaidGuids = new();

		private void Clear() {
			_sliders.Clear();
		}

		public bool Load(string fileName) {
			var result = false;
			Clear();
			var document = new XmlDocument();
			try {
				if (File.Exists(fileName)) {
					document.Load(fileName);
					foreach (XmlNode node in document.SelectNodes("/slidertemplate/sliders/slider")) {
						_sliders[node.Attributes["name"].Value] = new() {
							Min = StringToFloat(node.Attributes["min"].Value, 0f),
							Max = StringToFloat(node.Attributes["max"].Value, 100f),
						};
					}
					// Helper.Log($"SliderTemplates.SliderTemplate.Load({fileName}) -> ok");
					result = true;
				}
			} catch (Exception e) {
				LogError(e);
			}
			return result;
		}

		public void WriteProps(Maid maid) {
			foreach (var kv in _sliders) {
				var name = kv.Key;
				var slider = kv.Value;
				var mpn = name.ToEnum(MPN.null_mpn);
				if (mpn != MPN.null_mpn) {
					var maidProp = maid.GetProp(mpn);
					maidProp.min = (int)slider.Min;
					maidProp.max = (int)slider.Max;
				}
			}
		}

		private static float StringToFloat(string s, float defaultValue) {
			return s != null && float.TryParse(s, out var v) ? v : defaultValue;
		}
	}
}
