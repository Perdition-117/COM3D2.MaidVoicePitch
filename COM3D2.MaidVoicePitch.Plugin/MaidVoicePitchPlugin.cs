using CM3D2.ExternalSaveData.Managed;
using CM3D2.ExternalPreset.Managed;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityInjector;
using UnityInjector.Attributes;
using UnityEngine.SceneManagement;

namespace CM3D2.MaidVoicePitch.Plugin {
	[PluginName("CM3D2 MaidVoicePitch"), PluginVersion("0.2.18")]
	public class MaidVoicePitch : PluginBase {
		public static string PluginName { get { return "CM3D2.MaidVoicePitch"; } }
		static bool bDeserialized = false;

		//static string[] boneMorph_PropNames;

		//static string[] BoneMorph_PropNames
		//{
		//    get
		//    {
		//        if (boneMorph_PropNames == null)
		//        {
		//            boneMorph_PropNames = Helper.GetInstanceField(typeof(BoneMorph_), null, "PropNames") as string[];
		//        }
		//        return boneMorph_PropNames;
		//    }
		//}

		TBodyMoveHeadAndEye tbodyMoveHeadAndEye = new TBodyMoveHeadAndEye();

		static Vector3 skirtScaleBackUp;
		static Vector3 jiggleBoneScaleBackUp;

		/// <summary>
		/// Transform変形を行うボーンのリスト。
		/// ここに書いておくと自動でBoneMorphに登録されTransform処理されます。
		/// string[]の内容は{"ボーン名", "ExSaveのプロパティ名"}
		/// ボーン名に?が含まれるとLとRに置換されます。
		/// 頭に影響が行くボーンを登録する場合は
		/// WIDESLIDER() 内の ignoreHeadBones にボーン名を書くこと。
		/// </summary>
		private string[][] boneAndPropNameList = new string[][]
		{
				new string[] { "Bip01 ? Thigh", "THISCL" },         // 下半身
                new string[] { "momotwist_?", "MTWSCL" },         // ももツイスト
                new string[] { "momoniku_?", "MMNSCL" },         // もも肉
                new string[] { "Bip01 Pelvis_SCL_", "PELSCL" },     // 骨盤
                new string[] { "Bip01 ? Thigh_SCL_", "THISCL2" },   // 膝
                new string[] { "Bip01 ? Calf", "CALFSCL" },         // 膝下
                new string[] { "Bip01 ? Foot", "FOOTSCL" },         // 足首より下
                new string[] { "Skirt", "SKTSCL" },                 // スカート
                new string[] { "Bip01 Spine_SCL_", "SPISCL" },      // 胴(下腹部周辺)
                new string[] { "Bip01 Spine0a_SCL_", "S0ASCL" },    // 胴0a(腹部周辺)
                new string[] { "Bip01 Spine1_SCL_", "S1_SCL" },     // 胴1_(みぞおち周辺)
                new string[] { "Bip01 Spine1a_SCL_", "S1ASCL" },    // 胴1a(首・肋骨周辺)
                new string[] { "Bip01 Spine1a", "S1ABASESCL" },     // 胴1a(胸より上)※頭に影響有り
                new string[] { "Kata_?", "KATASCL" },               // 肩
                new string[] { "Bip01 ? UpperArm", "UPARMSCL" },    // 上腕
                new string[] { "Bip01 ? Forearm", "FARMSCL" },      // 前腕
                new string[] { "Bip01 ? Hand", "HANDSCL" },         // 手
                new string[] { "Bip01 ? Clavicle", "CLVSCL" },      // 鎖骨
                new string[] { "Mune_?", "MUNESCL" },               // 胸
                new string[] { "Mune_?_sub", "MUNESUBSCL" },        // 胸サブ
                new string[] { "Bip01 Neck_SCL_", "NECKSCL" },      // 首
                //new string[] { "", "" },
        };

		FieldInfo f_muneLParent = Helper.GetFieldInfo(typeof(TBody), "m_trHitParentL");
		FieldInfo f_muneLChild = Helper.GetFieldInfo(typeof(TBody), "m_trHitChildL");
		FieldInfo f_muneRParent = Helper.GetFieldInfo(typeof(TBody), "m_trHitParentR");
		FieldInfo f_muneRChild = Helper.GetFieldInfo(typeof(TBody), "m_trHitChildR");
		FieldInfo f_muneLSub = Helper.GetFieldInfo(typeof(TBody), "m_trsMuneLsub");
		FieldInfo f_muneRSub = Helper.GetFieldInfo(typeof(TBody), "m_trsMuneRsub");

		public void Awake() {
			UnityEngine.GameObject.DontDestroyOnLoad(this);

			string tag = "sintyou";
			foreach (string[] boneAndPropName in boneAndPropNameList) {
				string bname = boneAndPropName[0];
				string key = string.Concat("min+" + tag, "*", bname.Replace('?', 'L'));
				if (!BoneMorph.dic.ContainsKey(key)) {
					PluginHelper.BoneMorphSetScale(tag, bname, 1f, 1f, 1f, 1f, 1f, 1f);
				}
			}

			SceneManager.sceneLoaded += OnSceneLoaded;

			// ExPresetに外部から登録
			ExPreset.AddExSaveNode(PluginName);
			ExPreset.loadNotify.AddListener(MaidVoicePitch_UpdateSliders);
		}

		void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
			KagHooks.SetHook(PluginName, true);

			// TBody.MoveHeadAndEye 処理終了後のコールバック
			CM3D2.MaidVoicePitch.Managed.Callbacks.TBody.MoveHeadAndEye.Callbacks[PluginName] = tbodyMoveHeadAndEyeCallback;

			// BoneMorph_.Blend 処理終了後のコールバック
			CM3D2.MaidVoicePitch.Managed.Callbacks.BoneMorph_.Blend.Callbacks[PluginName] = boneMorph_BlendCallback;

			// GameMain.Deserialize処理終了後のコールバック
			//  ロードが行われたときに呼び出される
			CM3D2.ExternalSaveData.Managed.GameMainCallbacks.Deserialize.Callbacks[PluginName] =
				(gameMain, f_nSaveNo) => {
					bDeserialized = true;
				};

