using System;
using System.Globalization;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace HamStats.Website.Xml;

public class DateTimeConverter : IXmlSerializable
{
    public DateTime Value { get; set; }

    public XmlSchema GetSchema()
    {
        return null;
    }

    public void ReadXml(XmlReader reader)
    {
        string dateValue = reader.ReadElementContentAsString();
        if (DateTime.TryParseExact(dateValue, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsedTime))
        {
            Value = parsedTime;
        }
        else
        {
            Value = DateTime.MinValue;
        }
    }

    public void WriteXml(XmlWriter writer)
    {
        throw new NotImplementedException();
    }
}