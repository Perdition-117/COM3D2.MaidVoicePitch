using CM3D2.ExternalSaveData.Managed;
using UnityEngine;

namespace CM3D2.MaidVoicePitch.Plugin;

class MaidForearms {
	public Transform LeftForeArm { get; set; }
	public Transform RightForeArm { get; set; }
	public Transform UpperArm { get; set; }
}

//The original Farmfix gave dry turds a run for their money with some of the largest GC Allocs in meido causing massive stutter. This resolves the situation completely, making FarmFix 0 alloc.
internal static class ForeArmFixOptimized {
	private static readonly Dictionary<string, MaidForearms> MaidBones = new();

	// ForeArmFix : 前腕の歪みを修正する CM3D.MaidVoicePitch.Plugin.cs の追加メソッド
	// CM3D.MaidVoicePitch.Plugin を適用しメイドのフリーコメント欄に #FARMFIX# の記述で前腕の歪みを修正する。
	// 前腕歪みバグを修正
	internal static void ForeArmFix(Maid maid) {
		if (!ExSaveData.GetBool(maid, MaidVoicePitch.PluginName, "FARMFIX", false) || maid.IsCrcBody) {
			return;
		}

		if (!(MaidBones.TryGetValue(maid.status.guid, out var MaidBone) || MaidBone is { LeftForeArm: { }, RightForeArm: { }, UpperArm: { } })) {
			Transform foreArmL = null;
			Transform foreArmR = null;
			Transform upperArm = null;

			foreach (var bone in maid.body0.bonemorph.bones) {
				if (bone.linkT == null) continue;
				if (bone.linkT.name == "Bip01 L Forearm") foreArmL = bone.linkT;
				if (bone.linkT.name == "Bip01 R Forearm") foreArmR = bone.linkT;
				if (bone.linkT.name == "Bip01 L UpperArm") upperArm = bone.linkT;

				if (foreArmL != null && foreArmR != null && upperArm != null) {
					break;
				}
			}

			if (foreArmL != null && foreArmR != null && upperArm != null) {
				MaidBones[maid.status.guid] = new() {
					LeftForeArm = foreArmL,
					RightForeArm = foreArmR,
					UpperArm = upperArm,
				};
			} else {
				return;
			}
		}

		var package = MaidBones[maid.status.guid];
		var scaleUpperArmX = package.UpperArm.localScale.x;

		var scaleUpperArm = new Vector3(scaleUpperArmX, 1f, 1f);

		var antiScaleUpperArm_d = new Vector3(1f / scaleUpperArmX - 1f, 0f, 0f);

		var eaForeArmL = package.LeftForeArm.localRotation.eulerAngles;
		var eaForeArmR = package.RightForeArm.localRotation.eulerAngles;

		var antirotForeArmL = Quaternion.Euler(eaForeArmL - new Vector3(180f, 180f, 180f));
		var antirotForeArmR = Quaternion.Euler(eaForeArmR - new Vector3(180f, 180f, 180f));
		var scaleForeArmL_d = antirotForeArmL * antiScaleUpperArm_d;
		var scaleForeArmR_d = antirotForeArmR * antiScaleUpperArm_d;

		var antiScaleForeArmL = new Vector3(1f, 1f, 1f) + new Vector3(Mathf.Abs(scaleForeArmL_d.x), Mathf.Abs(scaleForeArmL_d.y), Mathf.Abs(scaleForeArmL_d.z));
		var antiScaleForeArmR = new Vector3(1f, 1f, 1f) + new Vector3(Mathf.Abs(scaleForeArmR_d.x), Mathf.Abs(scaleForeArmR_d.y), Mathf.Abs(scaleForeArmR_d.z));

		//foreach (Transform t in tListFAL) t.localScale = Vector3.Scale(antisclFAL, sclUA);

		package.LeftForeArm.localScale = Vector3.Scale(antiScaleForeArmL, scaleUpperArm);

		//foreach (Transform t in tListFAR) t.localScale = Vector3.Scale(antisclFAR, sclUA);
		package.RightForeArm.localScale = Vector3.Scale(antiScaleForeArmR, scaleUpperArm);
	}
}
