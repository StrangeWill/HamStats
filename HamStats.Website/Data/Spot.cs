using System.Xml.Serialization;

namespace HamStats.Website.Data;

public class Spot
{
    [XmlElement("app")]
    public string? App { get; set; }
}