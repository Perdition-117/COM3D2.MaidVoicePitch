using System.Linq;
using UnityEngine;
using CM3D2.ExternalSaveData.Managed;
using HarmonyLib;
using System.Reflection.Emit;
using System.IO;

namespace CM3D2.MaidVoicePitch.Plugin;

public class DistortCorrect {
	private static readonly KeyValuePair<string, string>[] BoneAndPropNameList = {
		new("Bip01 ? Thigh_SCL_", "THISCL"),     // 下半身
		new("Bip01 ? Calf_SCL_",  "THISCL"),     // 下半身
		new("Bip01 ? Foot",       "THISCL"),     // 下半身
		new("momotwist_?",        "THISCL"),     // 下半身
		new("momotwist_?",        "MTWSCL"),     // ももツイスト
		new("momoniku_?",         "MMNSCL"),     // もも肉
		new("Bip01 Pelvis_SCL_",  "PELSCL"),     // 骨盤
		new("Hip_?",              "PELSCL"),     // 骨盤
		new("Hip_?",              "HIPSCL"),     // 骨盤
		new("Bip01 ? Thigh_SCL_", "THISCL2"),    // 膝
		new("Bip01 ? Calf_SCL_",  "CALFSCL"),    // 膝下
		new("Bip01 ? Foot",       "CALFSCL"),    // 膝下
		new("Bip01 ? Foot",       "FOOTSCL"),    // 足首より下
		new("Skirt",              "SKTSCL"),     // スカート
		new("Bip01 Spine_SCL_",   "SPISCL"),     // 胴(下腹部周辺)
		new("Bip01 Spine0a_SCL_", "S0ASCL"),     // 胴0a(腹部周辺)
		new("Bip01 Spine1_SCL_",  "S1_SCL"),     // 胴1_(みぞおち周辺)
		new("Bip01 Spine1a_SCL_", "S1ASCL"),     // 胴1a(首・肋骨周辺)
		new("Bip01 Spine1a",      "S1ABASESCL"), // 胴1a(胸より上)※頭に影響有り

		new("Kata_?",             "KATASCL"),    // 肩
		new("Mune_?",             "MUNESCL"),    // 胸
		new("Mune_?_sub",         "MUNESUBSCL"), // 胸サブ
		new("Bip01 Neck_SCL_",    "NECKSCL"),    // 首
	};

	private static readonly HashSet<string> ScaleBoneHash = new() {
		"Bip01 L UpperArm",
		"Bip01 L Forearm",
		"Bip01 L Hand",
		"Bip01 L Finger0",
		"Bip01 L Finger01",
		"Bip01 L Finger02",
		"Bip01 L Finger1",
		"Bip01 L Finger11",
		"Bip01 L Finger12",
		"Bip01 L Finger2",
		"Bip01 L Finger21",
		"Bip01 L Finger22",
		"Bip01 L Finger3",
		"Bip01 L Finger31",
		"Bip01 L Finger32",
		"Bip01 L Finger4",
		"Bip01 L Finger41",
		"Bip01 L Finger42",
		"Bip01 L Calf",
		"Bip01 R UpperArm",
		"Bip01 R Forearm",
		"Bip01 R Hand",
		"Bip01 R Finger0",
		"Bip01 R Finger01",
		"Bip01 R Finger02",
		"Bip01 R Finger1",
		"Bip01 R Finger11",
		"Bip01 R Finger12",
		"Bip01 R Finger2",
		"Bip01 R Finger21",
		"Bip01 R Finger22",
		"Bip01 R Finger3",
		"Bip01 R Finger31",
		"Bip01 R Finger32",
		"Bip01 R Finger4",
		"Bip01 R Finger41",
		"Bip01 R Finger42",
		"Bip01 R Calf",
	};

	private static readonly Dictionary<Maid, bool> LimbFixes = new();

	private static Dictionary<string, Vector3> OriginalBones;
	private static Dictionary<string, List<BoneMorph.BoneProp>> OriginalBones2;
	private static Dictionary<string, Vector3> NewBones;
	private static Dictionary<string, List<BoneMorph.BoneProp>> NewBones2;

