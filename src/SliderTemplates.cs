// スライダー範囲拡大を指定するテンプレートファイル
using BepInEx;
using CM3D2.ExternalSaveData.Managed;
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

internal static class SliderTemplates {
    class Cache : TemplateFiles<SliderTemplate> { }
    static Cache sliderTemplates = new();

    public static void Clear() {
        sliderTemplates.Clear();
    }

    public static void Update(string PluginName) {
        // エディット画面以外では何もせず終了
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "SceneEdit") {
            return;
        }
		var cm = GameMain.Instance.CharacterMgr;
        for (int i = 0, n = cm.GetStockMaidCount(); i < n; i++) {
            Maid maid = cm.GetStockMaid(i);
            Update(maid, PluginName);
        }
    }

    static public void Update(Maid maid, string PluginName) {
        if (!maid.Visible) {
            return;
        }
		var fname = ExSaveData.Get(maid, PluginName, "SLIDER_TEMPLATE", "MaidVoicePitchSlider.xml");
        fname = Path.Combine(Paths.ConfigPath, fname);
		PathCheck(maid, PluginName, fname);
		var sliderTemplate = sliderTemplates.Get(fname);
		var guid = maid.status.guid;
        if (sliderTemplate != null && !sliderTemplate.LoadedMaidGuids.Contains(guid)) {
            sliderTemplate.WriteProps(maid);
            sliderTemplate.LoadedMaidGuids.Add(guid);
        }
    }

    static void PathCheck(Maid maid, string PluginName, string fname) {
        if (!File.Exists(fname)) {
            fname = "MaidVoicePitchSlider.xml";
            ExSaveData.Set(maid, PluginName, "SLIDER_TEMPLATE", fname);
        }
    }

    class SliderTemplate : ITemplateFile {
        public class Slider {
            public float min;
            public float max;
        }

        public Dictionary<string, Slider> Sliders { get; set; }
        public HashSet<string> LoadedMaidGuids = new();

        public SliderTemplate() {
            Clear();
        }

        void Clear() {
            Sliders = new();
        }

        public bool Load(string fname) {
			var result = false;
            Clear();
            var xd = new XmlDocument();
            try {
                if (File.Exists(fname)) {
#if DEBUG
                    Helper.Log("SliderTemplates.SliderTemplate.Load({0})", fname);
#endif
                    xd.Load(fname);
                    foreach (XmlNode e in xd.SelectNodes("/slidertemplate/sliders/slider")) {
                        Sliders[e.Attributes["name"].Value] = new() {
                            min = Helper.StringToFloat(e.Attributes["min"].Value, 0f),
                            max = Helper.StringToFloat(e.Attributes["max"].Value, 100f)
                        };
#if DEBUG
                        {
                            var name = e.Attributes["name"].Value;
                            var s = Sliders[name];
                            Helper.Log("  {0} .min={1:F6}, .max={2:F6}", name, s.min, s.max);
                        }
#endif
                    }
                    // Helper.Log("SliderTemplates.SliderTemplate.Load({0}) -> ok", fname);
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
				var mpn = Helper.ToEnum<MPN>(name, MPN.null_mpn);
                if (mpn != MPN.null_mpn) {
					var maidProp = maid.GetProp(mpn);
                    maidProp.min = (int)slider.min;
                    maidProp.max = (int)slider.max;
                }
            }
        }
    }
}
