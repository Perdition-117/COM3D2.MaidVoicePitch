public interface ITemplateFile {
	bool Load(string fileName);
}

public class TemplateFiles<T> : Dictionary<string, T> where T : ITemplateFile, new() {
	public T GetTemplate(string fileName) {
		if (fileName != null) {
			if (TryGetValue(fileName, out var t0)) {
				return t0;
			}

			var t1 = new T();
			if (t1.Load(fileName)) {
				Add(fileName, t1);
				return t1;
			}
		}
		return default;
	}
}
