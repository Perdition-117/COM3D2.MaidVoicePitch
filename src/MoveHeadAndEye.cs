using UnityEngine;
using static CM3D2.MaidVoicePitch.Plugin.MaidVoicePitch;

namespace MaidVoicePitch;

internal static class TBodyMoveHeadAndEye {
	public class BackupValues : MonoBehaviour {
		public TBody Body;
		public bool Reset = true;
		public Quaternion Rotation = Quaternion.identity;
		public Quaternion LeftEyeRotation = Quaternion.identity;
		public Quaternion RightEyeRotation = Quaternion.identity;

		public void Update() {
			// 前回よりも角度の差が大きい場合はリセットする
			if (Body?.maid) {
				//本体側更新によりjbMuneLの取得方法変更 
				var jbMuneL = Body.jbMuneL ?? Body.jbMuneL;
				if (jbMuneL.boWarpInit) {
					Reset = true;
				}
			}
		}
	}

	public static void MoveHeadAndEye(TBody body) {
		if (body.trsHead == null) {
			return;
		}
		if (GameMain.Instance.MainCamera == null) {
			return;
		}

		try {
			var bParamHeadTrack = false;
			var maid = body.maid;
			if (maid != null) {
				bParamHeadTrack = GetBooleanProperty(maid, "HEAD_TRACK", false);
			}

			var headEulerAngle = body.HeadEulerAngle;
			var headEulerAngleG = body.HeadEulerAngleG;
			var eyeEulerAngle = body.EyeEulerAngle;

			// eyeTargetWorldPos：ワールド座標系での視線のターゲット位置
			var eyeTargetWorldPosition = UpdateEyeTargetPosition(body);

			// COM3D2の追加処理
			if (!body.boLockHeadAndEye) {
				// HeadToCamPer：最終的に顔がカメラを向く度合い
				//  0 なら元の頭の向き、1 ならカメラの向き
				if (body.boHeadToCam) {
					body.HeadToCamPer += Time.deltaTime * body.HeadToCamFadeSpeed;
				} else {
					body.HeadToCamPer -= Time.deltaTime * body.HeadToCamFadeSpeed;
				}
				body.HeadToCamPer = Mathf.Clamp01(body.HeadToCamPer);

				body.boChkEye = false;

				if (bParamHeadTrack) {
					var externalValues = body.GetOrAddComponent<BackupValues>();
					externalValues.Body = body;
					MoveHead(externalValues, body, eyeTargetWorldPosition);
					MoveEyes(externalValues, body, eyeTargetWorldPosition);
				} else {
					OriginalMoveHead(body, eyeTargetWorldPosition, ref headEulerAngle, ref headEulerAngleG);
					OriginalMoveEyes(body, eyeTargetWorldPosition, ref eyeEulerAngle);
				}
			}

			body.HeadEulerAngle = headEulerAngle;
			body.HeadEulerAngleG = headEulerAngleG;
			body.EyeEulerAngle = eyeEulerAngle;
		} catch (Exception ex) {
			LogError(ex);
		}
	}

	// 元の MoveHeadAndEye 相当の処理
	private static void OriginalMoveEyes(TBody body, Vector3 eyeTargetPosition, ref Vector3 eyeEulerAngle) {
		if (body.boMAN || body.trsEyeL == null || body.trsEyeR == null) {
			return;
		}

		// 目の追従処理
		if (body.boEyeToCam && body.boChkEye) {
			var direction = Quaternion.Inverse(body.trsHead.rotation) * (eyeTargetPosition - body.trsHead.position);
			var quaternion = Quaternion.FromToRotation(Vector3.up, direction);
			var eulerAngles = NormalizeEulerAngles(quaternion.eulerAngles);

			var num = 0.5f;
			if (body.boEyeSorashi) {
				num = 0.05f;
			}
			eyeEulerAngle = eyeEulerAngle * (1f - num) + eulerAngles * num;
		} else {
			eyeEulerAngle *= 0.95f;
		}

		body.trsEyeL.localRotation = body.quaDefEyeL * Quaternion.Euler(0.0f, eyeEulerAngle.x * -0.2f + body.m_editYorime, eyeEulerAngle.z * -0.1f);
		body.trsEyeR.localRotation = body.quaDefEyeR * Quaternion.Euler(0.0f, eyeEulerAngle.x * 0.2f + body.m_editYorime, eyeEulerAngle.z * 0.1f);
	}

