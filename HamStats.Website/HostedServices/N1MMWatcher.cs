
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Xml.Linq;
using System.Xml.Serialization;
using HamStats.Data;
using HamStats.Data.Models;
using HamStats.Website.Data;
using Microsoft.EntityFrameworkCore;

namespace HamStats.Website.HostedServices;

public class N1MMWatcher : IHostedService
{
    protected ILogger<N1MMWatcher> Logger { get; }

    protected IServiceProvider ServiceProvider { get; }

    protected UdpClient UdpClient { get; set; }

    public N1MMWatcher(
        ILogger<N1MMWatcher> logger,
        IServiceProvider serviceProvider)
    {
        Logger = logger;
        ServiceProvider = serviceProvider;
        UdpClient = new UdpClient(new IPEndPoint(IPAddress.Any, 16000));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await ReceiveDataAsync(cancellationToken);
    }

    protected async Task ReceiveDataAsync(CancellationToken cancellationToken)
    {
        var result = await UdpClient.ReceiveAsync();
        var receivedMessage = Encoding.UTF8.GetString(result.Buffer).Replace("False", "false").Replace("True", "false");
        var element = XElement.Parse(receivedMessage);
        await HandleMessage(element);
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        await ReceiveDataAsync(cancellationToken);
    }

    protected async Task HandleMessage(XElement element)
    {
        var root = element.Name.LocalName.ToLower();
        var task = root switch
        {
            "AppInfo" => ProcessXml<ApplicationInfo>(element),
            "contactinfo" => ProcessXml<ContactInfo>(element),
            "contactreplace" => ProcessXml<ContactReplace>(element),
            "contactdelete" => ProcessXml<ContactDelete>(element),
            "lookupinfo" => ProcessXml<LookupInfo>(element),
            "radioinfo" => ProcessXml<RadioInfo>(element),
            "spot" => ProcessXml<Spot>(element),
            "dynamicresults" => ProcessXml<ScorePayload>(element),
            _ => Log($"Element type {root} not found")
        };

        await task;
    }

    public Task Log(string message)
    {
        Logger.LogWarning(message);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Logger.LogInformation("Shutting down N1MM Watcher");
        UdpClient.Close();
        return Task.CompletedTask;
    }

    static T? Deserialize<T>(XElement element)
    {
        var serializer = new XmlSerializer(typeof(T));
        using var reader = new StringReader(element.ToString());
        try
        {
            return (T?)serializer.Deserialize(reader);
        }
        catch (Exception exception)
        {
            throw new Exception($"Error processing {element.Name.LocalName.ToLower()} -- {element}", exception);
        }
    }

    protected Task ProcessXml<T>(XElement element)
    {
        var payload = Deserialize<T>(element);
        return payload switch
        {
            ApplicationInfo appInfo => Process(appInfo),
            ContactReplace contactReplace => Process(contactReplace),
            LookupInfo lookupInfo => Process(lookupInfo),
            ContactInfo contactInfo => Process(contactInfo),
            ContactDelete contactDelete => Process(contactDelete),
            RadioInfo radioInfo => Process(radioInfo),
            Spot spot => Process(spot),
            ScorePayload score => Process(score),
            _ => throw new Exception($"Type {payload?.GetType()} not found")
        };
    }

    protected async Task Process(ApplicationInfo info)
    {
        Logger.LogDebug($"App: {info.App}");
    }

    protected async Task Process(ContactInfo info)
    {
        using var scope = ServiceProvider.CreateScope();
        var hamStatsDb = scope.ServiceProvider.GetRequiredService<HamStatsDb>();
        hamStatsDb.N1MMContacts.Add(new N1MMContact
        {
            Date = info.TimeStamp.Value,
            FromCall = info.MyCall,
            ToCall = info.Call,
            Band = info.Band,
            RxFrequency = info.RxFrequency,
            TxFrequency = info.TxFrequency,
            Mode = info.Mode,
            CountryPrefix = info.CountryPrefix,
            Sent = info.Sent,
            Receive = info.Received,
            Exchange = info.Exchangel,
            N1MMId = info.Id
        });
        await hamStatsDb.SaveChangesAsync();

        Logger.LogDebug($"Contact: {info.Call} - {info.Band} - {info.TimeStamp.Value.ToUniversalTime()} - {info.Mode} - {info.RadioInterfaced} - {info.RadioNumber} - {info.Operator} - RX: {info.RxFrequency} - TX: {info.TxFrequency}");
    }

