using System.IO;
using System.Xml;
using BepInEx;
using CM3D2.ExternalSaveData.Managed;

namespace CM3D2.MaidVoicePitch.Plugin;

internal static class FaceScriptTemplates {
	private static readonly Cache FaceScriptTemplateCache = new();

	public static void Clear() {
		FaceScriptTemplateCache.Clear();
	}

	public static string ProcFaceName(Maid maid, string pluginName, string faceName) {
		var t = Get(maid, pluginName);
		if (t == null) {
			// Helper.Log($"FaceScriptTemplates.ProcFaceName({maid},{pluginName},{faceName}) -> null");
			return faceName;
		}
		return t.ProcFaceName(faceName);
	}

	public static string ProcFaceBlendName(Maid maid, string pluginName, string faceBlendName) {
		var t = Get(maid, pluginName);
		if (t == null) {
			// Helper.Log($"FaceScriptTemplates.ProcFaceBlendName({maid},{pluginName},{faceBlendName}) -> null");
			return faceBlendName;
		}
		return t.ProcFaceBlendName(faceBlendName);
	}

	private static TemplateFile Get(Maid maid, string pluginName) {
		return Get(ExSaveData.Get(maid, pluginName, "FACE_SCRIPT_TEMPLATE", null));
	}

	private static TemplateFile Get(string fileName) {
		if (fileName != null) {
			fileName = Path.Combine(Paths.ConfigPath, fileName);
		}
		var t = FaceScriptTemplateCache.Get(fileName);
		// Helper.Log($"FaceScriptTemplates.Get({fileName}) -> {t}");
		return t;
	}

	class Cache : TemplateFiles<TemplateFile> { }

	class TemplateFile : ITemplateFile {
		public Dictionary<string, string> FaceBlends { get; set; }
		public Dictionary<string, string> Faces { get; set; }

		public TemplateFile() {
			Clear();
		}

		public void Clear() {
			FaceBlends = new();
			Faces = new();
		}

		public bool Load(string fileName) {
			var result = false;
			Clear();
			var document = new XmlDocument();
			try {
				if (File.Exists(fileName)) {
					document.Load(fileName);
					foreach (XmlNode node in document.SelectNodes("/facescripttemplate/faceblends/faceblend")) {
						FaceBlends[node.Attributes["key"].Value] = node.Attributes["value"].Value;
					}
					foreach (XmlNode node in document.SelectNodes("/facescripttemplate/faces/face")) {
						Faces[node.Attributes["key"].Value] = node.Attributes["value"].Value;
					}
					// Helper.Log($"FaceScriptTemplates.TemplateFile({fileName}) -> ok");
					result = true;
				}
			} catch (Exception e) {
				MaidVoicePitch.LogError(e);
			}
			return result;
		}

		public string ProcFaceName(string faceName) {
			if (Faces.TryGetValue(faceName, out var s)) {
				// Helper.Log($"FaceScriptTemplates.TemplateFile.ProcFaceName({faceName}) -> {s}");
				return s;
			}
			// Helper.Log($"FaceScriptTemplates.TemplateFile.ProcFaceName({faceName}) -> fail");
			return faceName;
		}

		public string ProcFaceBlendName(string faceBlendName) {
			if (FaceBlends.TryGetValue(faceBlendName, out var s)) {
				// Helper.Log($"FaceScriptTemplates.TemplateFile.ProcFaceBlendName({faceBlendName}) -> {s}");
				return s;
			}
			// Helper.Log($"FaceScriptTemplates.TemplateFile.ProcFaceBlendName({faceBlendName}) -> fail");
			return faceBlendName;
		}
	}
}
