using CM3D2.ExternalSaveData.Managed;
using script;
using System;
using UnityEngine;
using System.Diagnostics;

internal static class KagHooks {
    static bool bKagTagPropSetHooked = false;
    static string PluginName;
    delegate bool TagProcDelegate(BaseKagManager baseKagManager, KagTagSupport tag_data);

    public static void SetHook(string PluginName_, bool bForceSet) {
        PluginName = PluginName_;
        if (!bForceSet && bKagTagPropSetHooked) {
            return;
        }
        bKagTagPropSetHooked = true;
        HookTagCallback("propset", TagPropSet);
        HookTagCallback("faceblend", TagFaceBlend);
        HookTagCallback("face", TagFace);
        HookTagCallback("eyetocamera", TagEyeToCamera);
    }

    static void HookTagCallback(string tagName, TagProcDelegate tagProcDelegate) {
        foreach (var kv in GameMain.Instance.ScriptMgr.kag_mot_dic) {
			var mgr = kv.Value;
			var kag = mgr.kag;
            kag.RemoveTagCallBack(tagName);
            if (tagProcDelegate == null) continue;
            kag.AddTagCallBack(tagName, new KagScript.KagTagCallBack(delegate (KagTagSupport tag_data) {
                return tagProcDelegate(mgr, tag_data);
            }));
        }

        {
			var mgr = GameMain.Instance.ScriptMgr.adv_kag;
			var kag = mgr.kag;
            kag.RemoveTagCallBack(tagName);
            if (tagProcDelegate != null) {
                kag.AddTagCallBack(tagName, new KagScript.KagTagCallBack(delegate (KagTagSupport tag_data) {
                    return tagProcDelegate(mgr, tag_data);
                }));
            }
        }

        {
			var mgr = GameMain.Instance.ScriptMgr.yotogi_kag;
			var kag = mgr.kag;
            kag.RemoveTagCallBack(tagName);
            if (tagProcDelegate != null) {
                kag.AddTagCallBack(tagName, new KagScript.KagTagCallBack(delegate (KagTagSupport tag_data) {
                    return tagProcDelegate(mgr, tag_data);
                }));
            }
        }
    }

    static bool TagPropSet(BaseKagManager baseKagManager, KagTagSupport tag_data) {
		var maidAndMan = baseKagManager.GetMaidAndMan(tag_data);
        if (maidAndMan != null && ExSaveData.GetBool(maidAndMan, PluginName, "PROPSET_OFF", false)) {
			var str = tag_data.GetTagProperty("category").AsString();
            if (Array.IndexOf(PluginHelper.MpnStrings, str) >= 0) {
#if DEBUG
                Console.WriteLine("PROPSET_OFF(category={0}) -> match", str);
                Helper.Log("PROPSET_OFF(category={0}) -> match", str);
#endif
                return false;
            }
        }

        return baseKagManager.TagPropSet(tag_data);
    }

    static bool TagEyeToCamera(BaseKagManager baseKagManager, KagTagSupport tag_data) {
		var maidAndMan = baseKagManager.GetMaidAndMan(tag_data);
        if (maidAndMan != null && ExSaveData.GetBool(maidAndMan, PluginName, "EYETOCAMERA_OFF", false)) {
            return false;
        }
        return baseKagManager.TagEyeToCamera(tag_data);
    }

    static bool TagFace(BaseKagManager baseKagManager, KagTagSupport tag_data) {
		var maidAndMan = baseKagManager.GetMaidAndMan(tag_data);
        if (maidAndMan == null) {
            return false;
        }
        if (maidAndMan != null && ExSaveData.GetBool(maidAndMan, PluginName, "FACE_OFF", false)) {
            // Helper.Log("FACE_OFF() -> match");
            return false;
        }

        baseKagManager.CheckAbsolutelyNecessaryTag(tag_data, "face", new string[] { "name" });

		var oldName = tag_data.GetTagProperty("name").AsString();
		var newName = FaceScriptTemplates.ProcFaceName(maidAndMan, PluginName, oldName);
		// Helper.Log("TagFace({0})->({1})", oldName, newName);

		var waitEventList = GetWaitEventList(baseKagManager, "face");
		var num = 0;
        if (tag_data.IsValid("wait")) {
            num = tag_data.GetTagProperty("wait").AsInteger();
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

    static bool TagFaceBlend(BaseKagManager baseKagManager, KagTagSupport tag_data) {
		var maidAndMan = baseKagManager.GetMaidAndMan(tag_data);
        if (maidAndMan == null) {
            return false;
        }
        if (ExSaveData.GetBool(maidAndMan, PluginName, "FACEBLEND_OFF", false)) {
            Trace.WriteLine("FACEBLEND_OFF() -> match");
            return false;
        }

        baseKagManager.CheckAbsolutelyNecessaryTag(tag_data, "faceblend", new string[] { "name" });

		var oldName = tag_data.GetTagProperty("name").AsString();
        if (oldName == "なし") {
            oldName = "無し";
        }

		var newName = FaceScriptTemplates.ProcFaceBlendName(maidAndMan, PluginName, oldName);
        Trace.WriteLine($"TagFaceBlend({oldName})->({newName})");

        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "ScenePhotoMode") {
            newName = "オリジナル";
        }

        maidAndMan.FaceBlend(newName);
        return false;
    }

    static WaitEventList GetWaitEventList(BaseKagManager baseKagManager, string list_name) {
        return baseKagManager.GetWaitEventList(list_name);
    }
}
