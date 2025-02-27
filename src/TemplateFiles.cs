public interface ITemplateFile {
	bool Load(string fname);
}

public class TemplateFiles<T> : Dictionary<string, T> where T : ITemplateFile, new() {
	public T Get(string fname) {
		if (fname != null) {
			if (TryGetValue(fname, out var t0)) {
				return t0;
			}

			var t1 = new T();
			if (t1.Load(fname)) {
				Add(fname, t1);
				return t1;
			}
		}
		return default;
	}
}
