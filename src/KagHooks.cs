using CM3D2.ExternalSaveData.Managed;
using CM3D2.MaidVoicePitch.Plugin;

internal static class KagHooks {
	private static bool kagTagPropSetHooked = false;
	private static string _pluginName;

	delegate bool TagProcDelegate(BaseKagManager baseKagManager, KagTagSupport tag_data);

	public static void SetHook(string pluginName, bool forceSet) {
		_pluginName = pluginName;
		if (!forceSet && kagTagPropSetHooked) {
			return;
		}
		kagTagPropSetHooked = true;
		HookTagCallback("propset", TagPropSet);
		HookTagCallback("faceblend", TagFaceBlend);
		HookTagCallback("face", TagFace);
		HookTagCallback("eyetocamera", TagEyeToCamera);
	}

	private static void HookTagCallback(string tagName, TagProcDelegate tagProcDelegate) {
		foreach (var kv in GameMain.Instance.ScriptMgr.kag_mot_dic.Values) {
			HookTagCallback(tagName, tagProcDelegate, kv);
		}

		HookTagCallback(tagName, tagProcDelegate, GameMain.Instance.ScriptMgr.adv_kag);
		HookTagCallback(tagName, tagProcDelegate, GameMain.Instance.ScriptMgr.yotogi_kag);
	}

	private static void HookTagCallback(string tagName, TagProcDelegate tagProcDelegate, BaseKagManager baseKagManager) {
		var kagScript = baseKagManager.kag;
		kagScript.RemoveTagCallBack(tagName);
		if (tagProcDelegate != null) {
			kagScript.AddTagCallBack(tagName, new((tagData) => tagProcDelegate(baseKagManager, tagData)));
		}
	}

	private static bool TagPropSet(BaseKagManager baseKagManager, KagTagSupport tagData) {
		var maidAndMan = baseKagManager.GetMaidAndMan(tagData);
		if (maidAndMan != null && ExSaveData.GetBool(maidAndMan, _pluginName, "PROPSET_OFF", false)) {
			var str = tagData.GetTagProperty("category").AsString();
			if (Array.IndexOf(PluginHelper.MpnStrings, str) >= 0) {
#if DEBUG
				MaidVoicePitch.LogDebug($"PROPSET_OFF(category={str}) -> match");
#endif
				return false;
			}
		}

		return baseKagManager.TagPropSet(tagData);
	}

	private static bool TagEyeToCamera(BaseKagManager baseKagManager, KagTagSupport tagData) {
		var maidAndMan = baseKagManager.GetMaidAndMan(tagData);
		if (maidAndMan != null && ExSaveData.GetBool(maidAndMan, _pluginName, "EYETOCAMERA_OFF", false)) {
			return false;
		}
		return baseKagManager.TagEyeToCamera(tagData);
	}

	private static bool TagFace(BaseKagManager baseKagManager, KagTagSupport tagData) {
		var maidAndMan = baseKagManager.GetMaidAndMan(tagData);
		if (maidAndMan == null) {
			return false;
		}
		if (maidAndMan != null && ExSaveData.GetBool(maidAndMan, _pluginName, "FACE_OFF", false)) {
			// Helper.Log("FACE_OFF() -> match");
			return false;
		}

		baseKagManager.CheckAbsolutelyNecessaryTag(tagData, "face", new[] { "name" });

		var oldName = tagData.GetTagProperty("name").AsString();
		var newName = FaceScriptTemplates.ProcFaceName(maidAndMan, _pluginName, oldName);
		// Helper.Log($"TagFace({oldName})->({newName})");

		var waitEventList = baseKagManager.GetWaitEventList("face");
		var num = 0;
		if (tagData.IsValid("wait")) {
			num = tagData.GetTagProperty("wait").AsInteger();
		}
		if (num > 0) {
			waitEventList.Add(() => {
				if (maidAndMan != null && maidAndMan.body0 != null && maidAndMan.body0.isLoadedBody) {
					maidAndMan.FaceAnime(newName, 1f, 0);
				}
			}, num);
		} else {
			maidAndMan.FaceAnime(newName, 1f, 0);
			waitEventList.Clear();
		}
		return false;
	}

	private static bool TagFaceBlend(BaseKagManager baseKagManager, KagTagSupport tagData) {
		var maidAndMan = baseKagManager.GetMaidAndMan(tagData);
		if (maidAndMan == null) {
			return false;
		}

		if (ExSaveData.GetBool(maidAndMan, _pluginName, "FACEBLEND_OFF", false)) {
			MaidVoicePitch.LogDebug("FACEBLEND_OFF() -> match");
			return false;
		}

		baseKagManager.CheckAbsolutelyNecessaryTag(tagData, "faceblend", new[] { "name" });

		var oldName = tagData.GetTagProperty("name").AsString();
		if (oldName == "なし") {
			oldName = "無し";
		}

		var newName = FaceScriptTemplates.ProcFaceBlendName(maidAndMan, _pluginName, oldName);
		MaidVoicePitch.LogDebug($"TagFaceBlend({oldName})->({newName})");

		if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "ScenePhotoMode") {
			newName = "オリジナル";
		}

		maidAndMan.FaceBlend(newName);
		return false;
	}
}
