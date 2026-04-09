using HarmonyLib;

namespace CM3D2.MaidVoicePitch.Managed.Callbacks {
	namespace BoneMorph_ {
		public class Blend {
			public delegate void Callback(global::BoneMorph_ that);

			public static Callbacks<Callback> Callbacks = new();

			[HarmonyPostfix]
			[HarmonyPatch(typeof(global::BoneMorph_), nameof(global::BoneMorph_.Blend))]
			public static void Invoke(global::BoneMorph_ __instance) {
				try {
					foreach (var callback in Callbacks.ArrayOfVals) {
						callback(__instance);
					}
				} catch (Exception e) {
					Plugin.MaidVoicePitch.LogError(e);
				}
			}
		}
	}

	namespace DynamicSkirtBone {
		public class PreUpdateSelf {
			public delegate void Callback(global::DynamicSkirtBone that);

			public static Callbacks<Callback> Callbacks = new();

			public static void Invoke(global::DynamicSkirtBone __instance) {
				try {
					foreach (var callback in Callbacks.ArrayOfVals) {
						callback(__instance);
					}
				} catch (Exception e) {
					Plugin.MaidVoicePitch.LogError(e);
				}
			}
		}

		public class PostUpdateSelf {
			public delegate void Callback(global::DynamicSkirtBone that);

			public static Callbacks<Callback> Callbacks = new();

			public static void Invoke(global::DynamicSkirtBone __instance) {
				try {
					foreach (var callback in Callbacks.ArrayOfVals) {
						callback(__instance);
					}
				} catch (Exception e) {
					Plugin.MaidVoicePitch.LogError(e);
				}
			}
		}
	}

	namespace jiggleBone {
		public class PreLateUpdateSelf {
			public delegate void Callback(global::jiggleBone that);

			public static Callbacks<Callback> Callbacks = new();

			[HarmonyPrefix]
			[HarmonyPatch(typeof(global::jiggleBone), nameof(global::jiggleBone.LateUpdateSelf))]
			public static void Invoke(global::jiggleBone __instance) {
				try {
					foreach (var callback in Callbacks.ArrayOfVals) {
						callback(__instance);
					}
				} catch (Exception e) {
					Plugin.MaidVoicePitch.LogError(e);
				}
			}
		}

		public class PostLateUpdateSelf {
			public delegate void Callback(global::jiggleBone that);

			public static Callbacks<Callback> Callbacks = new();

			[HarmonyPostfix]
			[HarmonyPatch(typeof(global::jiggleBone), nameof(global::jiggleBone.LateUpdateSelf))]
			public static void Invoke(global::jiggleBone __instance) {
				try {
					foreach (var callback in Callbacks.ArrayOfVals) {
						callback(__instance);
					}
				} catch (Exception e) {
					Plugin.MaidVoicePitch.LogError(e);
				}
			}
		}
	}

	//Calling values in a normal dictionary returns an enumerable which has high GC allocs. Given the amount of calls for that property, we made a cached dictionary that does basically the same thing but way faster without any GC allocs.
	public class Callbacks<TValue> : SortedDictionary<string, TValue> {
		private TValue[] _values = new TValue[0];

		public TValue[] ArrayOfVals {
			get {
				if (Count != _values.Length) {
					Array.Resize(ref _values, Count);
					Values.CopyTo(_values, 0);
				}

				return _values;
			}
		}
	}
}
