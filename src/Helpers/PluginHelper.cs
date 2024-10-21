using System.Linq;
using UnityEngine;

internal static class PluginHelper {
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

	public static Maid GetMaid(TBody tbody) {
		return tbody.maid;
	}

	// AudioSourceMgrを手がかりに、Maidを得る
	public static bool TryGetMaid(AudioSourceMgr audioSourceMgr, out Maid maid) {
		maid = null;
		if (audioSourceMgr == null) {
			return false;
		}
		maid = GetMaids().FirstOrDefault(e => e.AudioMan != null && e.AudioMan == audioSourceMgr);
		return maid;
	}

	// BoneMorph_を手がかりに、Maidを得る
	public static bool TryGetMaid(BoneMorph_ boneMorph_, out Maid maid) {
		maid = null;
		if (boneMorph_ == null) {
			return false;
		}
		maid = GetMaids().FirstOrDefault(e => e.body0?.bonemorph != null && e.body0.bonemorph == boneMorph_);
		return maid;
	}

	public static IEnumerable<Maid> GetMaids() {
		var characterManager = GameMain.Instance.CharacterMgr;
		for (var i = 0; i < characterManager.GetStockMaidCount(); i++) {
			yield return characterManager.GetStockMaid(i);
		}
		foreach (var npcMaid in characterManager.m_listStockNpcMaid) {
			yield return npcMaid;
		}
	}

	public static void DebugGui() {
		if (bDebugEnable && debugLines != null && debugLines.Count > 0) {
			debugWindowRect = GUILayout.Window(windowId, debugWindowRect, DebugGuiWindow, "Debug");
		}
	}

	public static void DebugGuiWindow(int windowId) {
		debugScrollPosition = GUILayout.BeginScrollView(debugScrollPosition);
		foreach (var line in debugLines) {
			GUILayout.Label(line);
		}
		GUILayout.EndScrollView();
	}

	public static void DebugClear() {
		debugLines.Clear();
	}

	public static void Debug(string s) {
		if (!bDebugEnable) {
			return;
		}
		if (debugLines.Count > debugLinesMax) {
			return;
		}
		debugLines.Add(s);
	}

	public static void Debug(string format, params object[] args) {
		if (!bDebugEnable) {
			return;
		}
		Debug(string.Format(format, args));
	}

	public static float NormalizeAngle(float angle) {
		if (angle >= 180.0f) {
			angle -= 360.0f;
		} else if (angle < -180.0f) {
			angle += 360.0f;
		}
		return angle;
	}

	public static Vector3 NormalizeEulerAngles(Vector3 eulerAngles) {
		return new(
			NormalizeAngle(eulerAngles.x),
			NormalizeAngle(eulerAngles.y),
			NormalizeAngle(eulerAngles.z));
	}
}
