using System.Xml.Serialization;
using HamStats.Website.Xml;

namespace HamStats.Website.Data;

public class ContactDelete
{
    [XmlElement("app")]
    public string App { get; set; }

    [XmlElement("timestamp")]
    public DateTimeConverter Timestamp { get; set; }

    [XmlElement("contestnr")]
    public int? ContestNumber { get; set; }

    [XmlElement("StationName")]
    public string? StationName { get; set; }

    [XmlElement("ID")]
    public string? Id { get; set; }
}