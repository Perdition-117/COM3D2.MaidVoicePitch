using System.Linq;
using UnityEngine;

internal static class PluginHelper {
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
