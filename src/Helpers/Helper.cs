internal static class Helper {
	public static float StringToFloat(string s, float defaultValue) {
		return s != null && float.TryParse(s, out var v) ? v : defaultValue;
	}

	// http://stackoverflow.com/a/1082587/2132223
	public static TEnum ToEnum<TEnum>(this string strEnumValue, TEnum defaultValue) {
		if (!Enum.IsDefined(typeof(TEnum), strEnumValue)) {
			return defaultValue;
		}
		return (TEnum)Enum.Parse(typeof(TEnum), strEnumValue);
	}
}
