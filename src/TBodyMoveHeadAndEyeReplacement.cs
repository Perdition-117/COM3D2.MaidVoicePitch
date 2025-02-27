using CM3D2.MaidVoicePitch.Plugin;
using UnityEngine;

class TBodyMoveHeadAndEye {
	public class ExternalValues : MonoBehaviour {
		public TBody tbody;
		public Quaternion prevQuat = Quaternion.identity;
		public Quaternion prevLeftEyeQuat = Quaternion.identity;
		public Quaternion prevRightEyeQuat = Quaternion.identity;

		public Quaternion prevHeadQuat = Quaternion.identity;
		public bool bReset = true;

		public void Update() {
			// 前回よりも角度の差が大きい場合はリセットする
			var maid = tbody?.maid;
			if (maid != null) {
				//本体側更新によりjbMuneLの取得方法変更 
				var jbMuneL = tbody.jbMuneL ?? tbody.jbMuneL;
				if (jbMuneL.boWarpInit) {
					bReset = true;
				}
			}
		}
	}

	public void Callback(TBody tbody) {
		var that = tbody;

		if (that.trsHead == null) {
			return;
		}
		var mainCamera = GameMain.Instance.MainCamera;
		if (mainCamera == null) {
			return;
		}

		try {
			var bParamHeadTrack = false;
			var maid = tbody.maid;
			if (maid != null) {
				bParamHeadTrack = MaidVoicePitch.GetBooleanProperty(maid, "HEAD_TRACK", false);
			}

			var thatHeadEulerAngle = that.HeadEulerAngle;
			var thatHeadEulerAngleG = that.HeadEulerAngleG;
			var thatEyeEulerAngle = that.EyeEulerAngle;

			if (bParamHeadTrack) {
				var externalValues = tbody.GetOrAddComponent<ExternalValues>();
				externalValues.tbody = tbody;
				newTbodyMoveHeadAndEyeCallback2(externalValues, tbody, ref thatHeadEulerAngle, ref thatHeadEulerAngleG, ref thatEyeEulerAngle);
			} else {
				originalTbodyMoveHeadAndEyeCallback2(tbody, ref thatHeadEulerAngle, ref thatHeadEulerAngleG, ref thatEyeEulerAngle);
			}

			that.HeadEulerAngle = thatHeadEulerAngle;
			that.HeadEulerAngleG = thatHeadEulerAngleG;
			that.EyeEulerAngle = thatEyeEulerAngle;

		} catch (Exception ex) {
			MaidVoicePitch.LogError(ex);
		}
	}

	// 元の MoveHeadAndEye 相当の処理
	void originalTbodyMoveHeadAndEyeCallback2(TBody tbody, ref Vector3 thatHeadEulerAngle, ref Vector3 thatHeadEulerAngleG, ref Vector3 thatEyeEulerAngle) {
		var that = tbody;
		var mainCamera = GameMain.Instance.MainCamera;

		// eyeTargetWorldPos：ワールド座標系での視線のターゲット位置
		var eyeTargetWorldPos = updateEyeTargetPos(tbody);

		// COM3D2の追加処理
		if (tbody.boLockHeadAndEye) return;

		// HeadToCamPer：最終的に顔がカメラを向く度合い
		//  0 なら元の頭の向き、1 ならカメラの向き
		if (that.boHeadToCam) {
			that.HeadToCamPer += Time.deltaTime * that.HeadToCamFadeSpeed;
		} else {
			that.HeadToCamPer -= Time.deltaTime * that.HeadToCamFadeSpeed;
		}
		that.HeadToCamPer = Mathf.Clamp01(that.HeadToCamPer);

		that.boChkEye = false;

		originalMoveHead(tbody, ref thatHeadEulerAngle, ref thatHeadEulerAngleG, ref thatEyeEulerAngle, eyeTargetWorldPos);

		if (that.boMAN || that.trsEyeL == null || that.trsEyeR == null) {
			return;
		}

		// 目の追従処理
		if (that.boEyeToCam && that.boChkEye) {
			var toDirection2 = Quaternion.Inverse(that.trsHead.rotation) * (eyeTargetWorldPos - that.trsHead.position);
			var quaternion2 = new Quaternion();
			quaternion2.SetFromToRotation(Vector3.up, toDirection2);
			var eulerAngles2 = PluginHelper.NormalizeEulerAngles(quaternion2.eulerAngles);
			var view = Vector3.Normalize(eyeTargetWorldPos - that.trsEyeL.position);
			quaternion2.SetLookRotation(view, Vector3.up);
			var quaternion3 = quaternion2 * Quaternion.Euler(0.0f, 90f, 0.0f);

			var num = 0.5f;
			if (that.boEyeSorashi) {
				num = 0.05f;
			}
			thatEyeEulerAngle = thatEyeEulerAngle * (1f - num) + eulerAngles2 * num;
		} else {
			thatEyeEulerAngle *= 0.95f;
		}

		that.trsEyeL.localRotation = that.quaDefEyeL * Quaternion.Euler(0.0f, thatEyeEulerAngle.x * -0.2f + that.m_editYorime, thatEyeEulerAngle.z * -0.1f);
		that.trsEyeR.localRotation = that.quaDefEyeR * Quaternion.Euler(0.0f, thatEyeEulerAngle.x * 0.2f + that.m_editYorime, thatEyeEulerAngle.z * 0.1f);
	}

