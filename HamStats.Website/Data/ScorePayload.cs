using System.Xml.Serialization;
using HamStats.Website.Xml;

namespace HamStats.Website.Data;

[XmlRoot("dynamicresults")]
public class ScorePayload
{
    [XmlElement("contest")]
    public string? Contest { get; set; }

    [XmlElement("call")]
    public string? Call { get; set; }

    [XmlElement("Ops")]
    public string? Ops { get; set; }

    [XmlElement("class")]
    public ScoreClass Class { get; set; }

    [XmlElement("club")]
    public string Club { get; set; }

    [XmlElement("qth")]
    public QthClass Qth { get; set; }

    [XmlElement("breakdown")]
    public BreakdownClass Breakdown { get; set; }

    [XmlElement("score")]
    public int? Score { get; set; }

    [XmlElement("timestamp")]
    public DateTimeConverter Timestamp { get; set; }

    public class ScoreClass
    {
        [XmlAttribute("power")]
        public string? Power { get; set; }

        [XmlAttribute("assisted")]
        public string? Assisted { get; set; }

        [XmlAttribute("transmitter")]
        public string? Transmitter { get; set; }

        [XmlAttribute("ops")]
        public string? Ops { get; set; }

        [XmlAttribute("bands")]
        public string? Bands { get; set; }

        [XmlAttribute("mode")]
        public string? Mode { get; set; }

        [XmlAttribute("overlay")]
        public string? Overlay { get; set; }
    }

    public class QthClass
    {
        [XmlElement("dxcccountry")]
        public string? DXCCCountry { get; set; }

        [XmlElement("cqzone")]
        public string? CQZone { get; set; }

        [XmlElement("arrlsection")]
        public string? ARRLSection { get; set; }

        [XmlElement("stprvoth")]
        public string? Stprvoth { get; set; }

        [XmlElement("grid6")]
        public string? Grid { get; set; }

        [XmlElement("iaruzone")]
        public string? Iaruzone { get; set; }
    }

    public class BreakdownClass
    {
        [XmlElement("qso")]
        public List<QsoClass> Qsos { get; set; }

        [XmlElement("point")]
        public List<QsoClass> Points { get; set; }
    }
    public class QsoClass
    {
        [XmlAttribute("band")]
        public string Band { get; set; }

        [XmlAttribute("mode")]
        public string Mode { get; set; }

        [XmlText]
        public int Value { get; set; }
    }
}