			// スカート計算用コールバック
			CM3D2.MaidVoicePitch.Managed.Callbacks.DynamicSkirtBone.PreUpdateSelf.Callbacks[PluginName] = DynamicSkirtBonePreUpdate;
			CM3D2.MaidVoicePitch.Managed.Callbacks.DynamicSkirtBone.PostUpdateSelf.Callbacks[PluginName] = DynamicSkirtBonePostUpdate;

			// 胸ボーンサイズ調整用コールバック
			CM3D2.MaidVoicePitch.Managed.Callbacks.jiggleBone.PreLateUpdateSelf.Callbacks[PluginName] = jiggleBonePreLateUpdateSelef;
			CM3D2.MaidVoicePitch.Managed.Callbacks.jiggleBone.PostLateUpdateSelf.Callbacks[PluginName] = jiggleBonePostLateUpdateSelef;

			CM3D2.MaidVoicePitch.Managed.Callbacks.CharacterMgr.PresetSet.Callbacks[PluginName] = CharacterMgrPresetSet;

			// ロード直後のシーン読み込みなら、初回セットアップを行う
			if (bDeserialized) {
				bDeserialized = false;
				ExSaveData.CleanupMaids();
				CleanupExSave();
			}

			SliderTemplates.Clear();
		}

		public void OnGUI() {
			PluginHelper.DebugGui();
		}

		public void Update() {
			//PluginHelper.LineClear();
			PluginHelper.DebugClear();
			// テンプレートキャッシュを消去して、再読み込みを促す
			if (Input.GetKey(KeyCode.F12)) {
				FaceScriptTemplates.Clear();
				SliderTemplates.Clear();
			}
			SliderTemplates.Update(PluginName);

			// エディット画面にいる場合は特別処理として毎フレームアップデートを行う
			if (SceneManager.GetActiveScene().name == "SceneEdit") {
				if (GameMain.Instance != null && GameMain.Instance.CharacterMgr != null) {
					CharacterMgr cm = GameMain.Instance.CharacterMgr;
					for (int i = 0, n = cm.GetStockMaidCount(); i < n; i++) {
						EditSceneMaidUpdate(cm.GetStockMaid(i));
					}
				}
			}
		}

		/// <summary>
		/// BoneMorph_.Blend の処理終了後に呼ばれるコールバック。
		/// 初期化、設定変更時のみ呼び出される。
		/// ボーンのブレンド処理が行われる際、拡張スライダーに関連する補正は基本的にここで行う。
		/// 毎フレーム呼び出されるわけではないことに注意
		/// </summary>
		void boneMorph_BlendCallback(BoneMorph_ boneMorph_) {
			Maid maid = PluginHelper.GetMaid(boneMorph_);
			if (maid == null) {
				return;
			}
			WideSlider(maid);
			//EyeBall(maid);

			if (SceneManager.GetActiveScene().name != "ScenePhotoMode" && maid.body0 != null && maid.body0.isLoadedBody) {
				IKPreInit(maid);
				maid.body0.IKCtrl.Init();
				//.IKCtrl.Init();
			}
		}

		void IKPreInit(Maid maid) {
			FullBodyIKCtrl fbikc = maid.body0.IKCtrl;

			Transform mouth = (Transform)Helper.GetInstanceField(typeof(FullBodyIKCtrl), fbikc, "m_Mouth");
			if (mouth) {
				DestroyImmediate(mouth.gameObject);
			}

			Transform nippleL = (Transform)Helper.GetInstanceField(typeof(FullBodyIKCtrl), fbikc, "m_NippleL");
			if (nippleL) {
				DestroyImmediate(nippleL.gameObject);
			}

			Transform nippleR = (Transform)Helper.GetInstanceField(typeof(FullBodyIKCtrl), fbikc, "m_NippleR");

			if (nippleR) {
				DestroyImmediate(nippleR.gameObject);
			}
		}

		/// <summary>
		/// TBody.MoveHeadAndEye の処理終了後に呼ばれるコールバック
		///  表示されている間は毎フレーム呼び出される
		/// </summary>
		void tbodyMoveHeadAndEyeCallback(TBody tbody) {
			tbodyMoveHeadAndEye.Callback(tbody);

			if (tbody.boMAN || tbody.trsEyeL == null || tbody.trsEyeR == null || tbody.maid == null) {
				return;
			}

			Maid maid = tbody.maid;

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
		}

		/// <summary>
		/// AudioSourceMgr.Play および AudioSourceMgr.PlayOneShot の処理終了後に呼ばれるコールバック。
		/// ピッチ変更を行う
		/// </summary>
		static void SetAudioPitch(AudioSourceMgr audioSourceMgr) {
			Maid maid = PluginHelper.GetMaid(audioSourceMgr);
			if (maid == null || audioSourceMgr.audiosource == null || !audioSourceMgr.audiosource.isPlaying) {
				return;
			}
			float f = ExSaveData.GetFloat(maid, PluginName, "PITCH", 0f);
			audioSourceMgr.audiosource.pitch = 1f + f;
		}

		/// <summary>
		/// AddModsSlider等から呼び出されるコールバック
		/// 呼び出し方法は this.gameObject.SendMessage("MaidVoicePitch.TestUpdateSliders");
		/// </summary>
		public void MaidVoicePitch_UpdateSliders() {
			//Console.WriteLine("Updating sliders....");

			if (GameMain.Instance != null && GameMain.Instance.CharacterMgr != null) {
				CharacterMgr cm = GameMain.Instance.CharacterMgr;
				for (int i = 0, n = cm.GetStockMaidCount(); i < n; i++) {
					Maid maid = cm.GetStockMaid(i);
					if (maid != null && maid.body0 != null && maid.body0.bonemorph != null) {
						//
						//	todo	本当にこの方法しかないのか調べること
						//
						//	１人目のメイドをエディットし、管理画面に戻り、
						//	続けて２人目をエディットしようとすると、１人目のメイドの
						//	boneMorphLocal.linkT が null になっていて例外がおきるので
						//	あらかじめ linkT を調べる
						//
						bool safe = true;
						foreach (BoneMorphLocal boneMorphLocal in maid.body0.bonemorph.bones) {
							if (boneMorphLocal.linkT == null) {
								safe = false;
							}
						}
						if (safe) {
							try {

								// 同じ "sintyou" の値を入れて、強制的にモーフ再計算を行う
								float SCALE_Sintyou = maid.body0.bonemorph.SCALE_Sintyou;
								maid.body0.BoneMorph_FromProcItem("sintyou", SCALE_Sintyou);

							} catch (Exception) {
								;
							}
						}
					}
				}
			}
		}