	Vector3 updateEyeTargetPos(TBody tbody) {
		var that = tbody;
		var mainCamera = GameMain.Instance.MainCamera;

		// eyeTargetWorldPos：ワールド座標系での視線のターゲット位置
		Vector3 eyeTargetWorldPos;
		if (that.trsLookTarget == null) {
			eyeTargetWorldPos = that.trsHead.TransformPoint(that.offsetLookTarget);
			if (that.boEyeSorashi) {
				// num : 顔の前方と顔→カメラベクトルの内積。1.0に近ければ、カメラが顔の正面にある
				var num = Vector3.Dot(
					(eyeTargetWorldPos - that.trsHead.position).normalized,
					(mainCamera.transform.position - that.trsHead.position).normalized);

				if (that.EyeSorashiCnt > 0) {
					++that.EyeSorashiCnt;
					if (that.EyeSorashiCnt > 200) {
						that.EyeSorashiCnt = 0;
					}
				}

				// カメラが顔の前方にあり、なおかつ前回の変更から 200 フレーム経過しているなら、新しい「前方」を決める
				if (num > 0.9f && that.EyeSorashiCnt == 0) {
					that.offsetLookTarget = !that.EyeSorashiTgl ? new Vector3(-0.6f, 1f, 0.6f) : new Vector3(-0.5f, 1f, -0.7f);
					that.EyeSorashiTgl = !that.EyeSorashiTgl;
					that.EyeSorashiCnt = 1;
				}
			}
		} else {
			eyeTargetWorldPos = that.trsLookTarget.position;
		}
		return eyeTargetWorldPos;
	}

