using System.Xml.Serialization;
using HamStats.Website.Xml;

namespace HamStats.Website.Data;

[XmlRoot("contactinfo")]
public class ContactInfo
{
    [XmlElement("app")]
    public string? App { get; set; }

    [XmlElement("contestname")]
    public string? ContestName { get; set; }

    [XmlElement("contestnr")]
    public int? ContestNumber { get; set; }

    [XmlElement("timestamp")]
    public DateTimeConverter TimeStamp { get; set; }

    [XmlElement("mycall")]
    public string? MyCall { get; set; }

    [XmlElement("band")]
    public string Band { get; set; }

    [XmlElement("rxfreq")]
    public string RxFrequency { get; set; }

    [XmlElement("txfreq")]
    public string TxFrequency { get; set; }

    [XmlElement("operator")]
    public string? Operator { get; set; }

    [XmlElement("mode")]
    public string Mode { get; set; }

    [XmlElement("call")]
    public string? Call { get; set; }

    [XmlElement("countryprefix")]
    public string? CountryPrefix { get; set; }

    [XmlElement("wpxprefix")]
    public string? WpxPrefix { get; set; }

    [XmlElement("stationprefix")]
    public string? StationPrefix { get; set; }

    [XmlElement("continent")]
    public string Continent { get; set; }

    [XmlElement("snt")]
    public string? Sent { get; set; }

    [XmlElement("sntnr")]
    public int? SentNumber { get; set; }

    [XmlElement("rcv")]
    public string? Received { get; set; }

    [XmlElement("recvnr")]
    public int? RecievedNumber { get; set; }

    [XmlElement("gridsquare")]
    public string? Gridsquare { get; set; }

    [XmlElement("exchange1")]
    public string? Exchange1 { get; set; }

    [XmlElement("section")]
    public string? Section { get; set; }

    [XmlElement("comment")]
    public string? Comment { get; set; }

    [XmlElement("qth")]
    public string? Qth { get; set; }

    [XmlElement("name")]
    public string? Name { get; set; }

    [XmlElement("power")]
    public string? Power { get; set; }

    [XmlElement("misctext")]
    public string? Misctext { get; set; }

    [XmlElement("zone")]
    public int? Zone { get; set; }

    [XmlElement("prec")]
    public string? Prec { get; set; }

    [XmlElement("ck")]
    public int? Ck { get; set; }

    [XmlElement("ismultiplierl")]
    public int? Ismultiplierl { get; set; }

    [XmlElement("ismultiplier2")]
    public int? Ismultiplier2 { get; set; }

    [XmlElement("ismultiplier3")]
    public int? Ismultiplier3 { get; set; }

    [XmlElement("points")]
    public int? Points { get; set; }

    [XmlElement("radionr")]
    public int? RadioNumber { get; set; }

    [XmlElement("run1run2")]
    public int? Run { get; set; }

    [XmlElement("RoverLocation")]
    public string? RoverLocation { get; set; }

    [XmlElement("RadioInterfaced")]
    public int? RadioInterfaced { get; set; }

    [XmlElement("NetworkedCompNr")]
    public string? NetworkedCompNr { get; set; }

    [XmlElement("IsOriginal")]
    public bool IsOriginal { get; set; }

    [XmlElement("NetBiosName")]
    public string? NetBiosName { get; set; }

    [XmlElement("IsRunQSO")]
    public int? IsRunQSO { get; set; }

    [XmlElement("StationName")]
    public string? StationName { get; set; }

    [XmlElement("ID")]
    public string? Id { get; set; }

    [XmlElement("IsClaimedQso")]
    public int? IsClaimedQso { get; set; }
}