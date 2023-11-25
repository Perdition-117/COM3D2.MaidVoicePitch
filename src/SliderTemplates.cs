using System.IO;
using System.Xml;
using BepInEx;
using CM3D2.ExternalSaveData.Managed;
using CM3D2.MaidVoicePitch.Plugin;

internal static class SliderTemplates {
	private static readonly Cache SliderTemplateCache = new();

	public static void Clear() {
		SliderTemplateCache.Clear();
	}

	public static void Update(string PluginName) {
		// エディット画面以外では何もせず終了
		if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "SceneEdit") {
			return;
		}
		var cm = GameMain.Instance.CharacterMgr;
		for (var i = 0; i < cm.GetStockMaidCount(); i++) {
			var maid = cm.GetStockMaid(i);
			Update(maid, PluginName);
		}
	}

	public static void Update(Maid maid, string pluginName) {
		if (!maid.Visible) {
			return;
		}
		var fileName = ExSaveData.Get(maid, pluginName, "SLIDER_TEMPLATE", MaidVoicePitch.DefaultTemplateFile);
		fileName = Path.Combine(Paths.ConfigPath, fileName);
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
#if DEBUG
					Helper.Log($"SliderTemplates.SliderTemplate.Load({fileName})");
#endif
					document.Load(fileName);
					foreach (XmlNode node in document.SelectNodes("/slidertemplate/sliders/slider")) {
						Sliders[node.Attributes["name"].Value] = new() {
							Min = Helper.StringToFloat(node.Attributes["min"].Value, 0f),
							Max = Helper.StringToFloat(node.Attributes["max"].Value, 100f)
						};
#if DEBUG
						{
							var name = node.Attributes["name"].Value;
							var slider = Sliders[name];
							Helper.Log($"  {name} .min={slider.Min:F6}, .max={slider.Max:F6}");
						}
#endif
					}
					// Helper.Log($"SliderTemplates.SliderTemplate.Load({fileName}) -> ok");
					result = true;
				}
			} catch (Exception e) {
				Helper.ShowException(e);
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