	void originalMoveHead(TBody tbody, ref Vector3 thatHeadEulerAngle, ref Vector3 thatHeadEulerAngleG, ref Vector3 thatEyeEulerAngle, Vector3 eyeTargetWorldPos) {
		var that = tbody;
		//        CameraMain mainCamera = GameMain.Instance.MainCamera;

		// eulerAngles1：顔の正面向きのベクトルから見た、視線ターゲットまでの回転量
		Vector3 eulerAngles1;
		var quaternion1 = new Quaternion();
		{
			// toDirection1：顔からターゲットを見た向き（顔の座標系）
			var toDirection1 = Quaternion.Inverse(that.trsNeck.rotation) * (eyeTargetWorldPos - that.trsNeck.position);

			// quaternion1：(0,1,0) (顔の正面向きのベクトル) から見たときの、toDirection1 までの回転量
			quaternion1.SetFromToRotation(Vector3.up, toDirection1);

			eulerAngles1 = PluginHelper.NormalizeEulerAngles(quaternion1.eulerAngles);
		}

		if (that.boHeadToCamInMode) {
			// 追従範囲外かどうかを判定
			if (-80.0f >= eulerAngles1.x || eulerAngles1.x >= 80.0f || -50.0f >= eulerAngles1.z || eulerAngles1.z >= 60.0f) {
				that.boHeadToCamInMode = false;
			}
		} else {
			// 追従範囲内かどうかを判定
			if (-60.0f < eulerAngles1.x && eulerAngles1.x < 60.0f && -40.0f < eulerAngles1.z && eulerAngles1.z < 50.0f) {
				that.boHeadToCamInMode = true;
			}
		}

		if (that.boHeadToCamInMode) {
			// 追従モード
			that.boChkEye = true;
			var num = 0.3f;

			if (eulerAngles1.x > thatHeadEulerAngle.x + 10.0f) {
				thatHeadEulerAngleG.x += num;
			} else if (eulerAngles1.x < thatHeadEulerAngle.x - 10.0f) {
				thatHeadEulerAngleG.x -= num;
			} else {
				thatHeadEulerAngleG.x *= 0.95f;
			}

			if (eulerAngles1.z > thatHeadEulerAngle.z + 10.0f) {
				thatHeadEulerAngleG.z += num;
			} else if (eulerAngles1.z < thatHeadEulerAngle.z - 10.0f) {
				thatHeadEulerAngleG.z -= num;
			} else {
				thatHeadEulerAngleG.z *= 0.95f;
			}
		} else {
			// 自由モード
			var num = 0.1f;
			if (0.0f > thatHeadEulerAngle.x + 10.0) {
				thatHeadEulerAngleG.x += num;
			}
			if (0.0f < thatHeadEulerAngle.x - 10.0f) {
				thatHeadEulerAngleG.x -= num;
			}
			if (0.0f > thatHeadEulerAngle.z + 10.0f) {
				thatHeadEulerAngleG.z += num;
			}
			if (0.0f < thatHeadEulerAngle.z - 10.0f) {
				thatHeadEulerAngleG.z -= num;
			}
		}

		thatHeadEulerAngleG *= 0.95f;
		thatHeadEulerAngle += thatHeadEulerAngleG;

		var uScale = 0.4f;
		that.trsHead.localRotation = Quaternion.Slerp(
			that.trsHead.localRotation,
			that.quaDefHead * Quaternion.Euler(thatHeadEulerAngle.x * uScale, 0.0f, thatHeadEulerAngle.z * uScale),
			UTY.COSS(that.HeadToCamPer));
	}


