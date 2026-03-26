using System.Linq;

internal static class PluginHelper {
	// AudioSourceMgrを手がかりに、Maidを得る
	public static bool TryGetMaid(AudioSourceMgr audioSourceMgr, out Maid maid) {
		maid = null;
		if (audioSourceMgr == null) {
			return false;
		}
		maid = GetMaids().FirstOrDefault(e => e.AudioMan == audioSourceMgr);
		return maid;
	}

	// BoneMorph_を手がかりに、Maidを得る
	public static bool TryGetMaid(BoneMorph_ boneMorph_, out Maid maid) {
		maid = null;
		if (boneMorph_ == null) {
			return false;
		}
		maid = GetMaids().FirstOrDefault(e => e.body0?.bonemorph == boneMorph_);
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
}
