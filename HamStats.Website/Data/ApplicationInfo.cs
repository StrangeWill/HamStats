using System.Xml.Serialization;

namespace HamStats.Website.Data;

public class ApplicationInfo
{
    [XmlElement("app")]
    public string? App { get; set; }

    [XmlElement("dbname")]
    public string? DatabaseName { get; set; }

    [XmlElement("contestnr")]
    public int? ContestNumber { get; set; }

    [XmlElement("contestname")]
    public string? ContestName { get; set; }

    [XmlElement("StationName")]
    public string? StationName { get; set; }
}