using System.IO;
using System.Xml;
using BepInEx;
using CM3D2.ExternalSaveData.Managed;
using static CM3D2.MaidVoicePitch.Plugin.MaidVoicePitch;

namespace MaidVoicePitch;

internal static class FaceScriptTemplates {
	private static readonly Cache FaceScriptTemplateCache = new();

	public static void Clear() {
		FaceScriptTemplateCache.Clear();
	}

	public static string ProcFaceName(Maid maid, string pluginName, string faceName) {
		if (TryGetTemplate(maid, pluginName, out var template)) {
			// Helper.Log($"FaceScriptTemplates.ProcFaceName({maid},{pluginName},{faceName}) -> null");
			return template.ProcFaceName(faceName);
		}
		return faceName;
	}

	public static string ProcFaceBlendName(Maid maid, string pluginName, string faceBlendName) {
		if (TryGetTemplate(maid, pluginName, out var template)) {
			// Helper.Log($"FaceScriptTemplates.ProcFaceBlendName({maid},{pluginName},{faceBlendName}) -> null");
			return template.ProcFaceBlendName(faceBlendName);
		}
		return faceBlendName;
	}

	private static bool TryGetTemplate(Maid maid, string pluginName, out TemplateFile template) {
		var fileName = ExSaveData.Get(maid, pluginName, "FACE_SCRIPT_TEMPLATE", null);
		if (fileName != null) {
			fileName = Path.Combine(Paths.ConfigPath, fileName);
		}
		template = FaceScriptTemplateCache.GetTemplate(fileName);
		// Helper.Log($"FaceScriptTemplates.Get({fileName}) -> {t}");
		return template != null;
	}

	class Cache : TemplateFiles<TemplateFile> { }

	class TemplateFile : ITemplateFile {
		private readonly Dictionary<string, string> _faces = new();
		private readonly Dictionary<string, string> _faceBlends = new();

		private void Clear() {
			_faces.Clear();
			_faceBlends.Clear();
		}

		public bool Load(string fileName) {
			var result = false;
			Clear();
			try {
				if (File.Exists(fileName)) {
					var document = new XmlDocument();
					document.Load(fileName);
					foreach (XmlNode node in document.SelectNodes("/facescripttemplate/faces/face")) {
						_faces[node.Attributes["key"].Value] = node.Attributes["value"].Value;
					}
					foreach (XmlNode node in document.SelectNodes("/facescripttemplate/faceblends/faceblend")) {
						_faceBlends[node.Attributes["key"].Value] = node.Attributes["value"].Value;
					}
					// Helper.Log($"FaceScriptTemplates.TemplateFile({fileName}) -> ok");
					result = true;
				}
			} catch (Exception e) {
				LogError(e);
			}
			return result;
		}

		public string ProcFaceName(string faceName) {
			return _faces.TryGetValue(faceName, out var s) ? s : faceName;
		}

		public string ProcFaceBlendName(string faceBlendName) {
			return _faceBlends.TryGetValue(faceBlendName, out var s) ? s : faceBlendName;
		}
	}
}
