using System.IO;
using System.Xml;
using CM3D2.ExternalSaveData.Managed;
using CM3D2.MaidVoicePitch.Plugin;

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
		var fileName = ExSaveData.Get(maid, pluginName, "SLIDER_TEMPLATE", MaidVoicePitch.DefaultTemplateFile);
		fileName = Path.Combine(MaidVoicePitch.PluginPath, fileName);
		PathCheck(maid, pluginName, fileName);
		var sliderTemplate = SliderTemplateCache.Get(fileName);
		var guid = maid.status.guid;
		if (sliderTemplate != null && !sliderTemplate.LoadedMaidGuids.Contains(guid)) {
			sliderTemplate.WriteProps(maid);
			sliderTemplate.LoadedMaidGuids.Add(guid);
		}
	}

	private static void PathCheck(Maid maid, string pluginName, string fileName) {
		if (!File.Exists(fileName)) {
			fileName = MaidVoicePitch.DefaultTemplateFile;
			ExSaveData.Set(maid, pluginName, "SLIDER_TEMPLATE", fileName);
		}
	}

	class Cache : TemplateFiles<SliderTemplate> { }

	class SliderTemplate : ITemplateFile {
		public class Slider {
			public float Min { get; set; }
			public float Max { get; set; }
		}

		public Dictionary<string, Slider> Sliders { get; set; }
		public HashSet<string> LoadedMaidGuids = new();

		public SliderTemplate() {
			Clear();
		}

		void Clear() {
			Sliders = new();
		}

		public bool Load(string fileName) {
			var result = false;
			Clear();
			var document = new XmlDocument();
			try {
				if (File.Exists(fileName)) {
					document.Load(fileName);
					foreach (XmlNode node in document.SelectNodes("/slidertemplate/sliders/slider")) {
						Sliders[node.Attributes["name"].Value] = new() {
							Min = Helper.StringToFloat(node.Attributes["min"].Value, 0f),
							Max = Helper.StringToFloat(node.Attributes["max"].Value, 100f),
						};
					}
					// Helper.Log($"SliderTemplates.SliderTemplate.Load({fileName}) -> ok");
					result = true;
				}
			} catch (Exception e) {
				MaidVoicePitch.LogError(e);
			}
			return result;
		}

		public void WriteProps(Maid maid) {
			foreach (var kv in Sliders) {
				var name = kv.Key;
				var slider = kv.Value;
				var mpn = Helper.ToEnum(name, MPN.null_mpn);
				if (mpn != MPN.null_mpn) {
					var maidProp = maid.GetProp(mpn);
					maidProp.min = (int)slider.Min;
					maidProp.max = (int)slider.Max;
				}
			}
		}
	}
}