		/// <summary>
		/// エディットシーン用の状態更新
		/// </summary>
		void EditSceneMaidUpdate(Maid maid) {
			if (maid == null || !maid.Visible) {
				return;
			}

			bool bMuhyou = ExSaveData.GetBool(maid, PluginName, "MUHYOU", false);
			bool bLipSyncOff = ExSaveData.GetBool(maid, PluginName, "LIPSYNC_OFF", false);
			if (bLipSyncOff || bMuhyou || maid.MicLipSync) {
				// 何もしない
			} else {
				// エディットシーンではリップシンクを強制的に復活させる
				Helper.SetInstanceField(typeof(Maid), maid, "m_bFoceKuchipakuSelfUpdateTime", false);
			}
		}

		/// <summary>
		/// スカートサイズ変更時の処理
		/// </summary>
		void DynamicSkirtBonePreUpdate(DynamicSkirtBone dsb) {
			Transform targetTransform = ((Transform)Helper.GetInstanceField(typeof(DynamicSkirtBone), dsb, "m_trPanierParent")).parent;
			skirtScaleBackUp = targetTransform.localScale;
			targetTransform.localScale = Vector3.one;
		}

		void DynamicSkirtBonePostUpdate(DynamicSkirtBone dsb) {
			Transform targetTransform = ((Transform)Helper.GetInstanceField(typeof(DynamicSkirtBone), dsb, "m_trPanierParent")).parent;
			targetTransform.localScale = skirtScaleBackUp;
		}

		/// <summary>
		/// 胸サイズ変更処理
		/// </summary>
		void jiggleBonePreLateUpdateSelef(jiggleBone jiggleBone) {
			jiggleBoneScaleBackUp = jiggleBone.transform.localScale;
		}

		static Dictionary<jiggleBone, Maid> jigBones = new Dictionary<jiggleBone, Maid>();
		void jiggleBonePostLateUpdateSelef(jiggleBone jiggleBone) {
			// 変更処理が実行されなければ終了
			if (jiggleBone.transform.localScale == jiggleBoneScaleBackUp) return;
			// jiggleBoneからMaidを取得
			Maid maid = null;

			if (jigBones.ContainsKey(jiggleBone)) {
				maid = jigBones[jiggleBone];
			} else {
				Transform t = jiggleBone.transform;

				while (maid == null && t != null) {
					maid = t.GetComponent<Maid>();
					t = t.parent;
				}

				if (maid == null) return;

				jigBones[jiggleBone] = maid;
			}

			// スライダー拡張オフなら何もしない
			if (!ExSaveData.GetBool(maid, PluginName, "WIDESLIDER", false)) {
				return;
			}

			float sx = ExSaveData.GetFloat(maid, PluginName, "MUNESCL.height", 1f);
			float sy = ExSaveData.GetFloat(maid, PluginName, "MUNESCL.depth", 1f);
			float sz = ExSaveData.GetFloat(maid, PluginName, "MUNESCL.width", 1f);

			Vector3 scl = jiggleBone.transform.localScale;
			jiggleBone.transform.localScale = new Vector3(scl.x * sx, scl.y * sy, scl.z * sz);
		}

		void CharacterMgrPresetSet(CharacterMgr cMgr, Maid maid, CharacterMgr.Preset preset) {
			if (maid == null) {
				return;
			}
			SliderTemplates.Update(maid, PluginName);
		}

		// 目を常時カメラに向ける
		void EyeToCam(Maid maid, TBody tbody) {
			float fEyeToCam = ExSaveData.GetFloat(maid, PluginName, "EYETOCAM", 0f);
			if (fEyeToCam < -0.5f) {
				tbody.boEyeToCam = false;
			} else if (fEyeToCam > 0.5f) {
				tbody.boEyeToCam = true;
			}
		}

		// 顔を常時カメラに向ける
		void HeadToCam(Maid maid, TBody tbody) {
			float fHeadToCam = ExSaveData.GetFloat(maid, PluginName, "HEADTOCAM", 0f);
			if (fHeadToCam < -0.5f) {
				tbody.boHeadToCam = false;
			} else if (fHeadToCam > 0.5f) {
				tbody.boHeadToCam = true;
			}
		}

		// まばたき制限
		void Mabataki(Maid maid) {
			float mabatakiVal = (float)Helper.GetInstanceField(typeof(Maid), maid, "MabatakiVal");
			float f = Mathf.Clamp01(1f - ExSaveData.GetFloat(maid, PluginName, "MABATAKI", 1f));
			float mMin = Mathf.Asin(f);
			float mMax = (float)Math.PI - mMin;
			mMin = Mathf.Pow(mMin / (float)Math.PI, 0.5f);
			mMax = Mathf.Pow(mMax / (float)Math.PI, 0.5f);
			mabatakiVal = Mathf.Clamp(mabatakiVal, mMin, mMax);
			if (ExSaveData.GetBool(maid, PluginName, "MUHYOU", false)) {
				// 無表情の場合、常に目を固定
				mabatakiVal = mMin;
			}
			Helper.SetInstanceField(typeof(Maid), maid, "MabatakiVal", mabatakiVal);
		}

		// 瞳サイズ変更
		void EyeBall(Maid maid) {
			TBody tbody = maid.body0;
			if (tbody != null && tbody.trsEyeL != null && tbody.trsEyeR != null) {
				float w = ExSaveData.GetFloat(maid, PluginName, "EYEBALL.width", 1f);
				float h = ExSaveData.GetFloat(maid, PluginName, "EYEBALL.height", 1f);
				tbody.trsEyeL.localScale = new Vector3(1f, h, w);
				tbody.trsEyeR.localScale = new Vector3(1f, h, w);
			}
		}