	private static Vector3 UpdateEyeTargetPosition(TBody body) {
		// eyeTargetWorldPos：ワールド座標系での視線のターゲット位置
		Vector3 eyeTargetWorldPos;
		if (body.trsLookTarget == null) {
			eyeTargetWorldPos = body.trsHead.TransformPoint(body.offsetLookTarget);
			if (body.boEyeSorashi) {
				var mainCamera = GameMain.Instance.MainCamera;
				// num : 顔の前方と顔→カメラベクトルの内積。1.0に近ければ、カメラが顔の正面にある
				var num = Vector3.Dot(
					(eyeTargetWorldPos - body.trsHead.position).normalized,
					(mainCamera.transform.position - body.trsHead.position).normalized);

				if (body.EyeSorashiCnt > 0) {
					++body.EyeSorashiCnt;
					if (body.EyeSorashiCnt > 200) {
						body.EyeSorashiCnt = 0;
					}
				}

				// カメラが顔の前方にあり、なおかつ前回の変更から 200 フレーム経過しているなら、新しい「前方」を決める
				if (num > 0.9f && body.EyeSorashiCnt == 0) {
					body.offsetLookTarget = !body.EyeSorashiTgl ? new(-0.6f, 1f, 0.6f) : new(-0.5f, 1f, -0.7f);
					body.EyeSorashiTgl = !body.EyeSorashiTgl;
					body.EyeSorashiCnt = 1;
				}
			}
		} else {
			eyeTargetWorldPos = body.trsLookTarget.position;
		}
		return eyeTargetWorldPos;
	}

	private static void OriginalMoveHead(TBody body, Vector3 eyeTargetPosition, ref Vector3 headEulerAngle, ref Vector3 headEulerAngleG) {
		// eulerAngles：顔の正面向きのベクトルから見た、視線ターゲットまでの回転量
		Vector3 eulerAngles;
		{
			// direction：顔からターゲットを見た向き（顔の座標系）
			var direction = Quaternion.Inverse(body.trsNeck.rotation) * (eyeTargetPosition - body.trsNeck.position);
			// quaternion：(0,1,0) (顔の正面向きのベクトル) から見たときの、direction までの回転量
			var quaternion = Quaternion.FromToRotation(Vector3.up, direction);

			eulerAngles = NormalizeEulerAngles(quaternion.eulerAngles);
		}

		if (body.boHeadToCamInMode) {
			// 追従範囲外かどうかを判定
			if (-80.0f >= eulerAngles.x || eulerAngles.x >= 80.0f || -50.0f >= eulerAngles.z || eulerAngles.z >= 60.0f) {
				body.boHeadToCamInMode = false;
			}
		} else {
			// 追従範囲内かどうかを判定
			if (-60.0f < eulerAngles.x && eulerAngles.x < 60.0f && -40.0f < eulerAngles.z && eulerAngles.z < 50.0f) {
				body.boHeadToCamInMode = true;
			}
		}

		if (body.boHeadToCamInMode) {
			// 追従モード
			body.boChkEye = true;
			var num = 0.3f;

			if (eulerAngles.x > headEulerAngle.x + 10.0f) {
				headEulerAngleG.x += num;
			} else if (eulerAngles.x < headEulerAngle.x - 10.0f) {
				headEulerAngleG.x -= num;
			} else {
				headEulerAngleG.x *= 0.95f;
			}

			if (eulerAngles.z > headEulerAngle.z + 10.0f) {
				headEulerAngleG.z += num;
			} else if (eulerAngles.z < headEulerAngle.z - 10.0f) {
				headEulerAngleG.z -= num;
			} else {
				headEulerAngleG.z *= 0.95f;
			}
		} else {
			// 自由モード
			var num = 0.1f;
			if (0.0f > headEulerAngle.x + 10.0) {
				headEulerAngleG.x += num;
			}
			if (0.0f < headEulerAngle.x - 10.0f) {
				headEulerAngleG.x -= num;
			}
			if (0.0f > headEulerAngle.z + 10.0f) {
				headEulerAngleG.z += num;
			}
			if (0.0f < headEulerAngle.z - 10.0f) {
				headEulerAngleG.z -= num;
			}
		}

		headEulerAngleG *= 0.95f;
		headEulerAngle += headEulerAngleG;

		var uScale = 0.4f;
		body.trsHead.localRotation = Quaternion.Slerp(
			body.trsHead.localRotation,
			body.quaDefHead * Quaternion.Euler(headEulerAngle.x * uScale, 0.0f, headEulerAngle.z * uScale),
			UTY.COSS(body.HeadToCamPer));
	}

