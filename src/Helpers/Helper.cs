using System.Diagnostics;
using System.IO;
using System.Xml;

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

	public static void Log(string format, params object[] args) {
		if (!bLogEnable) {
			return;
		}
		Log(string.Format(format, args));
	}

	public static bool StringToBool(string s, bool defaultValue) {
		if (s == null) {
			return defaultValue;
		}
		if (bool.TryParse(s, out var v)) {
			return v;
		}
		if (float.TryParse(s, out var f)) {
			return f > 0.5f;
		}
		if (int.TryParse(s, out var i)) {
			return i > 0;
		}
		return defaultValue;
	}

	public static int StringToInt(string s, int defaultValue) {
		if (s == null || !int.TryParse(s, out var v)) {
			v = defaultValue;
		}
		return v;
	}

	//  public static float FloatTryParse(string s, float defaultValue)
	public static float StringToFloat(string s, float defaultValue) {
		if (s == null || !float.TryParse(s, out var v)) {
			v = defaultValue;
		}
		return v;
	}

	public static XmlDocument LoadXmlDocument(string xmlFilePath) {
		var xml = new XmlDocument();
		try {
			if (File.Exists(xmlFilePath)) {
				xml.Load(xmlFilePath);
			}
		} catch (Exception e) {
			ShowException(e);
		}
		return xml;
	}

	// http://stackoverflow.com/a/1082587/2132223
	public static TEnum ToEnum<TEnum>(this string strEnumValue, TEnum defaultValue) {
		if (!Enum.IsDefined(typeof(TEnum), strEnumValue)) {
			return defaultValue;
		}
		return (TEnum)Enum.Parse(typeof(TEnum), strEnumValue);
	}

	public static void ShowStackFrames(StackFrame[] stackFrames) {
		foreach (var f in stackFrames) {
			Console.WriteLine(
				"{0}({1}.{2}) : {3}.{4}",
				f.GetFileName(), f.GetFileLineNumber(), f.GetFileColumnNumber(),
				f.GetMethod().DeclaringType, f.GetMethod()
			);
		}
	}

	public static void ShowException(Exception ex) {
		Console.WriteLine(ex.Message);
		var st = new StackTrace(ex, true);
		ShowStackFrames(st.GetFrames());
	}
}