	// 新しい MoveHeadAndEye
	void newTbodyMoveHeadAndEyeCallback2(ExternalValues externalValues, TBody tbody, ref Vector3 thatHeadEulerAngle, ref Vector3 thatHeadEulerAngleG, ref Vector3 thatEyeEulerAngle) {
		var that = tbody;
		var maid = tbody.maid;
		var mainCamera = GameMain.Instance.MainCamera;

		// eyeTarget_world：視線の目標位置（ワールド座標系）
		var eyeTarget_world = updateEyeTargetPos(tbody);

		// COM3D2の追加処理
		if (tbody.boLockHeadAndEye) return;

		// HeadToCamPer：最終的に顔がカメラを向く度合い
		//  0 なら元の頭の向き、1 ならカメラの向き
		if (that.boHeadToCam) {
			that.HeadToCamPer += Time.deltaTime * that.HeadToCamFadeSpeed;
		} else {
			that.HeadToCamPer -= Time.deltaTime * that.HeadToCamFadeSpeed;
		}
		that.HeadToCamPer = Mathf.Clamp01(that.HeadToCamPer);

		that.boChkEye = false;

		newMoveHead(externalValues, tbody, ref thatHeadEulerAngle, ref thatHeadEulerAngleG, ref thatEyeEulerAngle, eyeTarget_world);

		externalValues.prevQuat = that.trsHead.rotation;
		if (that.boMAN || that.trsEyeL == null || that.trsEyeR == null) {
			return;
		}

		that.boChkEye = false;
		{
			var maidData = MaidVoicePitch.PluginSaveData.GetMaidData(maid);

			var paramEyeAng = maidData.GetFloat("EYE_ANG.angle", 0f);
			paramEyeAng = Mathf.Clamp(paramEyeAng, -180f, 180f);
			var paramSpeed = maidData.GetFloat("EYE_TRACK.speed", 0.05f);

			var paramInside = maidData.GetFloat("EYE_TRACK.inside", 60f);
			var paramOutside = maidData.GetFloat("EYE_TRACK.outside", 60f);
			var paramAbove = maidData.GetFloat("EYE_TRACK.above", 40f);
			var paramBelow = maidData.GetFloat("EYE_TRACK.below", 20f);
			var paramBehind = maidData.GetFloat("EYE_TRACK.behind", 170f);
			var paramOfsX = maidData.GetFloat("EYE_TRACK.ofsx", 0f);
			var paramOfsY = maidData.GetFloat("EYE_TRACK.ofsy", 0f);
			var targetPosition = eyeTarget_world;

			if (!that.boEyeToCam) {
				// 視線を正面に戻す
				eyeTarget_world = that.trsHead.TransformPoint(Vector3.up * 1000.0f);
			}

			{
				var trsEye = that.trsEyeL;
				var defQuat = that.quaDefEyeL * Quaternion.Euler(paramEyeAng, -paramOfsX, -paramOfsY);
				var prevQuat = externalValues.prevLeftEyeQuat;

				var trsParent = trsEye.parent;
				var newRotation_world = CalcNewEyeRotation(
					paramOutside,
					paramInside,
					paramBelow,
					paramAbove,
					paramBehind,
					trsParent.rotation,
					trsEye.position,
					eyeTarget_world,
					eyeTarget_world
				);
				var q = Quaternion.Inverse(trsParent.rotation) * newRotation_world;
				q = Quaternion.Slerp(Quaternion.identity, q, 0.2f);     // 眼球モデルの中心に、眼球のトランスフォームの原点が無いため、ごまかしている
				q = Quaternion.Slerp(prevQuat, q, paramSpeed);
				prevQuat = q;
				trsEye.localRotation = q * defQuat;

				externalValues.prevLeftEyeQuat = prevQuat;
			}

			{
				var trsEye = that.trsEyeR;
				var defQuat = that.quaDefEyeR * Quaternion.Euler(-paramEyeAng, -paramOfsX, paramOfsY);
				var prevQuat = externalValues.prevRightEyeQuat;

				var trsParent = trsEye.parent;
				var newRotation_world = CalcNewEyeRotation(
					paramOutside,
					paramInside,
					paramAbove,
					paramBelow,
					paramBehind,
					trsParent.rotation,
					trsEye.position,
					eyeTarget_world,
					eyeTarget_world
				);
				var q = Quaternion.Inverse(trsParent.rotation) * newRotation_world;
				q = Quaternion.Slerp(Quaternion.identity, q, 0.2f);
				q = Quaternion.Slerp(prevQuat, q, paramSpeed);
				prevQuat = q;
				trsEye.localRotation = q * defQuat;

				externalValues.prevRightEyeQuat = prevQuat;
			}
		}
	}

