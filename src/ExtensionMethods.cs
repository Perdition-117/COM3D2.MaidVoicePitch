using UnityEngine;

namespace ExtensionMethods;

internal static class StringExtensions {
	// http://stackoverflow.com/a/1082587/2132223
	public static TEnum ToEnum<TEnum>(this string strEnumValue, TEnum defaultValue) {
		if (!Enum.IsDefined(typeof(TEnum), strEnumValue)) {
			return defaultValue;
		}
		return (TEnum)Enum.Parse(typeof(TEnum), strEnumValue);
	}
}

internal static class Vector3Extensions {
	public static void Deconstruct(this Vector3 source, out float x, out float y, out float z) {
		x = source.x;
		y = source.y;
		z = source.z;
	}
}