    protected async Task Process(ContactReplace info)
    {
        using var scope = ServiceProvider.CreateScope();
        var hamStatsDb = scope.ServiceProvider.GetRequiredService<HamStatsDb>();
        var contact = await hamStatsDb.N1MMContacts.FirstOrDefaultAsync(n => n.N1MMId == info.Id);
        if (contact is null)
        {
            return;
        }

        contact.Date = info.TimeStamp.Value;
        contact.FromCall = info.MyCall;
        contact.ToCall = info.Call;
        contact.Band = info.Band;
        contact.RxFrequency = info.RxFrequency;
        contact.TxFrequency = info.TxFrequency;
        contact.Mode = info.Mode;
        contact.CountryPrefix = info.CountryPrefix;
        contact.Sent = info.Sent;
        contact.Receive = info.Received;
        contact.Exchange = info.Exchangel;
        await hamStatsDb.SaveChangesAsync();
        Logger.LogDebug($"Contact Replace: {info.App}");
    }

    protected async Task Process(ContactDelete info)
    {
        using var scope = ServiceProvider.CreateScope();
        var hamStatsDb = scope.ServiceProvider.GetRequiredService<HamStatsDb>();
        await hamStatsDb.N1MMContacts.Where(n => n.N1MMId == info.Id).ExecuteDeleteAsync();
        Logger.LogDebug($"Contact Delete: {info.App}");
    }

    protected async Task Process(LookupInfo info)
    {
        Logger.LogDebug($"Lookup: {info.App}");
    }

    protected async Task Process(RadioInfo info)
    {
        using var scope = ServiceProvider.CreateScope();
        var hamStatsDb = scope.ServiceProvider.GetRequiredService<HamStatsDb>();
        var radio = await hamStatsDb.N1MMRadios
            .Where(r => r.StationName == info.StationName && r.RadioName == info.RadioName && r.RadioNumber == info.RadioNumber)
            .FirstOrDefaultAsync() ?? hamStatsDb.Add(
            new N1MMRadio
            {
                StationName = info.StationName!,
                RadioName = info.RadioName!,
                RadioNumber = info.RadioNumber,
            }).Entity;
        radio.LastSeen = DateTime.UtcNow;
        radio.RxFrequency = info.Frequency;
        radio.TxFrequency = info.TxFrequency;
        await hamStatsDb.SaveChangesAsync();
        Logger.LogDebug($"Radio Info: {info.RadioName} #:{info.RadioNumber} - RX: {info.Frequency} - TX: {info.TxFrequency}");
    }

    protected async Task Process(Spot info)
    {
        Logger.LogDebug($"Spot: {info.App}");
    }

    protected async Task Process(ScorePayload info)
    {
        using var scope = ServiceProvider.CreateScope();
        var hamStatsDb = scope.ServiceProvider.GetRequiredService<HamStatsDb>();
        var score = await hamStatsDb.Scores
            .Include(s => s.Breakdown)
            .FirstOrDefaultAsync() ?? hamStatsDb.Scores.Add(new Score
            {
                Breakdown = []
            }).Entity;
        score.ArrlSection = info.Qth.ARRLSection;
        score.Assisted = info.Class.Assisted;
        score.Bands = info.Class.Bands;
        score.Call = info.Call;
        score.Club = info.Club;
        score.Contest = info.Contest;
        score.CqZone = info.Qth.CQZone;
        score.DxccCountry = info.Qth.DXCCCountry;
        score.Grid6 = info.Qth.Grid;
        score.Iaruzone = info.Qth.Iaruzone;
        score.Mode = info.Class.Mode;
        score.Ops = info.Ops;
        score.Overlay = info.Class.Overlay;
        score.Power = info.Class.Power;
        score.Stprvoth = info.Qth.Stprvoth;
        score.Transmitter = info.Class.Transmitter;
        score.Value = info.Score.Value;
        foreach (var point in info.Breakdown.Points)
        {
            var breakdown = score.Breakdown.FirstOrDefault(b => b.Band == point.Band && b.Mode == point.Mode);
            if (breakdown is null)
            {
                breakdown = new ScoreBreakdown
                {
                    Band = point.Band,
                    Mode = point.Mode
                };
                score.Breakdown.Add(breakdown);
            }

            breakdown.Points = point.Value;
        }

        foreach (var qso in info.Breakdown.Qsos)
        {
            var breakdown = score.Breakdown.FirstOrDefault(b => b.Band == qso.Band && b.Mode == qso.Mode);
            if (breakdown is null)
            {
                breakdown = new ScoreBreakdown
                {
                    Band = qso.Band,
                    Mode = qso.Mode
                };
                score.Breakdown.Add(breakdown);
            }

            breakdown.QSOs = qso.Value;
        }

        await hamStatsDb.SaveChangesAsync();
        Logger.LogDebug($"Score: {info.Score}");
    }
}