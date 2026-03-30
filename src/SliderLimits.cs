using System.Xml.Serialization;

namespace MaidVoicePitch;

[XmlRoot("SliderLimits")]
public class SliderLimits {
	[XmlElement("Slider")]
	public Slider[] Sliders { get; set; }

	public class Slider {
		[XmlAttribute] public string Name { get; set; }
		[XmlAttribute] public int MinValue { get; set; }
		[XmlAttribute] public int MaxValue { get; set; }
	}
}
