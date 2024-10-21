namespace CM3D2.MaidVoicePitch.Plugin;

internal static class BodyBone {
	public static readonly List<string> HandScales = new() {
		"Bip01 ? Hand_SCL_",
		"Bip01 ? Finger0_SCL_",
		"Bip01 ? Finger01_SCL_",
		"Bip01 ? Finger02_SCL_",
		"Bip01 ? Finger1_SCL_",
		"Bip01 ? Finger11_SCL_",
		"Bip01 ? Finger12_SCL_",
		"Bip01 ? Finger2_SCL_",
		"Bip01 ? Finger21_SCL_",
		"Bip01 ? Finger22_SCL_",
		"Bip01 ? Finger3_SCL_",
		"Bip01 ? Finger31_SCL_",
		"Bip01 ? Finger32_SCL_",
		"Bip01 ? Finger4_SCL_",
		"Bip01 ? Finger41_SCL_",
		"Bip01 ? Finger42_SCL_"
	};

	public static readonly List<string> HandPositions = new() {
		"Bip01 ? Finger0",
		"Bip01 ? Finger01",
		"Bip01 ? Finger02",
		"Bip01 ? Finger1",
		"Bip01 ? Finger11",
		"Bip01 ? Finger12",
		"Bip01 ? Finger2",
		"Bip01 ? Finger21",
		"Bip01 ? Finger22",
		"Bip01 ? Finger3",
		"Bip01 ? Finger31",
		"Bip01 ? Finger32",
		"Bip01 ? Finger4",
		"Bip01 ? Finger41",
		"Bip01 ? Finger42"
	};

	public static readonly List<string> ForeArmScales = new() {
		"Bip01 ? Forearm_SCL_",
		"Foretwist_?",
		"Foretwist1_?"
	};

	public static readonly List<string> ForeArmPositions = new() {
		"Foretwist_?",
		"Foretwist1_?",
		"Bip01 ? Hand"
	};

	public static readonly List<string> UpperArmScales = new() {
		"Bip01 ? UpperArm_SCL_",
		"Uppertwist_?",
		"Uppertwist1_?"
	};

	public static readonly List<string> UpperArmPositions = new() {
		"Uppertwist_?",
		"Uppertwist1_?",
		"Bip01 ? Forearm"
	};

	public static readonly List<string> ClavicleScales = new() {
		"Bip01 ? Clavicle_SCL_",
		"Kata_?"
	};

	public static readonly List<string> ClaviclePositions = new() {
		"Bip01 ? UpperArm",
		"Kata_?"
	};

	static BodyBone() {
		ForeArmScales.AddRange(HandScales);
		UpperArmScales.AddRange(ForeArmScales);
		ClavicleScales.AddRange(UpperArmScales);

		ForeArmPositions.AddRange(HandPositions);
		UpperArmPositions.AddRange(ForeArmPositions);
		ClaviclePositions.AddRange(UpperArmPositions);
	}
}
