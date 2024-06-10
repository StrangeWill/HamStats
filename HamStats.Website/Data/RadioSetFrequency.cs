using System.Xml.Serialization;

namespace HamStats.Website.Data;

public class RadioSetFrequency
{
    [XmlElement("app")]
    public string? App { get; set; }
}