	void newMoveHead(ExternalValues externalValues, TBody tbody, ref Vector3 thatHeadEulerAngle, ref Vector3 thatHeadEulerAngleG, ref Vector3 thatEyeEulerAngle, Vector3 eyeTarget_world) {
		var that = tbody;
		var maid = tbody.maid;

		var maidData = MaidVoicePitch.PluginSaveData.GetMaidData(maid);

		var paramSpeed = maidData.GetFloat("HEAD_TRACK.speed", 0.05f);
		var paramLateral = maidData.GetFloat("HEAD_TRACK.lateral", 60.0f);
		var paramAbove = maidData.GetFloat("HEAD_TRACK.above", 40.0f);
		var paramBelow = maidData.GetFloat("HEAD_TRACK.below", 20.0f);
		var paramBehind = maidData.GetFloat("HEAD_TRACK.behind", 170.0f);
		var paramOfsX = maidData.GetFloat("HEAD_TRACK.ofsx", 0.0f);
		var paramOfsY = maidData.GetFloat("HEAD_TRACK.ofsy", 0.0f);
		var paramOfsZ = maidData.GetFloat("HEAD_TRACK.ofsz", 0.0f);

		// 正面の角度
		var frontangle = maidData.GetFloat("HEAD_TRACK.frontangle", 0f);
		var frontquaternion = Quaternion.Euler(0f, 0f, frontangle);

		// モーションにしたがっている場合 (HeadToCamPer=0f) はオフセットをつけない
		paramOfsX *= that.HeadToCamPer;
		paramOfsY *= that.HeadToCamPer;
		paramOfsZ *= that.HeadToCamPer;

		var basePosition = that.trsHead.position;
		var baseRotation = that.trsNeck.rotation * frontquaternion;
		var target_local = Quaternion.Inverse(baseRotation) * (eyeTarget_world - basePosition);

		//追従割合
		var local_angle = Quaternion.FromToRotation(Vector3.up, target_local);
		var local_auler = local_angle.eulerAngles;
		var headrateup = maidData.GetFloat("HEAD_TRACK.headrateup", 1f);
		var headratedown = maidData.GetFloat("HEAD_TRACK.headratedown", 1f);
		var headratehorizon = maidData.GetFloat("HEAD_TRACK.headratehorizon", 1f);
		var dx = 0f;
		var dy = 0f;
		if (local_auler.x < 180f) {
			dx = local_auler.x * (headratehorizon - 1f);
		} else {
			dx = (local_auler.x - 360f) * (headratehorizon - 1f);
		}
		if (local_auler.z < 180f) {
			dy = local_auler.z * (headrateup - 1f);
		} else {
			dy = (local_auler.z - 360f) * (headratedown - 1f);
		}
		target_local = Quaternion.Euler(dx, 0f, dy) * target_local;

		target_local = Quaternion.Euler(paramOfsX, 0f, paramOfsY) * target_local;
		var target_world = (baseRotation * target_local) + basePosition;

		// 顔が向くべき方向を算出
		var newHeadRotation_world = CalcNewHeadRotation(
			paramLateral,
			paramAbove,
			paramBelow,
			paramBehind,
			baseRotation,
			basePosition,
			target_world,
			eyeTarget_world);

		newHeadRotation_world *= Quaternion.Euler(0f, paramOfsZ, 0f);

		// TBody.HeadToCamPer を「正面向き度合い」として加味する
		newHeadRotation_world = Quaternion.Slerp(that.trsHead.rotation, newHeadRotation_world, that.HeadToCamPer);

		// 角度
		var inclinerate = maidData.GetFloat("HEAD_TRACK.inclinerate", 0f);
		var newHeadRotation_Local = Quaternion.Inverse(baseRotation) * newHeadRotation_world;
		var newHeadRotationEulerLocal = newHeadRotation_Local.eulerAngles;
		if (newHeadRotationEulerLocal.x > 180f) {
			newHeadRotationEulerLocal = new(
				newHeadRotationEulerLocal.x - 360f,
				newHeadRotationEulerLocal.y,
				newHeadRotationEulerLocal.z);
		}
		newHeadRotationEulerLocal = new(
			newHeadRotationEulerLocal.x,
			newHeadRotationEulerLocal.y + (newHeadRotationEulerLocal.x * inclinerate),
			newHeadRotationEulerLocal.z);
		newHeadRotation_Local = Quaternion.Euler(newHeadRotationEulerLocal);
		newHeadRotation_world = baseRotation * newHeadRotation_Local;

		var s = paramSpeed;

		// 前回の回転よりも差が大きすぎる場合はリセットする
		if (externalValues.bReset) {
			externalValues.bReset = false;
			externalValues.prevQuat = that.trsHead.rotation;
			s = 0f;
		}

		// モーションにしたがっている場合 (HeadToCamPer=0f) は補間しない
		s = Mathf.Lerp(1f, s, that.HeadToCamPer);

		// 実際の回転
		that.trsHead.rotation = Quaternion.Slerp(externalValues.prevQuat, newHeadRotation_world, s);
	}

	static Quaternion CalcNewHeadRotation(float paramLateral, float paramAbove, float paramBelow, float paramBehind, Quaternion neckRotation, Vector3 headPosition, Vector3 targetPosition, Vector3 origTargetPosition) {
		return CalcNewRotation(
			Vector3.right,
			Vector3.forward,
			Vector3.up,
			paramLateral,
			paramLateral,
			paramAbove,
			paramBelow,
			paramBehind,
			neckRotation,
			headPosition,
			targetPosition,
			origTargetPosition);
	}

	static Quaternion CalcNewEyeRotation(float paramLeft, float paramRight, float paramAbove, float paramBelow, float paramBehind, Quaternion neckRotation, Vector3 headPosition, Vector3 targetPosition, Vector3 origTargetPosition) {
		return CalcNewRotation(
			Vector3.up,
			Vector3.forward,
			Vector3.left,
			paramLeft,
			paramRight,
			paramAbove,
			paramBelow,
			paramBehind,
			neckRotation,
			headPosition,
			targetPosition,
			origTargetPosition);
	}

