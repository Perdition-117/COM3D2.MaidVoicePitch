using System.IO;

internal static class Helper {
	static StreamWriter logStreamWriter = null;
	public static readonly DateTime now = DateTime.Now;

#if DEBUG
	public static bool bLogEnable = true;
#else
	public static bool bLogEnable = false;
#endif

	public static void Log(string s) {
		if (!bLogEnable) {
			return;
		}
		if (logStreamWriter == null) {
			var fname = $".\\Log_{now:yyyyMMdd_HHmmss}.log";
			logStreamWriter = new(fname, true);
		}
		logStreamWriter.Write(s);
		logStreamWriter.Write("\n");
		logStreamWriter.Flush();
	}

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