	// 新しい MoveHeadAndEye
	private static void MoveEyes(BackupValues backupValues, TBody body, Vector3 eyeTargetPosition) {
		backupValues.Rotation = body.trsHead.rotation;

		if (body.boMAN || body.trsEyeL == null || body.trsEyeR == null) {
			return;
		}

		body.boChkEye = false;
		{
			var maidData = PluginSaveData.GetMaidData(body.maid);
			float GetValue(string name, float defaultValue = 0f) => maidData.GetFloat($"EYE_TRACK.{name}", defaultValue);

			var paramEyeAngle = maidData.GetFloat("EYE_ANG.angle", 0f);
			paramEyeAngle = Mathf.Clamp(paramEyeAngle, -180f, 180f);
			var paramSpeed = GetValue("speed", 0.05f);

			var paramInside = GetValue("inside", 60f);
			var paramOutside = GetValue("outside", 60f);
			var paramAbove = GetValue("above", 40f);
			var paramBelow = GetValue("below", 20f);
			var paramBehind = GetValue("behind", 170f);
			var paramOffsetX = GetValue("ofsx");
			var paramOffsetY = GetValue("ofsy");

			if (!body.boEyeToCam) {
				// 視線を正面に戻す
				eyeTargetPosition = body.trsHead.TransformPoint(Vector3.up * 1000.0f);
			}

			{
				var defaultRotation = body.quaDefEyeL * Quaternion.Euler(paramEyeAngle, -paramOffsetX, -paramOffsetY);
				var rotation = GetEyeRotation(body.trsEyeL, backupValues.LeftEyeRotation, paramBelow, paramAbove);
				var originalRotation = rotation;
				body.trsEyeL.localRotation = rotation * defaultRotation;
				backupValues.LeftEyeRotation = originalRotation;
			}

			{
				var defaultRotation = body.quaDefEyeR * Quaternion.Euler(-paramEyeAngle, -paramOffsetX, paramOffsetY);
				var rotation = GetEyeRotation(body.trsEyeR, backupValues.RightEyeRotation, paramAbove, paramBelow);
				var originalRotation = rotation;
				body.trsEyeR.localRotation = rotation * defaultRotation;
				backupValues.RightEyeRotation = originalRotation;
			}

			Quaternion GetEyeRotation(Transform eye, Quaternion originalRotation, float paramAbove, float paramBelow) {
				var rotation = CalcNewRotation(
					Vector3.up,
					Vector3.forward,
					Vector3.left,
					paramOutside,
					paramInside,
					paramAbove,
					paramBelow,
					paramBehind,
					eye.parent.rotation,
					eye.position,
					eyeTargetPosition,
					eyeTargetPosition
				);
				rotation = Quaternion.Inverse(eye.parent.rotation) * rotation;
				rotation = Quaternion.Slerp(Quaternion.identity, rotation, 0.2f);     // 眼球モデルの中心に、眼球のトランスフォームの原点が無いため、ごまかしている
				rotation = Quaternion.Slerp(originalRotation, rotation, paramSpeed);
				return rotation;
			}
		}
	}