	[HarmonyPrefix]
	[HarmonyPatch(typeof(TBody), nameof(TBody.AddItem), typeof(MPN), typeof(string), typeof(string), typeof(string), typeof(string), typeof(bool), typeof(int))]
	[HarmonyPatch(typeof(TBody), nameof(TBody.DelItem))]
	private static void ResetBones(TBody __instance) {
		ResetBoneDic(__instance.maid, true);
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(BoneMorph_), nameof(BoneMorph_.Blend))]
	private static void PreBlend(BoneMorph_ __instance) {
		PluginHelper.TryGetMaid(__instance, out var maid);
		ResetBoneDic(maid, false);
	}

	[HarmonyTranspiler]
	[HarmonyPatch(typeof(ImportCM), nameof(ImportCM.LoadSkinMesh_R))]
	private static IEnumerable<CodeInstruction> HookLoadMesh(IEnumerable<CodeInstruction> instructions) {
		return CreateTranspiler(instructions, 15);
	}

	[HarmonyTranspiler]
	[HarmonyPatch(typeof(ImportCM), nameof(ImportCM.LoadOnlyBone_R))]
	private static IEnumerable<CodeInstruction> HookLoadBone(IEnumerable<CodeInstruction> instructions) {
		return CreateTranspiler(instructions, 10);
	}

	private static IEnumerable<CodeInstruction> CreateTranspiler(IEnumerable<CodeInstruction> instructions, int i) {
		var codeMatcher = new CodeMatcher(instructions);

		codeMatcher
			.MatchStartForward(new CodeMatch(OpCodes.Ldarg_0))
			.MatchStartForward(new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(BinaryReader), nameof(BinaryReader.ReadByte))))
			.MatchStartForward(new CodeMatch(OpCodes.Ldloc_S))
			.MatchStartForward(new CodeMatch(OpCodes.Brfalse))
			.InsertAndAdvance(
				new CodeInstruction(OpCodes.Ldloc, i),
				new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(DistortCorrect), nameof(JudgeSclBone)))
			);

		return codeMatcher.InstructionEnumeration();
	}

	private static bool JudgeSclBone(bool flag, GameObject bone) => flag || ScaleBoneHash.Contains(bone.name);

	private static void SetBoneMorphScale(string tag, string boneName, float x = 1, float y = 1, float z = 1, float x2 = 1, float y2 = 1, float z2 = 1) {
		BoneMorph.SetScale(tag, boneName, x, y, z, x2, y2, z2);
	}

	private static void ResetBoneDic(Maid maid, bool staticFlag) {
		if (OriginalBones == null) {
			InitBoneDic();
		}

		if (maid == null || maid.IsCrcBody) {
			return;
		}

		var wideSlider = ExSaveData.GetBool(maid, "CM3D2.MaidVoicePitch", "WIDESLIDER", false);
		var limbFix = ExSaveData.GetBool(maid, "CM3D2.MaidVoicePitch", "LIMBSFIX", false);
		var enable = wideSlider && limbFix;
		if (staticFlag || !LimbFixes.ContainsKey(maid) || (LimbFixes[maid] != enable)) {
			if (enable) {
				BoneMorph.dic = NewBones;
				BoneMorph.dic2 = NewBones2;
			} else {
				BoneMorph.dic = OriginalBones;
				BoneMorph.dic2 = OriginalBones2;
			}
			maid.body0.bonemorph.Init();
			maid.body0.bonemorph.AddRoot(maid.body0.m_Bones.transform);
			LimbFixes[maid] = enable;
		}
	}

	private static void InitBoneDic() {
		OriginalBones = BoneMorph.dic;
		OriginalBones2 = BoneMorph.dic2;

		BoneMorph.dic = new();
		BoneMorph.dic2 = new();

		BoneMorph.SetPosition("KubiScl", "Bip01 Neck", 0.95f, 1f, 1f, 1.05f, 1f, 1f);
		BoneMorph.SetPosition("KubiScl", "Bip01 Head", 0.8f, 1f, 1f, 1.2f, 1f, 1f);

		SetUdeScale("UdeScl", 0.85f, 1f, 1f, 1.15f, 1f, 1f);

		SetBoneMorphScale("EyeSclX", "Eyepos_L", 1f, 1f, 0.92f, 1f, 1f, 1.08f);
		SetBoneMorphScale("EyeSclX", "Eyepos_R", 1f, 1f, 0.92f, 1f, 1f, 1.08f);
		SetBoneMorphScale("EyeSclY", "Eyepos_L", 1f, 0.92f, 1f, 1f, 1.08f, 1f);
		SetBoneMorphScale("EyeSclY", "Eyepos_R", 1f, 0.92f, 1f, 1f, 1.08f, 1f);
		BoneMorph.SetPosition("EyePosX", "Eyepos_R", 1f, 1f, 0.9f, 1f, 1f, 1.1f);
		BoneMorph.SetPosition("EyePosX", "Eyepos_L", 1f, 1f, 0.9f, 1f, 1f, 1.1f);
		BoneMorph.SetPosition("EyePosY", "Eyepos_R", 1f, 0.93f, 1f, 1f, 1.07f, 1f);
		BoneMorph.SetPosition("EyePosY", "Eyepos_L", 1f, 0.93f, 1f, 1f, 1.07f, 1f);
		SetBoneMorphScale("HeadX", "Bip01 Head", 1f, 0.9f, 0.8f, 1f, 1.1f, 1.2f);
		SetBoneMorphScale("HeadY", "Bip01 Head", 0.8f, 0.9f, 1f, 1.2f, 1.1f, 1f);

		BoneMorph.SetPosition("DouPer", "Bip01 Spine", 1f, 1f, 0.94f, 1f, 1f, 1.06f);
		BoneMorph.SetPosition("DouPer", "Bip01 Spine0a", 0.88f, 1f, 1f, 1.12f, 1f, 1f);
		BoneMorph.SetPosition("DouPer", "Bip01 Spine1", 0.88f, 1f, 1f, 1.12f, 1f, 1f);
		BoneMorph.SetPosition("DouPer", "Bip01 Spine1a", 0.88f, 1f, 1f, 1.12f, 1f, 1f);
		BoneMorph.SetPosition("DouPer", "Bip01 Neck", 1.03f, 1f, 1f, 0.97f, 1f, 1f);
		BoneMorph.SetPosition("DouPer", "Bip01 ? Calf", 0.87f, 1f, 1f, 1.13f, 1f, 1f);
		BoneMorph.SetPosition("DouPer", "Bip01 ? Foot", 0.87f, 1f, 1f, 1.13f, 1f, 1f);
		SetBoneMorphScale("DouPer", "Bip01 ? Thigh_SCL_", 0.87f, 1f, 1f, 1.13f, 1f, 1f);
		SetBoneMorphScale("DouPer", "momotwist_?", 0.87f, 1f, 1f, 1.13f, 1f, 1f);
		SetBoneMorphScale("DouPer", "Bip01 ? Calf_SCL_", 0.87f, 1f, 1f, 1.13f, 1f, 1f);

		SetUdeScale("DouPer", 0.98f, 1f, 1f, 1.02f, 1f, 1f);

		BoneMorph.SetPosition("sintyou", "Bip01 Spine", 1f, 1f, 0.85f, 1f, 1f, 1.15f);
		BoneMorph.SetPosition("sintyou", "Bip01 Spine0a", 0.88f, 1f, 1f, 1.12f, 1f, 1f);
		BoneMorph.SetPosition("sintyou", "Bip01 Spine1", 0.88f, 1f, 1f, 1.12f, 1f, 1f);
		BoneMorph.SetPosition("sintyou", "Bip01 Spine1a", 0.88f, 1f, 1f, 1.12f, 1f, 1f);
		BoneMorph.SetPosition("sintyou", "Bip01 Neck", 0.97f, 1f, 1f, 1.03f, 1f, 1f);
		BoneMorph.SetPosition("sintyou", "Bip01 Head", 0.9f, 1f, 1f, 1.1f, 1f, 1f);
		BoneMorph.SetPosition("sintyou", "Bip01 ? Calf", 0.87f, 1f, 1f, 1.13f, 1f, 1f);
		BoneMorph.SetPosition("sintyou", "Bip01 ? Foot", 0.87f, 1f, 1f, 1.13f, 1f, 1f);
		SetBoneMorphScale("sintyou", "Bip01 ? Thigh_SCL_", 0.87f, 1f, 1f, 1.13f, 1f, 1f);
		SetBoneMorphScale("sintyou", "momotwist_?", 0.87f, 1f, 1f, 1.13f, 1f, 1f);
		SetBoneMorphScale("sintyou", "Bip01 ? Calf_SCL_", 0.87f, 1f, 1f, 1.13f, 1f, 1f);

		SetUdeScale("sintyou", 0.9f, 1f, 1f, 1.1f, 1f, 1f);

		SetBoneMorphScale("sintyou", "Bip01 ? Thigh");
		SetBoneMorphScale("sintyou", "momoniku_?");
		SetBoneMorphScale("sintyou", "Bip01 Pelvis_SCL_");
		SetBoneMorphScale("sintyou", "Bip01 ? Calf");
		SetBoneMorphScale("sintyou", "Bip01 ? Foot");
		SetBoneMorphScale("sintyou", "Skirt");
		SetBoneMorphScale("sintyou", "Bip01 Spine_SCL_");
		SetBoneMorphScale("sintyou", "Bip01 Spine0a_SCL_");
		SetBoneMorphScale("sintyou", "Bip01 Spine1_SCL_");
		SetBoneMorphScale("sintyou", "Bip01 Spine1a_SCL_");
		SetBoneMorphScale("sintyou", "Bip01 Spine1a");
		SetBoneMorphScale("sintyou", "Bip01 ? Clavicle");
		SetBoneMorphScale("sintyou", "Bip01 ? Clavicle_SCL_");
		SetBoneMorphScale("sintyou", "Bip01 ? UpperArm");
		SetBoneMorphScale("sintyou", "Bip01 ? Forearm");
		SetBoneMorphScale("sintyou", "Bip01 ? Hand");
		SetBoneMorphScale("sintyou", "Kata_?");
		SetBoneMorphScale("sintyou", "Mune_?");
		SetBoneMorphScale("sintyou", "Mune_?_sub");
		SetBoneMorphScale("sintyou", "Bip01 Neck_SCL_");

		SetBoneMorphScale("koshi", "Bip01 Pelvis_SCL_", 1f, 0.8f, 0.92f, 1f, 1.2f, 1.08f);
		SetBoneMorphScale("koshi", "Bip01 Spine_SCL_", 1f, 1f, 1f, 1f, 1f, 1f);
		SetBoneMorphScale("koshi", "Hip_?", 1f, 0.96f, 0.9f, 1f, 1.04f, 1.1f);
		SetBoneMorphScale("koshi", "Skirt", 1f, 0.85f, 0.88f, 1f, 1.2f, 1.12f);
		BoneMorph.SetPosition("kata", "Bip01 ? Clavicle", 0.98f, 1f, 0.5f, 1.02f, 1f, 1.5f);
		SetBoneMorphScale("kata", "Bip01 Spine1a_SCL_", 1f, 1f, 0.95f, 1f, 1f, 1.05f);
		SetBoneMorphScale("west", "Bip01 Spine_SCL_", 1f, 0.95f, 0.9f, 1f, 1.05f, 1.1f);
		SetBoneMorphScale("west", "Bip01 Spine0a_SCL_", 1f, 0.85f, 0.7f, 1f, 1.15f, 1.3f);
		SetBoneMorphScale("west", "Bip01 Spine1_SCL_", 1f, 0.9f, 0.85f, 1f, 1.1f, 1.15f);
		SetBoneMorphScale("west", "Bip01 Spine1a_SCL_", 1f, 0.95f, 0.95f, 1f, 1.05f, 1.05f);
		SetBoneMorphScale("west", "Skirt", 1f, 0.92f, 0.88f, 1f, 1.08f, 1.12f);

		NewBones = BoneMorph.dic;
		NewBones2 = BoneMorph.dic2;

		foreach (var str in NewBones.Keys.Where(e => !OriginalBones.ContainsKey(e))) {
			OriginalBones.Add(str, Vector3.one);
		}

		foreach (var kvp in NewBones2) {
			if (!OriginalBones2.ContainsKey(kvp.Key)) {
				OriginalBones2.Add(kvp.Key, new());
			}
			var propList = OriginalBones2[kvp.Key];
			foreach (var prop in kvp.Value) {
				if (!propList.Exists(e => e.strProp == prop.strProp)) {
					propList.Add(new() {
						strProp = prop.strProp,
						nIndex = prop.nIndex,
						bExistP = prop.bExistP,
						bExistM = prop.bExistM,
						vMinP = Vector3.one,
						vMaxP = Vector3.one,
						vMinM = Vector3.one,
						vMaxM = Vector3.one,
					});
				}
			}
		}
	}

	private static void SetUdeScale(string tag, float x, float y, float z, float x2, float y2, float z2) {
		void SetScale(string boneName) => BoneMorph.SetScale(tag, boneName, x, y, z, x2, y2, z2);
		void SetPosition(string boneName) => BoneMorph.SetPosition(tag, boneName, x, y, z, x2, y2, z2);
		SetScale("Bip01 ? UpperArm_SCL_");
		SetScale("Uppertwist_?");
		SetScale("Uppertwist1_?");
		SetPosition("Uppertwist1_?");
		SetPosition("Bip01 ? Forearm");
		SetScale("Bip01 ? Forearm_SCL_");
		SetScale("Foretwist_?");
		SetScale("Foretwist1_?");
		SetPosition("Foretwist_?");
		SetPosition("Bip01 ? Hand");
		SetScale("Bip01 ? Hand_SCL_");
		SetPosition("Bip01 ? Finger0");
		SetPosition("Bip01 ? Finger1");
		SetPosition("Bip01 ? Finger2");
		SetPosition("Bip01 ? Finger3");
		SetPosition("Bip01 ? Finger4");
		SetScale("Bip01 ? Finger0_SCL_");
		SetPosition("Bip01 ? Finger01");
		SetScale("Bip01 ? Finger01_SCL_");
		SetPosition("Bip01 ? Finger02");
		SetScale("Bip01 ? Finger02_SCL_");
		SetScale("Bip01 ? Finger1_SCL_");
		SetPosition("Bip01 ? Finger11");
		SetScale("Bip01 ? Finger11_SCL_");
		SetPosition("Bip01 ? Finger12");
		SetScale("Bip01 ? Finger12_SCL_");
		SetScale("Bip01 ? Finger2_SCL_");
		SetPosition("Bip01 ? Finger21");
		SetScale("Bip01 ? Finger21_SCL_");
		SetPosition("Bip01 ? Finger22");
		SetScale("Bip01 ? Finger22_SCL_");
		SetScale("Bip01 ? Finger3_SCL_");
		SetPosition("Bip01 ? Finger31");
		SetScale("Bip01 ? Finger31_SCL_");
		SetPosition("Bip01 ? Finger32");
		SetScale("Bip01 ? Finger32_SCL_");
		SetScale("Bip01 ? Finger4_SCL_");
		SetPosition("Bip01 ? Finger41");
		SetScale("Bip01 ? Finger41_SCL_");
		SetPosition("Bip01 ? Finger42");
		SetScale("Bip01 ? Finger42_SCL_");
	}

	internal static Dictionary<string, Vector3> GetBoneScales(Maid maid) {
		var boneScales = new Dictionary<string, Vector3>();
		SetBoneScales(boneScales, maid, "CLVSCL", BodyBone.ClavicleScales);
		SetBoneScales(boneScales, maid, "UPARMSCL", BodyBone.UpperArmScales);
		SetBoneScales(boneScales, maid, "FARMSCL", BodyBone.ForeArmScales);
		SetBoneScales(boneScales, maid, "HANDSCL", BodyBone.HandScales);
		SetBoneScales(boneScales, maid, BoneAndPropNameList);
		return boneScales;
	}

	internal static Dictionary<string, Vector3> GetBonePositionRates(Maid maid) {
		var bonePositionRates = new Dictionary<string, Vector3>();
		SetBoneScales(bonePositionRates, maid, "CLVSCL", BodyBone.ClaviclePositions);
		SetBoneScales(bonePositionRates, maid, "UPARMSCL", BodyBone.UpperArmPositions);
		SetBoneScales(bonePositionRates, maid, "FARMSCL", BodyBone.ForeArmPositions);
		SetBoneScales(bonePositionRates, maid, "HANDSCL", BodyBone.HandPositions);
		return bonePositionRates;
	}

	private static void SetBoneScales(Dictionary<string, Vector3> boneScales, Maid maid, KeyValuePair<string, string>[] boneAndPropNameList) {
		foreach (var item in boneAndPropNameList) {
			SetBoneScale(boneScales, item.Key, maid, item.Value);
		}
	}

	private static void SetBoneScales(Dictionary<string, Vector3> boneScales, Maid maid, string tag, List<string> bones) {
		foreach (var boneName in bones) {
			SetBoneScale(boneScales, boneName, maid, tag);
		}
	}

	private static void SetBoneScale(Dictionary<string, Vector3> boneScales, string boneName, Maid maid, string propName) {
		if (boneName.Contains("?")) {
			var boneNameL = boneName.Replace('?', 'L');
			var boneNameR = boneName.Replace('?', 'R');
			boneScales[boneNameL] = GetBoneScale(boneNameL);
			boneScales[boneNameR] = boneScales[boneNameL];
		} else {
			boneScales[boneName] = GetBoneScale(boneName);
		}

		Vector3 GetBoneScale(string boneName) {
			var boneScale = MaidVoicePitch.GetBoneScale(maid, propName);
			var baseScale = boneScales.TryGetValue(boneName, out var scale) ? scale : Vector3.one;
			return Vector3.Scale(baseScale, boneScale);
		}
	}
}
