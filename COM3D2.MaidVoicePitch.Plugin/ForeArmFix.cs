using CM3D2.ExternalSaveData.Managed;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityInjector;

namespace CM3D2.MaidVoicePitch.Plugin
{
	class MaidForearms
	{
		public Transform LeftForeArm;
		public Transform RightForeArm;
		public Transform UpperArm;
	}
	//The original Farmfix gave dry turds a run for their money with some of the largest GC Allocs in meido causing massive stutter. This resolves the situation completely, making FarmFix 0 alloc.
	internal static class ForeArmFixOptimized
	{
		static Dictionary<string, MaidForearms> maidBones = new Dictionary<string, MaidForearms>();

		// ForeArmFix : 前腕の歪みを修正する CM3D.MaidVoicePitch.Plugin.cs の追加メソッド
		// CM3D.MaidVoicePitch.Plugin を適用しメイドのフリーコメント欄に #FARMFIX# の記述で前腕の歪みを修正する。
		// 前腕歪みバグを修正
		internal static void ForeArmFix(Maid maid)
		{
			if (!ExSaveData.GetBool(maid, MaidVoicePitch.PluginName, "FARMFIX", false))
			{
				return;
			}

			BoneMorph_ bm_ = maid.body0.bonemorph;

			if (!maidBones.ContainsKey(maid.status.guid) || maidBones[maid.status.guid].LeftForeArm == null || maidBones[maid.status.guid].RightForeArm == null || maidBones[maid.status.guid].UpperArm == null) 
			{
				int i = 0;

				Transform LFore = null;
				Transform RFore = null;
				Transform UppArm = null;

				for (i = 0; i < bm_.bones.Count; i++)
				{
					if (bm_.bones[i].linkT == null) continue;
					if (bm_.bones[i].linkT.name == "Bip01 L Forearm") LFore = bm_.bones[i].linkT;
					if (bm_.bones[i].linkT.name == "Bip01 R Forearm") RFore = bm_.bones[i].linkT;
					if (bm_.bones[i].linkT.name == "Bip01 L UpperArm") UppArm = bm_.bones[i].linkT;

					if (LFore != null && RFore != null && UppArm != null) 
					{
						break;
					}
				}

				if (LFore != null && RFore != null && UppArm != null)
				{
					maidBones[maid.status.guid] = new MaidForearms
					{
						LeftForeArm = LFore,
						RightForeArm = RFore,
						UpperArm = UppArm
					};
				}
				else 
				{
					return;
				}
			}

			var Package = maidBones[maid.status.guid];
			var sclUAx = Package.UpperArm.localScale.x;

			Vector3 sclUA = new Vector3(sclUAx, 1f, 1f);

			Vector3 antisclUA_d = new Vector3(1f / sclUAx - 1f, 0f, 0f);

			Vector3 eaFAL = Package.LeftForeArm.localRotation.eulerAngles;
			Vector3 eaFAR = Package.RightForeArm.localRotation.eulerAngles;

			Quaternion antirotFAL = Quaternion.Euler(eaFAL - new Vector3(180f, 180f, 180f));
			Quaternion antirotFAR = Quaternion.Euler(eaFAR - new Vector3(180f, 180f, 180f));
			Vector3 sclFAL_d = antirotFAL * antisclUA_d;
			Vector3 sclFAR_d = antirotFAR * antisclUA_d;

			Vector3 antisclFAL = new Vector3(1f, 1f, 1f) + new Vector3(Mathf.Abs(sclFAL_d.x), Mathf.Abs(sclFAL_d.y), Mathf.Abs(sclFAL_d.z));
			Vector3 antisclFAR = new Vector3(1f, 1f, 1f) + new Vector3(Mathf.Abs(sclFAR_d.x), Mathf.Abs(sclFAR_d.y), Mathf.Abs(sclFAR_d.z));

			//foreach (Transform t in tListFAL) t.localScale = Vector3.Scale(antisclFAL, sclUA);

			Package.LeftForeArm.localScale = Vector3.Scale(antisclFAL, sclUA);

			//foreach (Transform t in tListFAR) t.localScale = Vector3.Scale(antisclFAR, sclUA);
			Package.RightForeArm.localScale = Vector3.Scale(antisclFAR, sclUA);
		}
	}
}
