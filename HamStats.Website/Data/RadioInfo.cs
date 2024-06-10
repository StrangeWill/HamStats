using System.Xml.Serialization;

namespace HamStats.Website.Data;

public class RadioInfo
{
    [XmlElement("app")]
    public string? App { get; set; }

    [XmlElement("StationName")]
    public string? StationName { get; set; }

    [XmlElement("RadioNr")]
    public int RadioNumber { get; set; }

    [XmlElement("Freq")]
    public string Frequency { get; set; }

    [XmlElement("TXFreq")]
    public string TxFrequency { get; set; }

    [XmlElement("Mode")]
    public string Mode { get; set; }

    [XmlElement("OpCall")]
    public string? OpCall { get; set; }

    [XmlElement("IsRunning")]
    public bool IsRunning { get; set; }

    [XmlElement("FocusEntry")]
    public int FocusEntry { get; set; }

    [XmlElement("EntryWindowHwnd")]
    public int EntryWindowHwnd { get; set; }

    [XmlElement("Antenna")]
    public int Antenna { get; set; }

    [XmlElement("Rotors")]
    public string? Rotors { get; set; }

    [XmlElement("FocusRadioNr")]
    public int FocusRadioNr { get; set; }

    [XmlElement("IsStereo")]
    public bool IsStereo { get; set; }

    [XmlElement("IsSplit")]
    public bool IsSplit { get; set; }

    [XmlElement("ActiveRadioNr")]
    public int ActiveRadioNr { get; set; }

    [XmlElement("IsTransmitting")]
    public bool IsTransmitting { get; set; }

    [XmlElement("FunctionKeyCaption")]
    public string? FunctionKeyCaption { get; set; }

    [XmlElement("RadioName")]
    public string? RadioName { get; set; }

    [XmlElement("AuxAntSelected")]
    public int AuxAntSelected { get; set; }

    [XmlElement("AuxAntSelectedName")]
    public string? AuxAntSelectedName { get; set; }

    [XmlElement("IsConnected")]
    public string? IsConnected { get; set; }
}