		// 瞳の角度を目の角度に合わせて補正
		void RotatePupil(Maid maid, TBody tbody) {
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
		void SetLipSyncIntensity(Maid maid, TBody tbody) {
			if (!ExSaveData.GetBool(maid, PluginName, "LIPSYNC_INTENISTY", false)) {
				return;
			}
			float f1 = Mathf.Clamp01(ExSaveData.GetFloat(maid, PluginName, "LIPSYNC_INTENISTY.value", 1f));
			maid.VoicePara_1 = f1 * 0.5f;
			maid.VoicePara_2 = f1 * 0.074f;
			maid.VoicePara_3 = f1 * 0.5f;
			maid.VoicePara_4 = f1 * 0.05f;
			if (f1 < 0.01f) {
				maid.voice_ao_f2 = 0;
			}
		}

		// リップシンク(口パク)抑制
		void DisableLipSync(Maid maid) {
			bool bMuhyou = ExSaveData.GetBool(maid, PluginName, "MUHYOU", false);
			bool bLipSyncOff = ExSaveData.GetBool(maid, PluginName, "LIPSYNC_OFF", false);
			if (bLipSyncOff || bMuhyou) {
				Helper.SetInstanceField(typeof(Maid), maid, "m_bFoceKuchipakuSelfUpdateTime", true);
			}
		}

		// 目と口の表情変化をやめる
		void DisableFaceAnime(Maid maid) {
			bool bMuhyou = ExSaveData.GetBool(maid, PluginName, "MUHYOU", false);
			bool bHyoujouOff = ExSaveData.GetBool(maid, PluginName, "HYOUJOU_OFF", false);
			if (bHyoujouOff || bMuhyou) {
				maid.FaceAnime("", 0f, 0);
			}
		}

		// スライダー範囲を拡大
		void WideSlider(Maid maid) {
			if (!ExSaveData.GetBool(maid, PluginName, "WIDESLIDER", false)) {
				return;
			}

			TBody tbody = maid.body0;
			//string[] PropNames = BoneMorph_PropNames;
			if (tbody == null || tbody.bonemorph == null || tbody.bonemorph.bones == null) {
				return;
			}
			BoneMorph_ boneMorph_ = tbody.bonemorph;

			// スケール変更するボーンのリスト
			Dictionary<string, Vector3> boneScale = new Dictionary<string, Vector3>();

			// ポジション変更するボーンのリスト
			Dictionary<string, Vector3> bonePosition = new Dictionary<string, Vector3>();

			//この配列に記載があるボーンは頭に影響を与えずにTransformを反映させる。
			//ただしボディに繋がっている中のアレは影響を受ける。
			string[] ignoreHeadBones = new string[] { "Bip01 Spine1a" };

			float eyeAngAngle;
			float eyeAngX;
			float eyeAngY;
			{
				float ra = ExSaveData.GetFloat(maid, PluginName, "EYE_ANG.angle", 0f);
				float rx = ExSaveData.GetFloat(maid, PluginName, "EYE_ANG.x", 0f);
				float ry = ExSaveData.GetFloat(maid, PluginName, "EYE_ANG.y", 0f);

				rx += -9f;
				ry += -17f;

				rx /= 1000f;
				ry /= 1000f;

				eyeAngAngle = ra;
				eyeAngX = rx;
				eyeAngY = ry;
			}

			Vector3 thiScl = new Vector3(
				1.0f,
				ExSaveData.GetFloat(maid, PluginName, "THISCL.depth", 1f),
				ExSaveData.GetFloat(maid, PluginName, "THISCL.width", 1f));

			Vector3 thiPosL;
			Vector3 thiPosR;
			{
				float dx = ExSaveData.GetFloat(maid, PluginName, "THIPOS.x", 0f);
				float dz = ExSaveData.GetFloat(maid, PluginName, "THIPOS.z", 0f);
				float dy = 0.0f;
				thiPosL = new Vector3(dy, dz / 1000f, -dx / 1000f);
				thiPosR = new Vector3(dy, dz / 1000f, dx / 1000f);
			}
			bonePosition["Bip01 L Thigh"] = thiPosL;
			bonePosition["Bip01 R Thigh"] = thiPosR;

			Vector3 thi2PosL;
			Vector3 thi2PosR;
			{
				float dx = ExSaveData.GetFloat(maid, PluginName, "THI2POS.x", 0f);
				float dz = ExSaveData.GetFloat(maid, PluginName, "THI2POS.z", 0f);
				float dy = ExSaveData.GetFloat(maid, PluginName, "THI2POS.y", 0f);
				thi2PosL = new Vector3(dy / 1000f, dz / 1000f, -dx / 1000f);
				thi2PosR = new Vector3(dy / 1000f, dz / 1000f, dx / 1000f);
			}
			bonePosition["Bip01 L Thigh_SCL_"] = thi2PosL;
			bonePosition["Bip01 R Thigh_SCL_"] = thi2PosR;

			// 元々足の位置と連動しており、追加するときに整合性を保つため足の位置との和で計算
			Vector3 hipPosL;
			Vector3 hipPosR;
			{
				float dx = ExSaveData.GetFloat(maid, PluginName, "HIPPOS.x", 0f);
				float dy = ExSaveData.GetFloat(maid, PluginName, "HIPPOS.y", 0f);
				float dz = ExSaveData.GetFloat(maid, PluginName, "HIPPOS.z", 0f);
				hipPosL = new Vector3(dy / 1000f, dz / 1000f, -dx / 1000f);
				hipPosR = new Vector3(dy / 1000f, dz / 1000f, dx / 1000f);
			}
			bonePosition["Hip_L"] = thiPosL + hipPosL;
			bonePosition["Hip_R"] = thiPosR + hipPosR;

			Vector3 mtwPosL;
			Vector3 mtwPosR;
			{
				float dx = ExSaveData.GetFloat(maid, PluginName, "MTWPOS.x", 0f);
				float dy = ExSaveData.GetFloat(maid, PluginName, "MTWPOS.y", 0f);
				float dz = ExSaveData.GetFloat(maid, PluginName, "MTWPOS.z", 0f);
				mtwPosL = new Vector3(dx / 10f, dy / 10f, dz / 10f);
				mtwPosR = new Vector3(dx / 10f, dy / 10f, -dz / 10f);
			}
			bonePosition["momotwist_L"] = mtwPosL;
			bonePosition["momotwist_R"] = mtwPosR;

			Vector3 mmnPosL;
			Vector3 mmnPosR;
			{
				float dx = ExSaveData.GetFloat(maid, PluginName, "MMNPOS.x", 0f);
				float dy = ExSaveData.GetFloat(maid, PluginName, "MMNPOS.y", 0f);
				float dz = ExSaveData.GetFloat(maid, PluginName, "MMNPOS.z", 0f);
				mmnPosL = new Vector3(dx / 10f, dy / 10f, dz / 10f);
				mmnPosR = new Vector3(dx / 10f, -dy / 10f, dz / 10f);
			}
			bonePosition["momoniku_L"] = mmnPosL;
			bonePosition["momoniku_R"] = mmnPosR;

			Vector3 skirtPos;
			{
				float dx = ExSaveData.GetFloat(maid, PluginName, "SKTPOS.x", 0f);
				float dy = ExSaveData.GetFloat(maid, PluginName, "SKTPOS.y", 0f);
				float dz = ExSaveData.GetFloat(maid, PluginName, "SKTPOS.z", 0f);
				skirtPos = new Vector3(-dz / 10f, -dy / 10f, dx / 10f);
			}
			bonePosition["Skirt"] = skirtPos;

			Vector3 spinePos;
			{
				float dx = ExSaveData.GetFloat(maid, PluginName, "SPIPOS.x", 0f);
				float dy = ExSaveData.GetFloat(maid, PluginName, "SPIPOS.y", 0f);
				float dz = ExSaveData.GetFloat(maid, PluginName, "SPIPOS.z", 0f);
				spinePos = new Vector3(-dx / 10f, dy / 10f, dz / 10f);
			}
			bonePosition["Bip01 Spine"] = spinePos;

			Vector3 spine0aPos;
			{
				float dx = ExSaveData.GetFloat(maid, PluginName, "S0APOS.x", 0f);
				float dy = ExSaveData.GetFloat(maid, PluginName, "S0APOS.y", 0f);
				float dz = ExSaveData.GetFloat(maid, PluginName, "S0APOS.z", 0f);
				spine0aPos = new Vector3(-dx / 10f, dy / 10f, dz / 10f);
			}
			bonePosition["Bip01 Spine0a"] = spine0aPos;

			Vector3 spine1Pos;
			{
				float dx = ExSaveData.GetFloat(maid, PluginName, "S1POS.x", 0f);
				float dy = ExSaveData.GetFloat(maid, PluginName, "S1POS.y", 0f);
				float dz = ExSaveData.GetFloat(maid, PluginName, "S1POS.z", 0f);
				spine1Pos = new Vector3(-dx / 10f, dy / 10f, dz / 10f);
			}
			bonePosition["Bip01 Spine1"] = spine1Pos;

			Vector3 spine1aPos;
			{
				float dx = ExSaveData.GetFloat(maid, PluginName, "S1APOS.x", 0f);
				float dy = ExSaveData.GetFloat(maid, PluginName, "S1APOS.y", 0f);
				float dz = ExSaveData.GetFloat(maid, PluginName, "S1APOS.z", 0f);
				spine1aPos = new Vector3(-dx / 10f, dy / 10f, dz / 10f);
			}
			bonePosition["Bip01 Spine1a"] = spine1aPos;

			Vector3 neckPos;
			{
				float dx = ExSaveData.GetFloat(maid, PluginName, "NECKPOS.x", 0f);
				float dy = ExSaveData.GetFloat(maid, PluginName, "NECKPOS.y", 0f);
				float dz = ExSaveData.GetFloat(maid, PluginName, "NECKPOS.z", 0f);
				neckPos = new Vector3(-dx / 10f, dy / 10f, dz / 10f);
			}
			bonePosition["Bip01 Neck"] = neckPos;

			Vector3 clvPosL;
			Vector3 clvPosR;
			{
				float dx = ExSaveData.GetFloat(maid, PluginName, "CLVPOS.x", 0f);
				float dz = ExSaveData.GetFloat(maid, PluginName, "CLVPOS.z", 0f);
				float dy = ExSaveData.GetFloat(maid, PluginName, "CLVPOS.y", 0f);
				clvPosL = new Vector3(-dx / 10f, dy / 10f, dz / 10f);
				clvPosR = new Vector3(-dx / 10f, dy / 10f, -dz / 10f);
			}
			bonePosition["Bip01 L Clavicle"] = clvPosL;
			bonePosition["Bip01 R Clavicle"] = clvPosR;

			Vector3 muneSubPosL;
			Vector3 muneSubPosR;
			{
				float dx = ExSaveData.GetFloat(maid, PluginName, "MUNESUBPOS.x", 0f);
				float dz = ExSaveData.GetFloat(maid, PluginName, "MUNESUBPOS.z", 0f);
				float dy = ExSaveData.GetFloat(maid, PluginName, "MUNESUBPOS.y", 0f);
				muneSubPosL = new Vector3(-dy / 10f, dz / 10f, -dx / 10f);
				muneSubPosR = new Vector3(-dy / 10f, -dz / 10f, -dx / 10f);
			}
			bonePosition["Mune_L_sub"] = muneSubPosL;
			bonePosition["Mune_R_sub"] = muneSubPosR;

			Vector3 munePosL;
			Vector3 munePosR;
			{
				float dx = ExSaveData.GetFloat(maid, PluginName, "MUNEPOS.x", 0f);
				float dz = ExSaveData.GetFloat(maid, PluginName, "MUNEPOS.z", 0f);
				float dy = ExSaveData.GetFloat(maid, PluginName, "MUNEPOS.y", 0f);
				munePosL = new Vector3(dz / 10f, -dy / 10f, dx / 10f);
				munePosR = new Vector3(dz / 10f, -dy / 10f, -dx / 10f);
			}
			bonePosition["Mune_L"] = munePosL;
			bonePosition["Mune_R"] = munePosR;

			// スケール変更するボーンをリストに一括登録
			SetBoneScaleFromList(boneScale, maid, boneAndPropNameList);

			// 元々尻はPELSCLに連動していたが単体でも設定できるようにする
			// ただし元との整合性をとるため乗算する
			Vector3 pelScl = new Vector3(
				ExSaveData.GetFloat(maid, PluginName, "PELSCL.height", 1f),
				ExSaveData.GetFloat(maid, PluginName, "PELSCL.depth", 1f),
				ExSaveData.GetFloat(maid, PluginName, "PELSCL.width", 1f));
			Vector3 hipScl = new Vector3(
				ExSaveData.GetFloat(maid, PluginName, "HIPSCL.height", 1f) * pelScl.x,
				ExSaveData.GetFloat(maid, PluginName, "HIPSCL.depth", 1f) * pelScl.y,
				ExSaveData.GetFloat(maid, PluginName, "HIPSCL.width", 1f) * pelScl.z);
			boneScale["Hip_L"] = hipScl;
			boneScale["Hip_R"] = hipScl;

			Transform tEyePosL = null;
			Transform tEyePosR = null;

			float sliderScale = 20f;
			for (int i = boneMorph_.bones.Count - 1; i >= 0; i--) {
				BoneMorphLocal boneMorphLocal = boneMorph_.bones[i];
				Vector3 scl = new Vector3(1f, 1f, 1f);
				Vector3 pos = boneMorphLocal.pos;
				//for (int j = 0; j < (int)PropNames.Length; j++)
				for (int j = 0; j < BoneMorph.PropNames.Length; j++) {
					float s = 1f;
					switch (j) {
						case 0:
							s = boneMorph_.SCALE_Kubi;
							break;
						case 1:
							s = boneMorph_.SCALE_Ude;
							break;
						case 2:
							s = boneMorph_.SCALE_EyeX;
							break;
						case 3:
							s = boneMorph_.SCALE_EyeY;
							break;
						case 4:
							s = boneMorph_.Postion_EyeX * (0.5f + boneMorph_.Postion_EyeY * 0.5f);
							break;
						case 5:
							s = boneMorph_.Postion_EyeY;
							break;
						case 6:
							s = boneMorph_.SCALE_HeadX;
							break;
						case 7:
							s = boneMorph_.SCALE_HeadY;
							break;
						case 8:
							s = boneMorph_.SCALE_DouPer;
							if (boneMorphLocal.Kahanshin == 0f) {
								s = 1f - s;
							}
							break;
						case 9:
							s = boneMorph_.SCALE_Sintyou;
							break;
						case 10:
							s = boneMorph_.SCALE_Koshi;
							break;
						case 11:
							s = boneMorph_.SCALE_Kata;
							break;
						case 12:
							s = boneMorph_.SCALE_West;
							break;
						default:
							s = 1f;
							break;
					}

					if ((boneMorphLocal.atr & 1L << (j & 63)) != 0L) {
						Vector3 v0 = boneMorphLocal.vecs_min[j];
						Vector3 v1 = boneMorphLocal.vecs_max[j];

						Vector3 n0 = v0 * sliderScale - v1 * (sliderScale - 1f);
						Vector3 n1 = v1 * sliderScale - v0 * (sliderScale - 1f);
						float f = (s + sliderScale - 1f) * (1f / (sliderScale * 2.0f - 1f));
						scl = Vector3.Scale(scl, Vector3.Lerp(n0, n1, f));
					}

					if ((boneMorphLocal.atr & 1L << (j + 32 & 63)) != 0L) {
						Vector3 v0 = boneMorphLocal.vecs_min[j + 32];
						Vector3 v1 = boneMorphLocal.vecs_max[j + 32];

						Vector3 n0 = v0 * sliderScale - v1 * (sliderScale - 1f);
						Vector3 n1 = v1 * sliderScale - v0 * (sliderScale - 1f);
						float f = (s + sliderScale - 1f) * (1f / (sliderScale * 2.0f - 1f));
						pos = Vector3.Scale(pos, Vector3.Lerp(n0, n1, f));
					}
				}

				Transform linkT = boneMorphLocal.linkT;
				if (linkT == null) {
					continue;
				}

				string name = linkT.name;

				if (name != null && name.Contains("Thigh_SCL_")) {
					boneMorph_.SnityouOutScale = Mathf.Pow(scl.x, 0.9f);
				}

				// リストに登録されているボーンのスケール設定
				if (name != null && boneScale.ContainsKey(name)) {
					scl = Vector3.Scale(scl, boneScale[name]);
				}

				// リストに登録されているボーンのポジション設定
				if (name != null && bonePosition.ContainsKey(name)) {
					pos += bonePosition[name];
				}

				Transform muneLParent = f_muneLParent.GetValue(tbody) as Transform;
				Transform muneLChild = f_muneLChild.GetValue(tbody) as Transform;
				Transform muneRParent = f_muneRParent.GetValue(tbody) as Transform;
				Transform muneRChild = f_muneRChild.GetValue(tbody) as Transform;
				Transform muneLSub = f_muneLSub.GetValue(tbody) as Transform;
				Transform muneRSub = f_muneRSub.GetValue(tbody) as Transform;
				if (muneLChild && muneLParent && muneRChild && muneRParent) {
					muneLChild.localPosition = muneLSub.localPosition;
					muneLParent.localPosition = muneLSub.localPosition;
					muneRChild.localPosition = muneRSub.localPosition;
					muneRParent.localPosition = muneRSub.localPosition;
				}

				// ignoreHeadBonesに登録されている場合はヒラエルキーを辿って頭のツリーを無視
				if (name != null) {
					if (!(ignoreHeadBones.Contains(name) && CMT.SearchObjObj(maid.body0.m_Bones.transform.Find("Bip01"), linkT))) {
						linkT.localScale = scl;
					}
					linkT.localPosition = pos;
				}

				if (name != null) {
					if (name == "Eyepos_L") {
						tEyePosL = linkT;
					} else if (name == "Eyepos_R") {
						tEyePosR = linkT;
					}
				}
			}

			// 目のサイズ・角度変更
			// EyeScaleRotate : 目のサイズと角度変更する CM3D.MaidVoicePich.Plugin.cs の追加メソッド
			// http://pastebin.com/DBuN5Sws
			// その１>>923
			// http://jbbs.shitaraba.net/bbs/read.cgi/game/55179/1438196715/923
			if (tEyePosL != null) {
				Transform linkT = tEyePosL;
				Vector3 localCenter = linkT.localPosition + (new Vector3(0f, eyeAngY, eyeAngX)); // ローカル座標系での回転中心位置
				Vector3 worldCenter = linkT.parent.TransformPoint(localCenter);         // ワールド座標系での回転中心位置
				Vector3 localAxis = new Vector3(-1f, 0f, 0f);                       // ローカル座標系での回転軸
				Vector3 worldAxis = linkT.TransformDirection(localAxis);               // ワールド座標系での回転軸

				linkT.localRotation = new Quaternion(-0.00560432f, -0.001345155f, 0.06805823f, 0.9976647f);    // 初期の回転量
				linkT.RotateAround(worldCenter, worldAxis, eyeAngAngle);
			}
			if (tEyePosR != null) {
				Transform linkT = tEyePosR;
				Vector3 localCenter = linkT.localPosition + (new Vector3(0f, eyeAngY, -eyeAngX));    // ローカル座標系での回転中心位置
				Vector3 worldCenter = linkT.parent.TransformPoint(localCenter);             // ワールド座標系での回転中心位置
				Vector3 localAxis = new Vector3(-1f, 0f, 0f);                           // ローカル座標系での回転軸
				Vector3 worldAxis = linkT.TransformDirection(localAxis);                   // ワールド座標系での回転軸

				linkT.localRotation = new Quaternion(0.9976647f, 0.06805764f, -0.001350592f, -0.005603582f);   // 初期の回転量
				linkT.RotateAround(worldCenter, worldAxis, -eyeAngAngle);
			}

			// COM3D2追加処理
			// ボーンポジション系
			var listBoneMorphPos = Helper.GetInstanceField(typeof(BoneMorph_), boneMorph_, "m_listBoneMorphPos");
			var ienumPos = listBoneMorphPos as IEnumerable;

			System.Reflection.Assembly asmPos = System.Reflection.Assembly.GetAssembly(typeof(BoneMorph_));
			Type listBoneMorphPosType = listBoneMorphPos.GetType();
			Type boneMorphPosType = asmPos.GetType("BoneMorph_+BoneMorphPos");

			foreach (object o in ienumPos) {
				string strPropName = (string)Helper.GetInstanceField(boneMorphPosType, o, "strPropName");
				Transform trs = (Transform)Helper.GetInstanceField(boneMorphPosType, o, "trBone");
				Vector3 defPos = (Vector3)Helper.GetInstanceField(boneMorphPosType, o, "m_vDefPos");
				Vector3 addMin = (Vector3)Helper.GetInstanceField(boneMorphPosType, o, "m_vAddMin");
				Vector3 addMax = (Vector3)Helper.GetInstanceField(boneMorphPosType, o, "m_vAddMax");

				if (strPropName == "Nosepos")
					trs.localPosition = Lerp(addMin, defPos, addMax, (float)boneMorph_.POS_Nose, sliderScale);
				else if (strPropName == "MayuY") {
					trs.localPosition = Lerp(addMin, defPos, addMax, (float)boneMorph_.POS_MayuY, sliderScale);
				} else if (strPropName == "EyeBallPosYL" || strPropName == "EyeBallPosYR") {
					trs.localPosition = Lerp(addMin, defPos, addMax, (float)boneMorph_.EyeBallPosY, sliderScale);
				} else if (strPropName == "Mayupos_L" || strPropName == "Mayupos_R") {
					Vector3 vector3_1 = Lerp(addMin, defPos, addMax, (float)boneMorph_.POS_MayuY, sliderScale);
					float x1 = addMin.x;
					addMin.x = addMax.x;
					addMax.x = x1;
					Vector3 vector3_2 = Lerp(addMin, defPos, addMax, (float)boneMorph_.POS_MayuX, sliderScale);
					float x3 = vector3_2.x + vector3_1.x - defPos.x;
					trs.localPosition = new Vector3(x3, vector3_1.y, vector3_2.z);
				}
			}

			// ボーンスケール系
			var listBoneMorphScl = Helper.GetInstanceField(typeof(BoneMorph_), boneMorph_, "m_listBoneMorphScl");
			var ienumScl = listBoneMorphScl as IEnumerable;

			System.Reflection.Assembly asmScl = System.Reflection.Assembly.GetAssembly(typeof(BoneMorph_));
			Type listBoneMorphSclType = listBoneMorphScl.GetType();
			Type boneMorphSclType = asmScl.GetType("BoneMorph_+BoneMorphScl");

			foreach (object o in ienumScl) {
				string strPropName = (string)Helper.GetInstanceField(boneMorphSclType, o, "strPropName");
				Transform trs = (Transform)Helper.GetInstanceField(boneMorphSclType, o, "trBone");
				Vector3 defScl = (Vector3)Helper.GetInstanceField(boneMorphSclType, o, "m_vDefScl");
				Vector3 addMin = (Vector3)Helper.GetInstanceField(boneMorphSclType, o, "m_vAddMin");
				Vector3 addMax = (Vector3)Helper.GetInstanceField(boneMorphSclType, o, "m_vAddMax");

				if (strPropName == "Earscl_L" || strPropName == "Earscl_R") {
					trs.localScale = Lerp(addMin, defScl, addMax, (float)boneMorph_.SCALE_Ear, sliderScale);
				} else if (strPropName == "Nosescl") {
					trs.localScale = Lerp(addMin, defScl, addMax, (float)boneMorph_.SCALE_Nose, sliderScale);
				} else if (strPropName == "EyeBallSclXL" || strPropName == "EyeBallSclXR") {
					Vector3 localScale = trs.localScale;
					localScale.z = Lerp(addMin, defScl, addMax, (float)boneMorph_.EyeBallSclX, sliderScale).z;
					trs.localScale = localScale;
				} else if (strPropName == "EyeBallSclYL" || strPropName == "EyeBallSclYR") {
					Vector3 localScale = trs.localScale;
					localScale.y = Lerp(addMin, defScl, addMax, (float)boneMorph_.EyeBallSclY, sliderScale).y;
					trs.localScale = localScale;
				}
			}

			// ボーンローテーション系
			var listBoneMorphRot = Helper.GetInstanceField(typeof(BoneMorph_), boneMorph_, "m_listBoneMorphRot");
			var ienumRot = listBoneMorphRot as IEnumerable;

			System.Reflection.Assembly asmRot = System.Reflection.Assembly.GetAssembly(typeof(BoneMorph_));
			Type listBoneMorphRotType = listBoneMorphRot.GetType();
			Type boneMorphRotType = asmRot.GetType("BoneMorph_+BoneMorphRotatio");

			foreach (object o in ienumRot) {
				string strPropName = (string)Helper.GetInstanceField(boneMorphRotType, o, "strPropName");
				Transform trs = (Transform)Helper.GetInstanceField(boneMorphRotType, o, "trBone");
				Quaternion defRot = (Quaternion)Helper.GetInstanceField(boneMorphRotType, o, "m_vDefRotate");
				Quaternion addMin = (Quaternion)Helper.GetInstanceField(boneMorphRotType, o, "m_vAddMin");
				Quaternion addMax = (Quaternion)Helper.GetInstanceField(boneMorphRotType, o, "m_vAddMax");

				if (strPropName == "Earrot_L" || strPropName == "Earrot_R") {
					trs.localRotation = RotLerp(addMin, defRot, addMax, (float)boneMorph_.ROT_Ear, sliderScale);
				} else if (strPropName == "Mayurot_L" || strPropName == "Mayurot_R") {
					trs.localRotation = RotLerp(addMin, defRot, addMax, (float)boneMorph_.ROT_Mayu, sliderScale);
				}
			}
		}

		public static Vector3 Lerp(Vector3 min, Vector3 def, Vector3 max, float t, float sliderScale) {
			if ((double)t >= 0.5) {
				Vector3 n1 = max + (max - def) * (sliderScale - 1f) * 2;
				float f = (t - 0.5f) * (1f / (sliderScale * 2.0f - 1f)) * 2.0f;
				return Vector3.Lerp(def, n1, f);
			} else {
				Vector3 n0 = min - (def - min) * (sliderScale - 1f) * 2;
				float f = (t + sliderScale - 1f) * (1f / (sliderScale * 2.0f - 1f)) * 2.0f;
				return Vector3.Lerp(n0, def, f);
			}
		}

		public static Quaternion RotLerp(Quaternion min, Quaternion def, Quaternion max, float t, float sliderScale) {
			float t1 = (double)t > 0.5 ? (float)(((double)t - 0.5) / 0.5) : t / 0.5f;
			if ((double)t <= 0.5)
				return Quaternion.LerpUnclamped(min, def, t1);
			return Quaternion.LerpUnclamped(def, max, t1);

		}

		private void SetBoneScaleFromList(Dictionary<string, Vector3> dictionary, Maid maid, string[][] _boneAndPropNameList) {
			foreach (var item in _boneAndPropNameList) {
				if (item[0].Contains("?")) {
					string boneNameL = item[0].Replace('?', 'L');
					string boneNameR = item[0].Replace('?', 'R');
					SetBoneScale(dictionary, boneNameL, maid, item[1]);
					dictionary[boneNameR] = dictionary[boneNameL];
				} else {
					SetBoneScale(dictionary, item[0], maid, item[1]);
				}
			}
		}

		void SetBoneScale(Dictionary<string, Vector3> dictionary, string boneName, Maid maid, string propName) {
			dictionary[boneName] = new Vector3(
	ExSaveData.GetFloat(maid, PluginName, propName + ".height", 1f),
	ExSaveData.GetFloat(maid, PluginName, propName + ".depth", 1f),
	ExSaveData.GetFloat(maid, PluginName, propName + ".width", 1f));
		}

		private string getHiraerchy(Transform t) {
			if (!t) {
				return string.Empty;
			}
			string hiraerchy = "/" + t.name;
			while (t.parent) {
				t = t.parent;
				hiraerchy = "/" + t.name + hiraerchy;
			}

			return hiraerchy;
		}

		// 動作していない古い設定を削除する
		static void CleanupExSave() {
			string[] obsoleteSettings = {
				"WIDESLIDER.enable", "PROPSET_OFF.enable", "LIPSYNC_OFF.enable",
				"HYOUJOU_OFF.enable", "EYETOCAMERA_OFF.enable", "MUHYOU.enable",
				"FARMFIX.enable", "EYEBALL.enable", "EYE_ANG.enable",
				"PELSCL.enable", "SKTSCL.enable", "THISCL.enable", "THIPOS.enable",
				"PELVIS.enable", "FARMFIX.enable", "SPISCL.enable",
				"S0ASCL.enable", "S1_SCL.enable", "S1ASCL.enable",
				"FACE_OFF.enable", "FACEBLEND_OFF.enable",

                // 以下0.2.4で廃止
                "FACE_ANIME_SPEED",
				"MABATAKI_SPEED",
				"PELVIS", "PELVIS.x", "PELVIS.y", "PELVIS.z",
			};
			CharacterMgr cm = GameMain.Instance.CharacterMgr;
			for (int i = 0, n = cm.GetStockMaidCount(); i < n; i++) {
				Maid maid = cm.GetStockMaid(i);
				foreach (string s in obsoleteSettings) {
					ExSaveData.Remove(maid, PluginName, s);
				}

				{
					string fname = ExSaveData.Get(maid, PluginName, "SLIDER_TEMPLATE", null);
					if (string.IsNullOrEmpty(fname)) {
						ExSaveData.Set(maid, PluginName, "SLIDER_TEMPLATE", "UnityInjector/Config/MaidVoicePitchSlider.xml", true);
					}
				}
			}

			string[] obsoleteGlobalSettings = {
				"TEST_GLOBAL_KEY"
			};
			foreach (string s in obsoleteGlobalSettings) {
				ExSaveData.GlobalRemove(PluginName, s);
			}
		}
	}
}