	private static void MoveHead(BackupValues backupValues, TBody body, Vector3 eyeTargetPosition) {
		var maidData = PluginSaveData.GetMaidData(body.maid);
		float GetValue(string name, float defaultValue = 0f) => maidData.GetFloat($"HEAD_TRACK.{name}", defaultValue);

		var paramSpeed = GetValue("speed", 0.05f);
		var paramLateral = GetValue("lateral", 60.0f);
		var paramAbove = GetValue("above", 40.0f);
		var paramBelow = GetValue("below", 20.0f);
		var paramBehind = GetValue("behind", 170.0f);
		var paramOffsetX = GetValue("ofsx");
		var paramOffsetY = GetValue("ofsy");
		var paramOffsetZ = GetValue("ofsz");

		// 正面の角度
		var frontangle = GetValue("frontangle");
		var frontquaternion = Quaternion.Euler(0f, 0f, frontangle);

		// モーションにしたがっている場合 (HeadToCamPer=0f) はオフセットをつけない
		paramOffsetX *= body.HeadToCamPer;
		paramOffsetY *= body.HeadToCamPer;
		paramOffsetZ *= body.HeadToCamPer;

		var basePosition = body.trsHead.position;
		var baseRotation = body.trsNeck.rotation * frontquaternion;
		var targetLocal = Quaternion.Inverse(baseRotation) * (eyeTargetPosition - basePosition);

		//追従割合
		var localAngle = Quaternion.FromToRotation(Vector3.up, targetLocal);
		var localEuler = localAngle.eulerAngles;
		var headRateUp = GetValue("headrateup", 1f);
		var headRateDown = GetValue("headratedown", 1f);
		var headRateHorizontal = GetValue("headratehorizon", 1f);
		var dx = 0f;
		var dy = 0f;
		if (localEuler.x < 180f) {
			dx = localEuler.x * (headRateHorizontal - 1f);
		} else {
			dx = (localEuler.x - 360f) * (headRateHorizontal - 1f);
		}
		if (localEuler.z < 180f) {
			dy = localEuler.z * (headRateUp - 1f);
		} else {
			dy = (localEuler.z - 360f) * (headRateDown - 1f);
		}

		targetLocal = Quaternion.Euler(dx, 0f, dy) * targetLocal;
		targetLocal = Quaternion.Euler(paramOffsetX, 0f, paramOffsetY) * targetLocal;
		var targetWorld = (baseRotation * targetLocal) + basePosition;

		// 顔が向くべき方向を算出
		var newHeadRotationWorld = CalcNewRotation(
			Vector3.right,
			Vector3.forward,
			Vector3.up,
			paramLateral,
			paramLateral,
			paramAbove,
			paramBelow,
			paramBehind,
			baseRotation,
			basePosition,
			targetWorld,
			eyeTargetPosition);

		newHeadRotationWorld *= Quaternion.Euler(0f, paramOffsetZ, 0f);

		// TBody.HeadToCamPer を「正面向き度合い」として加味する
		newHeadRotationWorld = Quaternion.Slerp(body.trsHead.rotation, newHeadRotationWorld, body.HeadToCamPer);

		// 角度
		var inclineRate = GetValue("inclinerate");
		var newHeadRotationLocal = Quaternion.Inverse(baseRotation) * newHeadRotationWorld;
		var newHeadRotationEulerLocal = newHeadRotationLocal.eulerAngles;
		if (newHeadRotationEulerLocal.x > 180f) {
			newHeadRotationEulerLocal = new(
				newHeadRotationEulerLocal.x - 360f,
				newHeadRotationEulerLocal.y,
				newHeadRotationEulerLocal.z);
		}
		newHeadRotationEulerLocal = new(
			newHeadRotationEulerLocal.x,
			newHeadRotationEulerLocal.y + (newHeadRotationEulerLocal.x * inclineRate),
			newHeadRotationEulerLocal.z);
		newHeadRotationLocal = Quaternion.Euler(newHeadRotationEulerLocal);
		newHeadRotationWorld = baseRotation * newHeadRotationLocal;

		// 前回の回転よりも差が大きすぎる場合はリセットする
		if (backupValues.Reset) {
			backupValues.Reset = false;
			backupValues.Rotation = body.trsHead.rotation;
			paramSpeed = 0f;
		}

		// モーションにしたがっている場合 (HeadToCamPer=0f) は補間しない
		paramSpeed = Mathf.Lerp(1f, paramSpeed, body.HeadToCamPer);

		// 実際の回転
		body.trsHead.rotation = Quaternion.Slerp(backupValues.Rotation, newHeadRotationWorld, paramSpeed);
	}

