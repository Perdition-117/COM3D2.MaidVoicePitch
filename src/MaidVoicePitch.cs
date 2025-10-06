using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using CM3D2.ExternalPreset.Managed;
using CM3D2.ExternalSaveData.Managed;
using ExtensionMethods;
using HarmonyLib;
using MaidVoicePitch;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CM3D2.MaidVoicePitch.Plugin;

[BepInPlugin("COM3D2.MaidVoicePitch", MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency("COM3D2.ExternalSaveData")]
[BepInDependency("COM3D2.ExternalPresetData")]
public class MaidVoicePitch : BaseUnityPlugin {
	public static string PluginName => "CM3D2.MaidVoicePitch";
	internal static readonly string PluginPath = Path.Combine(Paths.PluginPath, "ModSliders");

	internal const string DefaultTemplateFile = "MaidVoicePitchSlider.xml";

	private const float SliderScale = 20f;

	internal static readonly bool IsCom3d25 = new Version(GameUty.GetBuildVersionText()).Major == 3;
	internal static readonly PluginSaveData PluginSaveData = new(PluginName);

	private static readonly TBodyMoveHeadAndEye TBodyMoveHeadAndEye = new();
	private static readonly Dictionary<jiggleBone, Maid> JiggleBones = new();

	private static ManualLogSource _logger;
	private static bool _deserialized = false;
	private static Vector3 _skirtScaleBackUp;
	private static Vector3 _jiggleBoneScaleBackUp;

	private static PropertyInfo _bodyIk;
	private static FieldInfo _bodyIkMouth;
	private static FieldInfo _bodyIkNippleLeft;
	private static FieldInfo _bodyIkNippleRight;
	private static MethodInfo _bodyIkInit;

	private static ConfigEntry<bool> _configAllowEditModeFaceActions;

	/// <summary>
	/// Transform変形を行うボーンのリスト。
	/// ここに書いておくと自動でBoneMorphに登録されTransform処理されます。
	/// string[]の内容は{"ボーン名", "ExSaveのプロパティ名"}
	/// ボーン名に?が含まれるとLとRに置換されます。
	/// 頭に影響が行くボーンを登録する場合は
	/// WIDESLIDER() 内の ignoreHeadBones にボーン名を書くこと。
	/// </summary>
	private static readonly Dictionary<string, string> BoneAndPropNameList = new() {
		["Bip01 ? Thigh"]      = "THISCL",     // 下半身
		["momotwist_?"]        = "MTWSCL",     // ももツイスト
		["momoniku_?"]         = "MMNSCL",     // もも肉
		["Bip01 Pelvis_SCL_"]  = "PELSCL",     // 骨盤
		["Bip01 ? Thigh_SCL_"] = "THISCL2",    // 膝
		["Bip01 ? Calf"]       = "CALFSCL",    // 膝下
		["Bip01 ? Foot"]       = "FOOTSCL",    // 足首より下
		["Skirt"]              = "SKTSCL",     // スカート
		["Bip01 Spine_SCL_"]   = "SPISCL",     // 胴(下腹部周辺)
		["Bip01 Spine0a_SCL_"] = "S0ASCL",     // 胴0a(腹部周辺)
		["Bip01 Spine1_SCL_"]  = "S1_SCL",     // 胴1_(みぞおち周辺)
		["Bip01 Spine1a_SCL_"] = "S1ASCL",     // 胴1a(首・肋骨周辺)
		["Bip01 Spine1a"]      = "S1ABASESCL", // 胴1a(胸より上)※頭に影響有り
		["Kata_?"]             = "KATASCL",    // 肩
		["Bip01 ? UpperArm"]   = "UPARMSCL",   // 上腕
		["Bip01 ? Forearm"]    = "FARMSCL",    // 前腕
		["Bip01 ? Hand"]       = "HANDSCL",    // 手
		["Bip01 ? Clavicle"]   = "CLVSCL",     // 鎖骨
		["Mune_?"]             = "MUNESCL",    // 胸
		["Mune_?_sub"]         = "MUNESUBSCL", // 胸サブ
		["Bip01 Neck_SCL_"]    = "NECKSCL",    // 首
	};

	//この配列に記載があるボーンは頭に影響を与えずにTransformを反映させる。
	//ただしボディに繋がっている中のアレは影響を受ける。
	private static readonly string[] IgnoreHeadBones = {
		"Bip01 Spine1a",
	};

	private static readonly string[] ObsoleteSettings = {
		"WIDESLIDER",
		"WIDESLIDER.enable",
		"PROPSET_OFF.enable",
		"LIPSYNC_OFF.enable",

		"HYOUJOU_OFF.enable",
		"EYETOCAMERA_OFF.enable",
		"MUHYOU.enable",

		"FARMFIX.enable",
		"EYEBALL.enable",
		"EYEBALL.width",
		"EYEBALL.height",
		"EYE_ANG.enable",

		"PELSCL.enable",
		"SKTSCL.enable",
		"THISCL.enable",
		"THIPOS.enable",

		"PELVIS.enable",
		"FARMFIX.enable",
		"SPISCL.enable",

		"S0ASCL.enable",
		"S1_SCL.enable",
		"S1ASCL.enable",

		"FACE_OFF.enable",
		"FACEBLEND_OFF.enable",

		// 以下0.2.4で廃止
		"FACE_ANIME_SPEED",
		"MABATAKI_SPEED",

		"PELVIS",
		"PELVIS.x",
		"PELVIS.y",
		"PELVIS.z",
	};

	private static readonly string[] ObsoleteGlobalSettings = {
		"TEST_GLOBAL_KEY"
	};

	public void Awake() {
		DontDestroyOnLoad(this);

		SceneManager.sceneLoaded += OnSceneLoaded;

		_logger = Logger;

		_configAllowEditModeFaceActions = Config.Bind("General", "AllowEditModeFaceActions", false, "Allows face actions while in edit mode, even while otherwise disabled.");

		_configAllowEditModeFaceActions.SettingChanged += (o, e) => {
			if (_configAllowEditModeFaceActions.Value) {
				if (SceneManager.GetActiveScene().name == "SceneEdit" && GameMain.Instance?.CharacterMgr != null) {
					foreach (var maid in PluginHelper.GetMaids()) {
						maid.m_bFoceKuchipakuSelfUpdateTime = false;
					}
				}
			}
		};

		// ExPresetに外部から登録
		ExPreset.AddExSaveNode(PluginName);
		ExPreset.loadNotify.AddListener(MaidVoicePitch_UpdateSliders);

		Harmony.CreateAndPatchAll(typeof(Managed.Callbacks.TBody.LateUpdate));
		Harmony.CreateAndPatchAll(typeof(Managed.Callbacks.TBody.MoveHeadAndEye));
		Harmony.CreateAndPatchAll(typeof(Managed.Callbacks.BoneMorph_.Blend));
		Harmony.CreateAndPatchAll(typeof(Managed.Callbacks.AudioSourceMgr.Play));
		Harmony.CreateAndPatchAll(typeof(Managed.Callbacks.AudioSourceMgr.PlayOneShot));
		Harmony.CreateAndPatchAll(typeof(Managed.Callbacks.jiggleBone.PreLateUpdateSelf));
		Harmony.CreateAndPatchAll(typeof(Managed.Callbacks.jiggleBone.PostLateUpdateSelf));

		var harmony1 = Harmony.CreateAndPatchAll(typeof(MaidVoicePitch));
		var harmony2 = Harmony.CreateAndPatchAll(typeof(DistortCorrect));

		Type tbodyType = typeof(TBody);
		Type tbodyIkType;
		MethodInfo presetSetMethod;
		MethodInfo skirtBoneUpdateMethod;
		MethodInfo addItemMethod;

		if (IsCom3d25) {
			tbodyIkType = tbodyType.Assembly.GetType("FullBodyIKMgr");
			_bodyIk = tbodyType.GetProperty("fullBodyIK");
			_bodyIkMouth = GetField("Mouth");
			_bodyIkNippleLeft = GetField("NippleL");
			_bodyIkNippleRight = GetField("NippleR");

			Type tbodySkinType = typeof(TBodySkin);
			presetSetMethod = AccessTools.Method(typeof(CharacterMgr), nameof(CharacterMgr.PresetSet), new[] { typeof(Maid), typeof(CharacterMgr.Preset), typeof(bool) });
			skirtBoneUpdateMethod = AccessTools.Method(typeof(DynamicSkirtBone), "DynamicUpdate");
			addItemMethod = AccessTools.Method(tbodyType, nameof(TBody.AddItem), new[] {
				typeof(MPN),
				typeof(int),
				typeof(string),
				typeof(string),
				typeof(string),
				typeof(string),
				typeof(bool),
				typeof(int),
				typeof(bool),
				tbodySkinType.GetNestedType("SplitParam"),
				tbodySkinType.GetNestedType("TargetBodyType"),
				typeof(int),
			});
		} else {
			tbodyIkType = tbodyType.Assembly.GetType("FullBodyIKCtrl");
			_bodyIk = tbodyType.GetProperty("IKCtrl");
			_bodyIkMouth = GetField("m_Mouth");
			_bodyIkNippleLeft = GetField("m_NippleL");
			_bodyIkNippleRight = GetField("m_NippleR");

			presetSetMethod = AccessTools.Method(typeof(CharacterMgr), nameof(CharacterMgr.PresetSet), new[] { typeof(Maid), typeof(CharacterMgr.Preset) });
			skirtBoneUpdateMethod = AccessTools.Method(typeof(DynamicSkirtBone), "UpdateSelf");
			addItemMethod = AccessTools.Method(tbodyType, nameof(TBody.AddItem), new[] {
				typeof(MPN),
				typeof(string),
				typeof(string),
				typeof(string),
				typeof(string),
				typeof(bool),
				typeof(int),
			});
		}

		harmony1.Patch(presetSetMethod, new HarmonyMethod(typeof(Managed.Callbacks.CharacterMgr.PresetSet), nameof(Managed.Callbacks.CharacterMgr.PresetSet.Invoke)));
		harmony1.Patch(skirtBoneUpdateMethod,
			new HarmonyMethod(typeof(Managed.Callbacks.DynamicSkirtBone.PreUpdateSelf), nameof(Managed.Callbacks.DynamicSkirtBone.PreUpdateSelf.Invoke)),
			new HarmonyMethod(typeof(Managed.Callbacks.DynamicSkirtBone.PostUpdateSelf), nameof(Managed.Callbacks.DynamicSkirtBone.PostUpdateSelf.Invoke)));
		harmony2.Patch(addItemMethod, new HarmonyMethod(typeof(DistortCorrect), nameof(DistortCorrect.ResetBones)));

		_bodyIkInit = tbodyIkType.GetMethod("Init");

		FieldInfo GetField(string name) => tbodyIkType.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
	}

	internal static void LogDebug(object data) {
		_logger.LogDebug(data);
	}

	internal static void LogError(object data) {
		_logger.LogError(data);
	}

	private void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
		KagHooks.SetHook(PluginName, true);

		// TBody.MoveHeadAndEye 処理終了後のコールバック
		Managed.Callbacks.TBody.MoveHeadAndEye.Callbacks[PluginName] = TBody_MoveHeadAndEyeCallback;

		// BoneMorph_.Blend 処理終了後のコールバック
		Managed.Callbacks.BoneMorph_.Blend.Callbacks[PluginName] = BoneMorph_BlendCallback;

		// GameMain.Deserialize処理終了後のコールバック
		//  ロードが行われたときに呼び出される
		ExternalSaveData.Managed.GameMainCallbacks.Deserialize.Callbacks[PluginName] = (gameMain, f_nSaveNo) => _deserialized = true;

		// スカート計算用コールバック
		Managed.Callbacks.DynamicSkirtBone.PreUpdateSelf.Callbacks[PluginName] = DynamicSkirtBonePreUpdate;
		Managed.Callbacks.DynamicSkirtBone.PostUpdateSelf.Callbacks[PluginName] = DynamicSkirtBonePostUpdate;

		// 胸ボーンサイズ調整用コールバック
		Managed.Callbacks.jiggleBone.PreLateUpdateSelf.Callbacks[PluginName] = JiggleBone_PreLateUpdateSelf;
		Managed.Callbacks.jiggleBone.PostLateUpdateSelf.Callbacks[PluginName] = JiggleBone_PostLateUpdateSelf;

		Managed.Callbacks.CharacterMgr.PresetSet.Callbacks[PluginName] = CharacterMgrPresetSet;

		// ロード直後のシーン読み込みなら、初回セットアップを行う
		if (_deserialized) {
			_deserialized = false;
			ExSaveData.CleanupMaids();
			CleanupExSave();
		}

		SliderTemplates.Clear();
	}

	public void Update() {
		// テンプレートキャッシュを消去して、再読み込みを促す
		if (Input.GetKey(KeyCode.F12)) {
			FaceScriptTemplates.Clear();
			SliderTemplates.Clear();
		}
		SliderTemplates.Update(PluginName);

		// エディット画面にいる場合は特別処理として毎フレームアップデートを行う
		if (!_configAllowEditModeFaceActions.Value && SceneManager.GetActiveScene().name == "SceneEdit" && GameMain.Instance?.CharacterMgr != null) {
			foreach (var maid in PluginHelper.GetMaids()) {
				EditSceneMaidUpdate(maid);
			}
		}
	}

	internal static bool GetBooleanProperty(Maid maid, string propName, bool defaultValue) {
		return ExSaveData.GetBool(maid, PluginName, propName, defaultValue);
	}

	internal static float GetFloatProperty(Maid maid, string propName, float defaultValue) {
		return ExSaveData.GetFloat(maid, PluginName, propName, defaultValue);
	}

	private static bool IsAllFaceActionDisabled(Maid maid) => GetBooleanProperty(maid, "MUHYOU", false);
	private static bool IsFaceExpressionDisabled(Maid maid) => GetBooleanProperty(maid, "HYOUJOU_OFF", false);
	private static bool IsLipSyncDisabled(Maid maid) => GetBooleanProperty(maid, "LIPSYNC_OFF", false);

	[HarmonyPatch(typeof(BoneMorph), nameof(BoneMorph.Init))]
	[HarmonyPostfix]
	private static void BoneMorph_OnInit() {
		var tag = "sintyou";
		foreach (var boneAndPropName in BoneAndPropNameList) {
			var boneName = boneAndPropName.Key;
			var key = $"min+{tag}*{boneName.Replace('?', 'L')}";
			if (!BoneMorph.dic.ContainsKey(key)) {
				BoneMorph.SetScale(tag, boneName, 1f, 1f, 1f, 1f, 1f, 1f);
			}
		}
	}

	/// <summary>
	/// BoneMorph_.Blend の処理終了後に呼ばれるコールバック。
	/// 初期化、設定変更時のみ呼び出される。
	/// ボーンのブレンド処理が行われる際、拡張スライダーに関連する補正は基本的にここで行う。
	/// 毎フレーム呼び出されるわけではないことに注意
	/// </summary>
	private static void BoneMorph_BlendCallback(BoneMorph_ boneMorph) {
		if (PluginHelper.TryGetMaid(boneMorph, out var maid) && !maid.IsCrcBody) {
			WideSlider(maid);

			if (SceneManager.GetActiveScene().name != "ScenePhotoMode" && maid.body0 != null && maid.body0.isLoadedBody) {
				var bodyIk = _bodyIk.GetValue(maid.body0, null);
				IKPreInit(bodyIk);
				_bodyIkInit.Invoke(bodyIk, null);
			}
		}
	}

	private static void IKPreInit(object bodyIk) {
		Destroy(_bodyIkMouth);
		Destroy(_bodyIkNippleLeft);
		Destroy(_bodyIkNippleRight);

		void Destroy(FieldInfo fieldInfo) {
			if (fieldInfo.GetValue(bodyIk) is Transform transform) {
				DestroyImmediate(transform.gameObject);
			}
		}
	}

	/// <summary>
	/// TBody.MoveHeadAndEye の処理終了後に呼ばれるコールバック
	///  表示されている間は毎フレーム呼び出される
	/// </summary>
	private static void TBody_MoveHeadAndEyeCallback(TBody tbody) {
		TBodyMoveHeadAndEye.Callback(tbody);

		if (tbody.boMAN || tbody.trsEyeL == null || tbody.trsEyeR == null || tbody.maid == null) {
			return;
		}

		var maid = tbody.maid;

		if (maid.Visible) {
			DisableLipSync(maid);
			DisableFaceAnime(maid);
			Mabataki(maid);
			EyeToCam(maid, tbody);
			HeadToCam(maid, tbody);
			RotatePupil(maid, tbody);
			SetLipSyncIntensity(maid, tbody);
			ForeArmFixOptimized.ForeArmFix(maid);
		}

		return;
	}

	/// <summary>
	/// AudioSourceMgr.Play および AudioSourceMgr.PlayOneShot の処理終了後に呼ばれるコールバック。
	/// ピッチ変更を行う
	/// </summary>
	private static void SetAudioPitch(AudioSourceMgr audioSourceMgr) {
		if (PluginHelper.TryGetMaid(audioSourceMgr, out var maid) && audioSourceMgr.audiosource != null && audioSourceMgr.audiosource.isPlaying) {
			var pitch = GetFloatProperty(maid, "PITCH", 0f);
			audioSourceMgr.audiosource.pitch = 1f + pitch;
		}
	}

	/// <summary>
	/// AddModsSlider等から呼び出されるコールバック
	/// 呼び出し方法は this.gameObject.SendMessage("MaidVoicePitch.TestUpdateSliders");
	/// </summary>
	public void MaidVoicePitch_UpdateSliders() {
		if (GameMain.Instance?.CharacterMgr == null) {
			return;
		}

		foreach (var maid in PluginHelper.GetMaids().Where(e => !e.IsCrcBody && e?.body0?.bonemorph != null)) {
			//
			//	todo	本当にこの方法しかないのか調べること
			//
			//	１人目のメイドをエディットし、管理画面に戻り、
			//	続けて２人目をエディットしようとすると、１人目のメイドの
			//	boneMorphLocal.linkT が null になっていて例外がおきるので
			//	あらかじめ linkT を調べる
			//
			if (maid.body0.bonemorph.bones.All(e => e.linkT != null)) {
				try {
					// 同じ "sintyou" の値を入れて、強制的にモーフ再計算を行う
					var sintyouScale = maid.body0.bonemorph.SCALE_Sintyou;
					maid.body0.BoneMorph_FromProcItem("sintyou", sintyouScale);
				} catch (Exception) {
				}
			}
		}
	}

	/// <summary>
	/// エディットシーン用の状態更新
	/// </summary>
	private void EditSceneMaidUpdate(Maid maid) {
		if (maid == null || !maid.Visible) {
			return;
		}

		if (!(IsAllFaceActionDisabled(maid) || IsLipSyncDisabled(maid) || maid.MicLipSync)) {
			// エディットシーンではリップシンクを強制的に復活させる
			maid.m_bFoceKuchipakuSelfUpdateTime = false;
		}
	}

	/// <summary>
	/// スカートサイズ変更時の処理
	/// </summary>
	private static void DynamicSkirtBonePreUpdate(DynamicSkirtBone bone) {
		var targetTransform = bone.m_trPanierParent.parent;
		_skirtScaleBackUp = targetTransform.localScale;
		targetTransform.localScale = Vector3.one;
	}

	private static void DynamicSkirtBonePostUpdate(DynamicSkirtBone bone) {
		var targetTransform = bone.m_trPanierParent.parent;
		targetTransform.localScale = _skirtScaleBackUp;
	}

	/// <summary>
	/// 胸サイズ変更処理
	/// </summary>
	private static void JiggleBone_PreLateUpdateSelf(jiggleBone bone) {
		_jiggleBoneScaleBackUp = bone.transform.localScale;
	}

	private static void JiggleBone_PostLateUpdateSelf(jiggleBone bone) {
		// 変更処理が実行されなければ終了
		if (bone.transform.localScale == _jiggleBoneScaleBackUp) {
			return;
		}
		// jiggleBoneからMaidを取得
		Maid maid = null;

		if (JiggleBones.ContainsKey(bone)) {
			maid = JiggleBones[bone];
		} else {
			var transform = bone.transform;

			while (maid == null && transform != null) {
				maid = transform.GetComponent<Maid>();
				transform = transform.parent;
			}

			if (maid == null) return;

			JiggleBones[bone] = maid;
		}

		var breastScale = GetBoneScale(maid, "MUNESCL");
		bone.transform.localScale = Vector3.Scale(bone.transform.localScale, breastScale);
	}

	private static void CharacterMgrPresetSet(CharacterMgr __instance, Maid f_maid, CharacterMgr.Preset f_prest) {
		if (f_maid == null) {
			return;
		}
		SliderTemplates.Update(f_maid, PluginName);
	}

	[HarmonyTranspiler]
	[HarmonyPatch(typeof(SceneEdit), nameof(SceneEdit.SlideCallback))]
	private static IEnumerable<CodeInstruction> SceneEdit_SlideCallback(IEnumerable<CodeInstruction> instructions) {
		// SceneEdit.SlideCallback の補間式を変更し、
		// タブ等を変更してスライダーがアクティブになる度に
		// 負の値が 0 に近づくのを抑制する
		//
		// 元の補間式は以下のようになっている
		//
		//      (int) (prop1.min + (prop1.max - prop1.min) * UIProgressBar.current.value + 0.5)
		//
		// 例えば prop1.min = -100, prop1.max = 100, UIProgressBar.current.value = 0 の場合、
		// 以下のようになる
		//
		//        (int) (-100 + (100+100) * 0 + 0.5)
		//		= (int) (-99.5)
		//		= -99
		//
		//      double -> int のキャストについては右記を参照 : https://msdn.microsoft.com/en-us/library/yht2cx7b.aspx
		//
		// この値は期待する値 -100 になっていないので、これを以下のように修正したい
		//
		//      (int) Math.Round(prop1.min + (prop1.max - prop1.min) * UIProgressBar.current.value)
		//
		// ILレベルでは、該当部分は以下のようになっているので
		//
		//      IL_004a: callvirt instance float32 UIProgressBar::get_value()
		//      IL_004f: mul
		//      IL_0050: add
		//  --> IL_0051: ldc.r4 0.5
		//  --> IL_0056: add
		//      IL_0057: conv.i4
		//
		// これを以下のように改変する
		//
		//      IL_004a: callvirt instance float32 UIProgressBar::get_value()
		//      IL_004f: mul
		//      IL_0050: add
		//  --> IL_0051: call float64 [mscorlib]System.Math::Round(float64)
		//  --> IL_0056: nop
		//      IL_0057: conv.i4


		var codes = new List<CodeInstruction>(instructions);
		var instructionIndex = codes.FindLastIndex(e => e.opcode == OpCodes.Ldc_R4 && (float)e.operand == 0.5f);
		var instruction = codes[instructionIndex];
		instruction.opcode = OpCodes.Call;
		instruction.operand = typeof(Math).GetMethod(nameof(Math.Round), new Type[] { typeof(double) });
		codes[instructionIndex + 1].opcode = OpCodes.Nop;
		return codes;
	}

	// 目を常時カメラに向ける
	private static void EyeToCam(Maid maid, TBody tbody) {
		var fEyeToCam = GetFloatProperty(maid, "EYETOCAM", 0f);
		if (fEyeToCam < -0.5f) {
			tbody.boEyeToCam = false;
		} else if (fEyeToCam > 0.5f) {
			tbody.boEyeToCam = true;
		}
	}

	// 顔を常時カメラに向ける
	private static void HeadToCam(Maid maid, TBody tbody) {
		var fHeadToCam = GetFloatProperty(maid, "HEADTOCAM", 0f);
		if (fHeadToCam < -0.5f) {
			tbody.boHeadToCam = false;
		} else if (fHeadToCam > 0.5f) {
			tbody.boHeadToCam = true;
		}
	}

	// まばたき制限
	private static void Mabataki(Maid maid) {
		var mabatakiVal = maid.MabatakiVal;
		var f = Mathf.Clamp01(1f - GetFloatProperty(maid, "MABATAKI", 1f));
		var mMin = Mathf.Asin(f);
		var mMax = (float)Math.PI - mMin;
		mMin = Mathf.Pow(mMin / (float)Math.PI, 0.5f);
		mMax = Mathf.Pow(mMax / (float)Math.PI, 0.5f);
		mabatakiVal = Mathf.Clamp(mabatakiVal, mMin, mMax);
		if (IsAllFaceActionDisabled(maid)) {
			// 無表情の場合、常に目を固定
			mabatakiVal = mMin;
		}
		maid.MabatakiVal = mabatakiVal;
	}

	// 瞳の角度を目の角度に合わせて補正
	private static void RotatePupil(Maid maid, TBody tbody) {
		/*
					//  注意：TBody.MoveHeadAndEye内で trsEye[L,R].localRotation が上書きされているため、
					//  この値は TBody.MoveHeadAndEyeが呼ばれるたびに書き換える必要がある
					float eyeAng = ExSaveData.GetFloat(maid, PluginName, "EYE_ANG.angle", 0f);
					Vector3 eea = (Vector3)Helper.GetInstanceField(typeof(TBody), tbody, "EyeEulerAngle");
					tbody.trsEyeL.localRotation = tbody.quaDefEyeL * Quaternion.Euler(eyeAng, eea.x * -0.2f, eea.z * -0.1f);
					tbody.trsEyeR.localRotation = tbody.quaDefEyeR * Quaternion.Euler(-eyeAng, eea.x * 0.2f, eea.z * 0.1f);
		*/
	}

	// リップシンク強度指定
	private static void SetLipSyncIntensity(Maid maid, TBody tbody) {
		if (!GetBooleanProperty(maid, "LIPSYNC_INTENISTY", false)) {
			return;
		}
		var f1 = Mathf.Clamp01(GetFloatProperty(maid, "LIPSYNC_INTENISTY.value", 1f));
		maid.VoicePara_1 = f1 * 0.5f;
		maid.VoicePara_2 = f1 * 0.074f;
		maid.VoicePara_3 = f1 * 0.5f;
		maid.VoicePara_4 = f1 * 0.05f;
		if (f1 < 0.01f) {
			maid.voice_ao_f2 = 0;
		}
	}

	// リップシンク(口パク)抑制
	private static void DisableLipSync(Maid maid) {
		if (IsAllFaceActionDisabled(maid) || IsLipSyncDisabled(maid)) {
			maid.m_bFoceKuchipakuSelfUpdateTime = true;
		}
	}

	// 目と口の表情変化をやめる
	private static void DisableFaceAnime(Maid maid) {
		if (IsAllFaceActionDisabled(maid) || IsFaceExpressionDisabled(maid)) {
			maid.FaceAnime("", 0f, 0);
		}
	}

	// スライダー範囲を拡大
	public static void WideSlider(Maid maid) {
		var tbody = maid.body0;
		if (tbody?.bonemorph?.bones == null || maid.IsCrcBody) {
			return;
		}

		var boneMorph = tbody.bonemorph;

		var fixLimbs = GetBooleanProperty(maid, "LIMBSFIX", false);

		// スケール変更するボーンのリスト
		var boneScales = fixLimbs ? DistortCorrect.GetBoneScales(maid) : GetBoneScales(maid);

		// ポジション変更するボーンのリスト
		var bonePositions = GetBonePositions(maid);

		var bonePositionRates = fixLimbs ? DistortCorrect.GetBonePositionRates(maid) : null;

		if (!fixLimbs) {
			// 元々尻はPELSCLに連動していたが単体でも設定できるようにする
			// ただし元との整合性をとるため乗算する
			var pelvisScale = GetBoneScale(maid, "PELSCL");
			var hipScale = GetBoneScale(maid, "HIPSCL");
			hipScale = Vector3.Scale(hipScale, pelvisScale);
			boneScales["Hip_L"] = hipScale;
			boneScales["Hip_R"] = hipScale;
		}

		Transform tEyePosL = null;
		Transform tEyePosR = null;

		for (var i = boneMorph.bones.Count - 1; i >= 0; i--) {
			var boneMorphLocal = boneMorph.bones[i];

			GetBoneProperties(boneMorph, boneMorphLocal, out var scale, out var position);

			var linkT = boneMorphLocal.linkT;
			if (linkT == null) {
				continue;
			}

			var name = linkT.name;

			if (name != null) {
				if (name.Contains("Thigh_SCL_")) {
					boneMorph.SnityouOutScale = Mathf.Pow(scale.x, 0.9f);
				}

				// リストに登録されているボーンのスケール設定
				if (boneScales.TryGetValue(name, out var boneScale)) {
					scale = Vector3.Scale(scale, boneScale);
				}

				// リストに登録されているボーンのポジション設定
				if (bonePositions.TryGetValue(name, out var bonePosition)) {
					position += bonePosition;
				}

				// リストに登録されているボーンのポジション設定
				if (fixLimbs && bonePositionRates.TryGetValue(name, out var bonePositionRate)) {
					position = Vector3.Scale(position, bonePositionRate);
				}
			}

			UpdateBreastPositions(tbody);

			// ignoreHeadBonesに登録されている場合はヒラエルキーを辿って頭のツリーを無視
			if (name != null) {
				if (!(IgnoreHeadBones.Contains(name) && CMT.SearchObjObj(maid.body0.m_Bones.transform.Find("Bip01"), linkT))) {
					linkT.localScale = scale;
				}
				linkT.localPosition = position;
			}

			if (name == "Eyepos_L") {
				tEyePosL = linkT;
			}
			if (name == "Eyepos_R") {
				tEyePosR = linkT;
			}
		}

		RotateEyes(maid, tEyePosL, tEyePosR);
		MorphBones(boneMorph);
	}

	private static Dictionary<string, Vector3> GetBonePositions(Maid maid) {
		var bonePositions = new Dictionary<string, Vector3>();

		{
			var (x, _, z) = GetBonePosition(maid, "THIPOS");
			bonePositions["Bip01 L Thigh"] = new Vector3(0, z, -x) / 1000f;
			bonePositions["Bip01 R Thigh"] = new Vector3(0, z, x) / 1000f;
		}

		{
			var (x, y, z) = GetBonePosition(maid, "THI2POS");
			bonePositions["Bip01 L Thigh_SCL_"] = new Vector3(y, z, -x) / 1000f;
			bonePositions["Bip01 R Thigh_SCL_"] = new Vector3(y, z, x) / 1000f;
		}

		// 元々足の位置と連動しており、追加するときに整合性を保つため足の位置との和で計算
		{
			var (x, y, z) = GetBonePosition(maid, "HIPPOS");
			bonePositions["Hip_L"] = bonePositions["Bip01 L Thigh"] + new Vector3(y, z, -x) / 1000f;
			bonePositions["Hip_R"] = bonePositions["Bip01 R Thigh"] + new Vector3(y, z, x) / 1000f;
		}

		{
			var (x, y, z) = GetBonePosition(maid, "MTWPOS");
			bonePositions["momotwist_L"] = new Vector3(x, y, z) / 10f;
			bonePositions["momotwist_R"] = new Vector3(x, y, -z) / 10f;
		}

		{
			var (x, y, z) = GetBonePosition(maid, "MMNPOS");
			bonePositions["momoniku_L"] = new Vector3(x, y, z) / 10f;
			bonePositions["momoniku_R"] = new Vector3(x, -y, z) / 10f;
		}

		{
			var (x, y, z) = GetBonePosition(maid, "SKTPOS");
			bonePositions["Skirt"] = new Vector3(-z, -y, x) / 10f;
		}

		{
			var (x, y, z) = GetBonePosition(maid, "SPIPOS");
			bonePositions["Bip01 Spine"] = new Vector3(-x, y, z) / 10f;
		}

		{
			var (x, y, z) = GetBonePosition(maid, "S0APOS");
			bonePositions["Bip01 Spine0a"] = new Vector3(-x, y, z) / 10f;
		}

		{
			var (x, y, z) = GetBonePosition(maid, "S1POS");
			bonePositions["Bip01 Spine1"] = new Vector3(-x, y, z) / 10f;
		}

		{
			var (x, y, z) = GetBonePosition(maid, "S1APOS");
			bonePositions["Bip01 Spine1a"] = new Vector3(-x, y, z) / 10f;
		}

		{
			var (x, y, z) = GetBonePosition(maid, "NECKPOS");
			bonePositions["Bip01 Neck"] = new Vector3(-x, y, z) / 10f;
		}

		{
			var (x, y, z) = GetBonePosition(maid, "CLVPOS");
			bonePositions["Bip01 L Clavicle"] = new Vector3(-x, y, z) / 10f;
			bonePositions["Bip01 R Clavicle"] = new Vector3(-x, y, -z) / 10f;
		}

		{
			var (x, y, z) = GetBonePosition(maid, "MUNESUBPOS");
			bonePositions["Mune_L_sub"] = new Vector3(-y, z, -x) / 10f;
			bonePositions["Mune_R_sub"] = new Vector3(-y, -z, -x) / 10f;
		}

		{
			var (x, y, z) = GetBonePosition(maid, "MUNEPOS");
			bonePositions["Mune_L"] = new Vector3(z, -y, x) / 10f;
			bonePositions["Mune_R"] = new Vector3(z, -y, -x) / 10f;
		}

		return bonePositions;
	}

	private static void GetBoneProperties(BoneMorph_ boneMorph, BoneMorphLocal boneMorphLocal, out Vector3 scale, out Vector3 position) {
		scale = Vector3.one;
		position = boneMorphLocal.pos;

		for (var i = 0; i < BoneMorph.PropNames.Length; i++) {
			var scaleModifier = i switch {
				0 => boneMorph.SCALE_Kubi,
				1 => boneMorph.SCALE_Ude,
				2 => boneMorph.SCALE_EyeX,
				3 => boneMorph.SCALE_EyeY,
				4 => boneMorph.Postion_EyeX * (0.5f + boneMorph.Postion_EyeY * 0.5f),
				5 => boneMorph.Postion_EyeY,
				6 => boneMorph.SCALE_HeadX,
				7 => boneMorph.SCALE_HeadY,
				8 => boneMorph.SCALE_DouPer,
				9 => boneMorph.SCALE_Sintyou,
				10 => boneMorph.SCALE_Koshi,
				11 => boneMorph.SCALE_Kata,
				12 => boneMorph.SCALE_West,
				_ => 1f,
			};

			if (i == 8 && boneMorphLocal.Kahanshin == 0f) {
				scaleModifier = 1f - scaleModifier;
			}

			if ((boneMorphLocal.atr & 1L << (i & 63)) != 0L) {
				scale = Vector3.Scale(scale, GetScale(i));
			}

			if ((boneMorphLocal.atr & 1L << (i + 32 & 63)) != 0L) {
				position = Vector3.Scale(position, GetScale(i + 32));
			}

			Vector3 GetScale(int vectorIndex) {
				var vectorMin = boneMorphLocal.vecs_min[vectorIndex];
				var vectorMax = boneMorphLocal.vecs_max[vectorIndex];
				var n0 = vectorMin * SliderScale - vectorMax * (SliderScale - 1f);
				var n1 = vectorMax * SliderScale - vectorMin * (SliderScale - 1f);
				var f = (scaleModifier + SliderScale - 1f) * (1f / (SliderScale * 2.0f - 1f));
				return Vector3.Lerp(n0, n1, f);
			}
		}
	}

	private static void UpdateBreastPositions(TBody tbody) {
		var muneLParent = tbody.m_trHitParentL;
		var muneRParent = tbody.m_trHitParentR;
		var muneLChild = tbody.m_trHitChildL;
		var muneRChild = tbody.m_trHitChildR;
		var muneLSub = tbody.m_trsMuneLsub;
		var muneRSub = tbody.m_trsMuneRsub;
		if (muneLChild && muneLParent && muneRChild && muneRParent) {
			muneLChild.localPosition = muneLSub.localPosition;
			muneLParent.localPosition = muneLSub.localPosition;
			muneRChild.localPosition = muneRSub.localPosition;
			muneRParent.localPosition = muneRSub.localPosition;
		}
	}

	private static void RotateEyes(Maid maid, Transform tEyePosL, Transform tEyePosR) {
		var (rx, ry, _) = GetBonePosition(maid, "EYE_ANG");
		var eyeAngle = GetFloatProperty(maid, "EYE_ANG.angle", 0f);
		var eyeAngleX = (rx - 9) / 1000;
		var eyeAngleY = (ry - 17) / 1000;

		// 目のサイズ・角度変更
		// EyeScaleRotate : 目のサイズと角度変更する CM3D.MaidVoicePich.Plugin.cs の追加メソッド
		// http://pastebin.com/DBuN5Sws
		// その１>>923
		// http://jbbs.shitaraba.net/bbs/read.cgi/game/55179/1438196715/923
		if (tEyePosL != null) {
			RotateEye(tEyePosL, new(-0.00560432f, -0.001345155f, 0.06805823f, 0.9976647f));
		}

		if (tEyePosR != null) {
			RotateEye(tEyePosR, new(0.9976647f, 0.06805764f, -0.001350592f, -0.005603582f), -1);
		}

		void RotateEye(Transform linkT, Quaternion rotation, int angleMultiplier = 1) {
			var localCenter = linkT.localPosition + new Vector3(0f, eyeAngleY, eyeAngleX * angleMultiplier); // ローカル座標系での回転中心位置
			var worldCenter = linkT.parent.TransformPoint(localCenter);         // ワールド座標系での回転中心位置
			var localAxis = new Vector3(-1f, 0f, 0f);                       // ローカル座標系での回転軸
			var worldAxis = linkT.TransformDirection(localAxis);               // ワールド座標系での回転軸

			linkT.localRotation = rotation;    // 初期の回転量
			linkT.RotateAround(worldCenter, worldAxis, eyeAngle * angleMultiplier);
		}
	}

	private static void MorphBones(BoneMorph_ boneMorph) {
		// COM3D2追加処理
		// ボーンポジション系
		foreach (var boneMorphPosition in boneMorph.m_listBoneMorphPos) {
			var transform = boneMorphPosition.trBone;
			var defPosition = boneMorphPosition.m_vDefPos;
			var addMin = boneMorphPosition.m_vAddMin;
			var addMax = boneMorphPosition.m_vAddMax;

			switch (boneMorphPosition.strPropName) {
				case "Nosepos":
					transform.localPosition = Lerp(boneMorph.POS_Nose);
					break;
				case "MayuY":
					transform.localPosition = Lerp(boneMorph.POS_MayuY);
					break;
				case "EyeBallPosYL":
				case "EyeBallPosYR":
					transform.localPosition = Lerp(boneMorph.EyeBallPosY);
					break;
				case "Mayupos_L":
				case "Mayupos_R":
					var vector3_1 = Lerp(boneMorph.POS_MayuY);
					var x1 = addMin.x;
					addMin.x = addMax.x;
					addMax.x = x1;
					var vector3_2 = Lerp(boneMorph.POS_MayuX);
					var x3 = vector3_2.x + vector3_1.x - defPosition.x;
					transform.localPosition = new(x3, vector3_1.y, vector3_2.z);
					break;
			}

			Vector3 Lerp(float position) => MaidVoicePitch.Lerp(addMin, boneMorphPosition.m_vDefPos, addMax, position, SliderScale);
		}

		// ボーンスケール系
		foreach (var boneMorphScale in boneMorph.m_listBoneMorphScl) {
			var transform = boneMorphScale.trBone;

			switch (boneMorphScale.strPropName) {
				case "Earscl_L":
				case "Earscl_R":
					transform.localScale = Lerp(boneMorph.SCALE_Ear);
					break;
				case "Nosescl":
					transform.localScale = Lerp(boneMorph.SCALE_Nose);
					break;
				case "EyeBallSclXL":
				case "EyeBallSclXR":
					var eyeBallScaleX = transform.localScale;
					eyeBallScaleX.z = Lerp(boneMorph.EyeBallSclX).z;
					transform.localScale = eyeBallScaleX;
					break;
				case "EyeBallSclYL":
				case "EyeBallSclYR":
					var eyeBallScaleY = transform.localScale;
					eyeBallScaleY.y = Lerp(boneMorph.EyeBallSclY).y;
					transform.localScale = eyeBallScaleY;
					break;
			}

			Vector3 Lerp(float scale) => MaidVoicePitch.Lerp(boneMorphScale.m_vAddMin, boneMorphScale.m_vDefScl, boneMorphScale.m_vAddMax, scale, SliderScale);
		}

		// ボーンローテーション系
		foreach (var boneMorphRotation in boneMorph.m_listBoneMorphRot) {
			var transform = boneMorphRotation.trBone;

			switch (boneMorphRotation.strPropName) {
				case "Earrot_L":
				case "Earrot_R":
					transform.localRotation = RotLerp(boneMorph.ROT_Ear);
					break;
				case "Mayurot_L":
				case "Mayurot_R":
					transform.localRotation = RotLerp(boneMorph.ROT_Mayu);
					break;
			}

			Quaternion RotLerp(float rotation) => MaidVoicePitch.RotLerp(boneMorphRotation.m_vAddMin, boneMorphRotation.m_vDefRotate, boneMorphRotation.m_vAddMax, rotation, SliderScale);
		}
	}

	public static Vector3 Lerp(Vector3 min, Vector3 def, Vector3 max, float t, float sliderScale) {
		if ((double)t >= 0.5) {
			var n1 = max + (max - def) * (sliderScale - 1f) * 2;
			var f = (t - 0.5f) * (1f / (sliderScale * 2.0f - 1f)) * 2.0f;
			return Vector3.Lerp(def, n1, f);
		} else {
			var n0 = min - (def - min) * (sliderScale - 1f) * 2;
			var f = (t + sliderScale - 1f) * (1f / (sliderScale * 2.0f - 1f)) * 2.0f;
			return Vector3.Lerp(n0, def, f);
		}
	}

	public static Quaternion RotLerp(Quaternion min, Quaternion def, Quaternion max, float t, float sliderScale) {
		var t1 = (double)t > 0.5 ? (float)(((double)t - 0.5) / 0.5) : t / 0.5f;
		if ((double)t <= 0.5) {
			return Quaternion.LerpUnclamped(min, def, t1);
		}
		return Quaternion.LerpUnclamped(def, max, t1);
	}

	internal static Vector3 GetBoneScale(Maid maid, string propName) {
		float GetBoneProperty(string axis) => GetFloatProperty(maid, propName + axis, 1f);
		var x = GetBoneProperty(".height");
		var y = GetBoneProperty(".depth");
		var z = GetBoneProperty(".width");
		return new(x, y, z);
	}

	private static Vector3 GetBonePosition(Maid maid, string propName) {
		float GetBoneProperty(string axis) => GetFloatProperty(maid, propName + axis, 0f);
		var x = GetBoneProperty(".x");
		var y = GetBoneProperty(".y");
		var z = GetBoneProperty(".z");
		return new(x, y, z);
	}

	private static Dictionary<string, Vector3> GetBoneScales(Maid maid) {
		var boneScales = new Dictionary<string, Vector3>();
		foreach (var item in BoneAndPropNameList) {
			var boneName = item.Key;
			if (boneName.Contains("?")) {
				var boneNameL = boneName.Replace('?', 'L');
				var boneNameR = boneName.Replace('?', 'R');
				boneScales[boneNameL] = GetBoneScale(maid, item.Value);
				boneScales[boneNameR] = boneScales[boneNameL];
			} else {
				boneScales[boneName] = GetBoneScale(maid, item.Value);
			}
		}
		return boneScales;
	}

	private string GetHierarchy(Transform transform) {
		if (!transform) {
			return string.Empty;
		}
		var hierarchy = "/" + transform.name;
		while (transform.parent) {
			transform = transform.parent;
			hierarchy = "/" + transform.name + hierarchy;
		}

		return hierarchy;
	}

	// 動作していない古い設定を削除する
	private static void CleanupExSave() {
		foreach (var maid in PluginHelper.GetMaids()) {
			foreach (var setting in ObsoleteSettings) {
				ExSaveData.Remove(maid, PluginName, setting);
			}

			{
				var fileName = ExSaveData.Get(maid, PluginName, "SLIDER_TEMPLATE", null);
				if (string.IsNullOrEmpty(fileName)) {
					ExSaveData.Set(maid, PluginName, "SLIDER_TEMPLATE", DefaultTemplateFile, true);
				}
			}
		}

		foreach (var setting in ObsoleteGlobalSettings) {
			ExSaveData.GlobalRemove(PluginName, setting);
		}
	}
}
