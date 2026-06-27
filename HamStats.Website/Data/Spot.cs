using System.Xml.Serialization;

namespace HamStats.Website.Data;

[XmlRoot("spot")]
public class Spot
{
    [XmlElement("app")]
    public string? App { get; set; }
}