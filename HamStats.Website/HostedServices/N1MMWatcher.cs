
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
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

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ = ReceiveDataAsync(cancellationToken);
        return Task.CompletedTask;
    }

    protected async Task ReceiveDataAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await UdpClient.ReceiveAsync();
            var receivedMessage = Encoding.UTF8.GetString(result.Buffer).Replace("False", "false").Replace("True", "false");
            Logger.LogTrace(receivedMessage);
            var element = XElement.Parse(receivedMessage);
            await HandleMessage(element);
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Error handling message");
        }

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
        var hamStatsDbContext = scope.ServiceProvider.GetRequiredService<HamStatsDbContext>();
        var radioId = await hamStatsDbContext.N1MMRadios
            .Where(r => r.RadioNumber == info.RadioNumber)
            .Select(r => r.VFO!.RadioId)
            .FirstAsync();
        hamStatsDbContext.N1MMContacts.Add(new N1MMContact
        {
            Date = info.TimeStamp.Value.ToUniversalTime(),
            FromCall = info.MyCall!,
            ToCall = info.Call!,
            Band = info.Band,
            RxFrequency = info.RxFrequency,
            TxFrequency = info.TxFrequency,
            Mode = info.Mode,
            CountryPrefix = info.CountryPrefix,
            Sent = info.Sent,
            Receive = info.Received,
            Exchange = info.Exchange1,
            Section = info.Section,
            Operator = info.Operator,
            N1MMId = info.Id!,
            Contact = new Contact
            {
                Date = info.TimeStamp.Value.ToUniversalTime(),
                FromCall = info.MyCall!,
                ToCall = info.Call!,
                Band = info.Band,
                Mode = info.Mode,
                RxFrequency = info.RxFrequency,
                TxFrequency = info.TxFrequency,
                Class = info.Exchange1,
                Section = info.Section,
                RadioId = radioId.Value,
                Operator = info.Operator,
            }
        });
        await hamStatsDbContext.SaveChangesAsync();

        Logger.LogDebug($"Contact: {info.Call} - {info.Band} - {info.TimeStamp.Value.ToUniversalTime()} - {info.Mode} - {info.RadioInterfaced} - {info.RadioNumber} - {info.Operator} - RX: {info.RxFrequency} - TX: {info.TxFrequency}");
    }

    protected async Task Process(ContactReplace info)
    {
        using var scope = ServiceProvider.CreateScope();
        var hamStatsDbContext = scope.ServiceProvider.GetRequiredService<HamStatsDbContext>();
        var contact = await hamStatsDbContext.N1MMContacts
            .Include(c => c.Contact)
            .FirstOrDefaultAsync(n => n.N1MMId == info.Id);
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
        contact.Exchange = info.Exchange1;
        contact.Section = info.Section;
        contact.Contact.Band = info.Band;
        contact.Contact.FromCall = info.MyCall;
        contact.Contact.ToCall = info.Call;
        contact.Contact.Band = info.Band;
        contact.Contact.Mode = info.Mode;
        contact.Contact.RxFrequency = info.RxFrequency;
        contact.Contact.TxFrequency = info.TxFrequency;
        contact.Contact.Class = info.Exchange1;
        contact.Contact.Section = info.Section;

        await hamStatsDbContext.SaveChangesAsync();
        Logger.LogDebug($"Contact Replace: {info.App}");
    }

    protected async Task Process(ContactDelete info)
    {
        using var scope = ServiceProvider.CreateScope();
        var hamStatsDbContext = scope.ServiceProvider.GetRequiredService<HamStatsDbContext>();
        await hamStatsDbContext.Contacts
            .Where(c => c.N1MMContact.N1MMId == info.Id)
            .ExecuteDeleteAsync();
        await hamStatsDbContext.N1MMContacts
            .Where(n => n.N1MMId == info.Id)
            .ExecuteDeleteAsync();
        Logger.LogDebug($"Contact Delete: {info.App}");
    }

    protected async Task Process(LookupInfo info)
    {
        Logger.LogDebug($"Lookup: {info.App}");
    }

    protected async Task Process(RadioInfo info)
    {
        using var scope = ServiceProvider.CreateScope();
        var hamStatsDbContext = scope.ServiceProvider.GetRequiredService<HamStatsDbContext>();
        var n1mmRadio = await hamStatsDbContext.N1MMRadios
            .Include(r => r.VFO)
            .ThenInclude(r => r!.Radio)
            .Where(r => r.StationName == info.StationName && r.RadioName == info.RadioName && r.RadioNumber == info.RadioNumber)
            .FirstOrDefaultAsync();
        if (n1mmRadio is null)
        {
            var radio = await hamStatsDbContext.Radios
                .FirstOrDefaultAsync(r => r.VFOs.Any(v => v.N1MMRadio!.RadioName == info.RadioName)) ?? new Radio
                {
                    Name = info.RadioName,
                    Operator = info.OpCall
                };
            n1mmRadio = hamStatsDbContext.Add(
                new N1MMRadio
                {
                    StationName = info.StationName!,
                    RadioName = info.RadioName!,
                    RadioNumber = info.RadioNumber,
                    VFO = new VFO
                    {
                        Name = info.RadioNumber switch
                        {
                            1 => "A",
                            2 => "B",
                            _ => throw new Exception($"Radio Number {info.RadioNumber} cannot be resolved to a VFO")
                        },
                        Radio = radio
                    }
                }).Entity;
        }

        n1mmRadio.LastSeen = DateTime.UtcNow;
        n1mmRadio.RxFrequency = info.Frequency;
        n1mmRadio.TxFrequency = info.TxFrequency;
        n1mmRadio.VFO.RxFrequency = info.Frequency;
        n1mmRadio.VFO.TxFrequency = info.TxFrequency;
        n1mmRadio.VFO.Radio.Operator = info.OpCall;
        await hamStatsDbContext.SaveChangesAsync();
        Logger.LogDebug($"Radio Info: {info.RadioName} #:{info.RadioNumber} - RX: {info.Frequency} - TX: {info.TxFrequency}");
    }

    protected async Task Process(Spot info)
    {
        Logger.LogDebug($"Spot: {info.App}");
    }

    protected async Task Process(ScorePayload info)
    {
        using var scope = ServiceProvider.CreateScope();
        var hamStatsDbContext = scope.ServiceProvider.GetRequiredService<HamStatsDbContext>();
        var score = await hamStatsDbContext.Scores
            .Include(s => s.Breakdown)
            .FirstOrDefaultAsync() ?? hamStatsDbContext.Scores.Add(new Score
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

        await hamStatsDbContext.SaveChangesAsync();
        Logger.LogDebug($"Score: {info.Score}");
    }
}