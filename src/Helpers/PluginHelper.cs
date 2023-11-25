using System;
using System.Collections.Generic;
using UnityEngine;

internal static class PluginHelper
{
    static string[] mpnStrings;

#if DEBUG
    public static bool bDebugEnable = true;
#else
    public static bool bDebugEnable = false;
#endif

    public static List<string> debugLines = new();
    public static int debugLinesMax = 100;
    public static Vector2 debugScrollPosition = new(0f, 0f);
    const int margin = 20;
    const int windowId = 0x123456;
    public static Rect debugWindowRect = new(margin, margin, Screen.width / 2 - (margin * 2), Screen.height - (margin * 2));

    public static string[] MpnStrings
    {
        get
        {
            var mpnValues = Enum.GetValues(typeof(MPN));
            mpnStrings = new string[mpnValues.Length];
            for (int i = 0, n = mpnValues.Length; i < n; i++)
            {
				var mpn = (MPN)mpnValues.GetValue(i);
                mpnStrings[i] = $"{mpn:G}";
            }
            return mpnStrings;
        }
    }

    public static Maid GetMaid(TBody tbody)
    {
        return tbody.maid;
    }

    // AudioSourceMgrを手がかりに、Maidを得る
    public static Maid GetMaid(AudioSourceMgr audioSourceMgr)
    {
        if (audioSourceMgr == null)
        {
            return null;
        }
		var cm = GameMain.Instance.CharacterMgr;
        for (int i = 0, n = cm.GetStockMaidCount(); i < n; i++)
        {
			var maid = cm.GetStockMaid(i);
            if (maid.AudioMan == null)
            {
                continue;
            }
            if (object.ReferenceEquals(maid.AudioMan, audioSourceMgr))
            {
                return maid;
            }
        }
        return null;
    }

    // BoneMorph_を手がかりに、Maidを得る
    public static Maid GetMaid(BoneMorph_ boneMorph_)
    {
        if (boneMorph_ == null)
        {
            return null;
        }
		var cm = GameMain.Instance.CharacterMgr;
        for (int i = 0, n = cm.GetStockMaidCount(); i < n; i++)
        {
			var maid = cm.GetStockMaid(i);
            if (maid.body0 == null || maid.body0.bonemorph == null)
            {
                continue;
            }
            if (object.ReferenceEquals(maid.body0.bonemorph, boneMorph_))
            {
                return maid;
            }
        }
        return null;
    }

    // BoneMorph_.SetScaleを呼び出す
    public static void BoneMorphSetScale(string tag, string bname, float x, float y, float z, float x2, float y2, float z2)
    {
        BoneMorph.SetScale(tag, bname, x, y, z, x2, y2, z2);
    }

    public static void DebugGui()
    {
        if (bDebugEnable && debugLines != null && debugLines.Count > 0)
        {
            debugWindowRect = GUILayout.Window(windowId, debugWindowRect, DebugGuiWindow, "Debug");
        }
    }

    public static void DebugGuiWindow(int windowId)
    {
        debugScrollPosition = GUILayout.BeginScrollView(debugScrollPosition);
        foreach (var line in debugLines)
        {
            GUILayout.Label(line);
        }
        GUILayout.EndScrollView();
    }

    public static void DebugClear()
    {
        debugLines.Clear();
    }

    public static void Debug(string s)
    {
        if (!bDebugEnable)
        {
            return;
        }
        if (debugLines.Count > debugLinesMax)
        {
            return;
        }
        debugLines.Add(s);
    }

    public static void Debug(string format, params object[] args)
    {
        if (!bDebugEnable)
        {
            return;
        }
        Debug(string.Format(format, args));
    }

    public static float NormalizeAngle(float angle)
    {
        if (angle >= 180.0f)
        {
            angle -= 360.0f;
        }
        else if (angle < -180.0f)
        {
            angle += 360.0f;
        }
        return angle;
    }

    public static Vector3 NormalizeEulerAngles(Vector3 eulerAngles)
    {
        return new(
            NormalizeAngle(eulerAngles.x),
            NormalizeAngle(eulerAngles.y),
            NormalizeAngle(eulerAngles.z));
    }

    public static T GetOrAddComponent<T>(GameObject gameObject) where T : Component
    {
        T t = null;
        if (gameObject != null)
        {
            t = gameObject.GetComponent<T>();
            if (t == null)
            {
                t = gameObject.AddComponent<T>();
            }
        }
        return t;
    }
}
