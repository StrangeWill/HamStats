using System.Xml.Serialization;

namespace HamStats.Website.Data;

public class Spectrum
{
    [XmlElement("app")]
    public string? App { get; set; }
}