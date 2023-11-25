using UnityEngine;

namespace ExtensionMethods;

internal static class Vector3Extensions {
	public static void Deconstruct(this Vector3 source, out float x, out float y, out float z) {
		x = source.x;
		y = source.y;
		z = source.z;
	}
}