	private static Quaternion CalcNewRotation(
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
		Vector3 originalTargetPosition
	) {
		// 「正面」の方向 (TBody.trsNeck座標系)
		var headForwardNeck = forwardVector;

		// 「正面」の方向（ワールド座標系）
		var headForwardWorld = neckRotation * headForwardNeck;

		// headから視線目標点への方向 (ワールド座標系。正規化済み)
		var headToTargetDirectionWorld = (targetPosition - headPosition).normalized;
		var originalHeadToTargetDirectionWorld = (originalTargetPosition - headPosition).normalized;

		// 現在の「正面」から目標までの角度
		var currentAngle = Vector3.Angle(headForwardWorld, headToTargetDirectionWorld);
		var originalAngle = Vector3.Angle(headForwardWorld, originalHeadToTargetDirectionWorld);

		// 視線目標点が首から見て真後ろ付近なら、目標方向は正面にする
		if (originalAngle >= paramBehind) {
			headToTargetDirectionWorld = headForwardWorld;
			currentAngle = Vector3.Angle(headForwardWorld, headToTargetDirectionWorld);
		}

		// headから視線目標点への方向 (TBody.trsNeck座標系。正規化済み)
		var headToTargetDirectionNeck = Quaternion.Inverse(neckRotation) * headToTargetDirectionWorld;

		// headForward(正面)の向きからheadToTargetDirectionの向きへの回転 (TBody.trsNeck座標系)
		var headForwardToTargetRotationNeck = Quaternion.FromToRotation(headForwardNeck, headToTargetDirectionNeck);

		//  rad : neck座標系の「正面」から見た（投影した）時の目標点の向き (trsNeck XZ 平面)
		var dx = Vector3.Dot(headToTargetDirectionNeck, rightVector);
		var dy = Vector3.Dot(headToTargetDirectionNeck, upVector);
		var deg = NormalizeAngle(Mathf.Rad2Deg * Mathf.Atan2(dy, dx));

		// 向きに応じた限界角度を算出
		var maxAngle = GetMaxAngle(deg, paramLeft, paramRight, paramAbove, paramBelow);

		// 限界角度を超えているか？
		if (currentAngle > maxAngle) {
			// 超えているので補正する
			var a = maxAngle / currentAngle;
			headForwardToTargetRotationNeck = Quaternion.Slerp(Quaternion.identity, headForwardToTargetRotationNeck, a);
		}

		return neckRotation * headForwardToTargetRotationNeck;
	}

	private static float NormalizeAngle(float angle) {
		if (angle >= 180.0f) {
			angle -= 360.0f;
		} else if (angle < -180.0f) {
			angle += 360.0f;
		}
		return angle;
	}

	private static Vector3 NormalizeEulerAngles(Vector3 eulerAngles) {
		return new(
			NormalizeAngle(eulerAngles.x),
			NormalizeAngle(eulerAngles.y),
			NormalizeAngle(eulerAngles.z));
	}

	private static float GetMaxAngle(float deg, float leftMaxAng, float rightMaxAng, float aboveMaxAng, float belowMaxAng) {
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