	static Quaternion CalcNewRotation(
		Vector3 rightVector,
		Vector3 upVector,
		Vector3 forwardVector,
		float paramLeft,
		float paramRight,
		float paramAbove,
		float paramBelow,
		float paramBehind,
		Quaternion neckRotation,
		Vector3 headPosition,
		Vector3 targetPosition,
		Vector3 origTargetPosition
	) {
		Quaternion newHeadRotation_world;
		// 「正面」の方向 (TBody.trsNeck座標系)
		var headForward_neck = forwardVector;

		// 「正面」の方向（ワールド座標系）
		var headForward_world = neckRotation * headForward_neck;

		// headから視線目標点への方向 (ワールド座標系。正規化済み)
		var headToTargetDirection_world = (targetPosition - headPosition).normalized;
		var origHeadToTargetDirection_world = (origTargetPosition - headPosition).normalized;

		// 現在の「正面」から目標までの角度
		var currentAngle = Vector3.Angle(headForward_world, headToTargetDirection_world);
		var origCurrentAngle = Vector3.Angle(headForward_world, origHeadToTargetDirection_world);

		// 視線目標点が首から見て真後ろ付近なら、目標方向は正面にする
		if (origCurrentAngle >= paramBehind) {
			headToTargetDirection_world = headForward_world;
			currentAngle = Vector3.Angle(headForward_world, headToTargetDirection_world);
		}

		// headから視線目標点への方向 (TBody.trsNeck座標系。正規化済み)
		var headToTargetDirection_neck = Quaternion.Inverse(neckRotation) * headToTargetDirection_world;

		// headForward(正面)の向きからheadToTargetDirectionの向きへの回転 (TBody.trsNeck座標系)
		var headForwardToTargetRotation_neck = new Quaternion();
		headForwardToTargetRotation_neck.SetFromToRotation(headForward_neck, headToTargetDirection_neck);

		//  rad : neck座標系の「正面」から見た（投影した）時の目標点の向き (trsNeck XZ 平面)
		var dx = Vector3.Dot(headToTargetDirection_neck, rightVector);
		var dy = Vector3.Dot(headToTargetDirection_neck, upVector);
		var deg = PluginHelper.NormalizeAngle(Mathf.Rad2Deg * Mathf.Atan2(dy, dx));

		// 向きに応じた限界角度を算出
		var angMax = GetMaxAngle(deg, paramLeft, paramRight, paramAbove, paramBelow);

		// 限界角度を超えているか？
		if (currentAngle > angMax) {
			// 超えているので補正する
			var a = angMax / currentAngle;
			headForwardToTargetRotation_neck = Quaternion.Slerp(Quaternion.identity, headForwardToTargetRotation_neck, a);
		}

		newHeadRotation_world = neckRotation * headForwardToTargetRotation_neck;

		return newHeadRotation_world;
	}

	static float GetMaxAngle(float deg, float lateralMaxAng, float aboveMaxAng, float belowMaxAng) {
		return GetMaxAngle(deg, lateralMaxAng, lateralMaxAng, aboveMaxAng, belowMaxAng);
	}

	static float GetMaxAngle(float deg, float leftMaxAng, float rightMaxAng, float aboveMaxAng, float belowMaxAng) {
		float v0, v1, b;
		if (deg >= 90.0f) {
			v0 = leftMaxAng;        // 90度 : 左
			v1 = aboveMaxAng;       // 180度 : 上
			b = 90.0f;
		} else if (deg >= 0.0f && deg < 90.0f) {
			v0 = belowMaxAng;       // 0度 : 下
			v1 = leftMaxAng;        // 90度 : 左
			b = 0.0f;
		} else if (deg >= -90.0f && deg < 0.0f) {
			v0 = rightMaxAng;       // -90度 : 右
			v1 = belowMaxAng;       // 0度 : 下
			b = -90.0f;
		} else { // (deg < -90.0f)
			v0 = aboveMaxAng;       // -180度 : 上
			v1 = rightMaxAng;       // -90度 : 右
			b = -180.0f;
		}
		return Mathf.Lerp(v0, v1, (deg - b) / 90.0f);
	}
}
