
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using System.Xml.Linq;
using System.Xml.Serialization;
using HamStats.Data;
using HamStats.Data.Models;
using HamStats.Website.CallsignLookup;
using HamStats.Website.Data;
using HamStats.Website.Hubs;
using Microsoft.EntityFrameworkCore;

namespace HamStats.Website.HostedServices;

public class N1MMWatcher : IHostedService
{
    protected ILogger<N1MMWatcher> Logger { get; }

    protected IServiceProvider ServiceProvider { get; }

    protected DashboardNotifier Notifier { get; }

    protected UdpClient UdpClient { get; set; }

    // Inbound datagrams are queued here so the socket reader never waits on a DB write.
    private readonly Channel<string> Messages = Channel.CreateUnbounded<string>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });

    private readonly CancellationTokenSource Cancellation = new();

    public N1MMWatcher(
        ILogger<N1MMWatcher> logger,
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        DashboardNotifier notifier)
    {
        Logger = logger;
        ServiceProvider = serviceProvider;
        Notifier = notifier;
        // N1MM+ broadcasts to UDP 12060 by default; override with N1MM:BroadcastPort.
        var port = configuration.GetValue<int?>("N1MM:BroadcastPort") ?? 12060;
        Logger.LogInformation("N1MM Watcher listening for broadcasts on UDP port {Port}", port);
        UdpClient = new UdpClient(new IPEndPoint(IPAddress.Any, port));
        // Absorb bursts at the OS level so datagrams aren't dropped during a slow write.
        UdpClient.Client.ReceiveBufferSize = 1 << 20;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // A tight receiver drains the socket into the queue; a separate worker does the parsing and
        // DB writes. Decoupling them stops a slow write from back-pressuring the socket and dropping
        // inbound datagrams — UDP has no retransmit, so a dropped packet is lost data.
        _ = ReceiveLoopAsync(Cancellation.Token);
        _ = ProcessLoopAsync(Cancellation.Token);
        return Task.CompletedTask;
    }

    protected async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var result = await UdpClient.ReceiveAsync(cancellationToken);
                var message = Encoding.UTF8.GetString(result.Buffer).Replace("False", "false").Replace("True", "true");
                Logger.LogTrace(message);
                // Unbounded queue: TryWrite always succeeds, so the socket is never blocked.
                Messages.Writer.TryWrite(message);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception exception)
            {
                Logger.LogError(exception, "Error receiving N1MM datagram");
            }
        }

        Messages.Writer.TryComplete();
    }

    protected async Task ProcessLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var message in Messages.Reader.ReadAllAsync(cancellationToken))
            {
                try
                {
                    var element = XElement.Parse(message);
                    await HandleMessage(element);
                }
                catch (Exception exception)
                {
                    Logger.LogError(exception, "Error handling message: {Message}", message);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // shutting down
        }
    }

    protected async Task HandleMessage(XElement element)
    {
        var root = element.Name.LocalName.ToLower();
        var task = root switch
        {
            "appinfo" => ProcessXml<ApplicationInfo>(element),
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
        Cancellation.Cancel();
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
        // N1MM's radionr is only the radio slot within a station (1=A, 2=B), not a unique radio id,
        // so match on StationName too — otherwise every station's radio 1 collapses onto the first one seen.
        var radioId = await hamStatsDbContext.N1MMRadios
            .Where(r => r.StationName == info.StationName && r.RadioNumber == info.RadioNumber)
            .Select(r => (Guid?)r.VFO!.RadioId)
            .FirstOrDefaultAsync();
        if (radioId is null)
        {
            // Digital QSOs (FT8/FT4 via the WSJT-X/JTDX interface) arrive with radionr=0 — N1MM never
            // assigns them an A/B slot — so the slot match above fails. Fall back to the radio at the same
            // station tuned closest to the QSO's RX frequency (both N1MM 10 Hz strings).
            radioId = await ResolveRadioByFrequency(hamStatsDbContext, info.StationName, info.RxFrequency);
        }
        if (radioId is null)
        {
            // Contact arrived before any radioinfo registered a radio for this station; drop it rather than crash.
            Logger.LogWarning($"No radio for contact {info.Call} (station {info.StationName}, radio {info.RadioNumber}); skipping.");
            return;
        }
        var gridsquare = await ResolveGrid(hamStatsDbContext, info.Gridsquare, info.Call, info.Section);
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
                Gridsquare = gridsquare,
                RadioId = radioId.Value,
                Operator = info.Operator,
            }
        });
        await hamStatsDbContext.SaveChangesAsync();
        await Notifier.Changed(DashboardNotifier.Contacts, DashboardNotifier.Radios);

        Logger.LogDebug($"Contact: {info.Call} - {info.Band} - {info.TimeStamp.Value.ToUniversalTime()} - {info.Mode} - {info.RadioInterfaced} - {info.RadioNumber} - {info.Operator} - RX: {info.RxFrequency} - TX: {info.TxFrequency}");
    }

    /// <summary>
    /// Picks the radio at <paramref name="stationName"/> tuned closest to <paramref name="rxFrequency"/>.
    /// Used for digital QSOs (radionr=0), which carry no A/B slot but do carry an RX frequency. The
    /// candidate set is tiny (1–2 radios per station), so the nearest-frequency match runs in memory.
    /// </summary>
    protected async Task<Guid?> ResolveRadioByFrequency(HamStatsDbContext hamStatsDbContext, string? stationName, string? rxFrequency)
    {
        if (!long.TryParse(rxFrequency, out var target))
        {
            return null;
        }

        var candidates = await hamStatsDbContext.N1MMRadios
            .Where(r => r.StationName == stationName && r.VFO != null)
            .Select(r => new { r.RxFrequency, r.VFO!.RadioId })
            .ToListAsync();

        return candidates
            .Where(c => c.RadioId is not null && long.TryParse(c.RxFrequency, out _))
            .OrderBy(c => Math.Abs(long.Parse(c.RxFrequency!) - target))
            .Select(c => c.RadioId)
            .FirstOrDefault();
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
        contact.Contact.Gridsquare = await ResolveGrid(hamStatsDbContext, info.Gridsquare, info.Call, info.Section);

        await hamStatsDbContext.SaveChangesAsync();
        await Notifier.Changed(DashboardNotifier.Contacts);
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
        await Notifier.Changed(DashboardNotifier.Contacts, DashboardNotifier.Radios);
        Logger.LogDebug($"Contact Delete: {info.App}");
    }

    protected async Task Process(LookupInfo info)
    {
        Logger.LogDebug($"Lookup: {info.App}");
    }

    /// <summary>
    /// Resolves the worked station's Maidenhead grid: prefer what N1MM supplied, otherwise fall back
    /// to the offline <see cref="CallsignEntry"/> lookup table (populated by the Hangfire import job),
    /// and finally — for Field Day stations that exchange an ARRL/RAC section but no grid — to the
    /// section's centroid.
    /// </summary>
    protected async Task<string?> ResolveGrid(HamStatsDbContext hamStatsDbContext, string? provided, string? call, string? section)
    {
        if (!string.IsNullOrWhiteSpace(provided))
        {
            return provided;
        }

        if (string.IsNullOrWhiteSpace(call))
        {
            return ArrlSections.GridFor(section);
        }

        var key = call.ToUpperInvariant();

        // Prefer a precise per-licensee grid (FCC/ISED postal lookup).
        var exact = await hamStatsDbContext.Callsigns
            .Where(c => c.Callsign == key)
            .Select(c => c.Grid)
            .FirstOrDefaultAsync();
        if (exact is not null)
        {
            return exact;
        }

        // Fall back to the cty.dat prefix table for DX coverage: an exact-callsign exception wins,
        // otherwise the longest matching leading prefix. Naive leading match (ignores portable
        // indicators) but sufficient to place a contact at its DXCC entity.
        var prefixGrid = await hamStatsDbContext.CallsignPrefixes
            .Where(p => (p.IsExact && p.Prefix == key) || (!p.IsExact && EF.Functions.Like(key, p.Prefix + "%")))
            .OrderByDescending(p => p.IsExact)
            .ThenByDescending(p => p.Prefix.Length)
            .Select(p => p.Grid)
            .FirstOrDefaultAsync();
        if (prefixGrid is not null)
        {
            return prefixGrid;
        }

        // Last resort: Field Day stations exchange an ARRL/RAC section, not a grid, and many won't be
        // in the callsign tables — place them at the section's centroid so they still map.
        return ArrlSections.GridFor(section);
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
        await Notifier.Changed(DashboardNotifier.Radios);
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
        await Notifier.Changed(DashboardNotifier.Scores);
        Logger.LogDebug($"Score: {info.Score}");
    }
}