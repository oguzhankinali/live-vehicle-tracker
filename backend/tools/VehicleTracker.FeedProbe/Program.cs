using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Google.Protobuf;
using TransitRealtime;

// ---------------------------------------------------------------------------
// ENTUR FEED PROBE
// Amac : GTFS-RT Vehicle Positions akisinin GERCEK davranisini olcmek.
// Not  : Veritabanina yazmaz, migration uretmez, uygulamanin hicbir sinifini
//        kullanmaz/degistirmez (yalnizca uretilmis protobuf siniflarini kullanir).
// Kullanim (backend klasorunden):
//   $env:ENTUR_CLIENT_NAME = "adiniz-vehicle-tracker"
//   dotnet run --project tools/VehicleTracker.FeedProbe -c Release -- 15 3
//   (arguman 1: sure dakika, varsayilan 15 | arguman 2: aralik sn, varsayilan 3)
// ---------------------------------------------------------------------------

var clientName = Environment.GetEnvironmentVariable("ENTUR_CLIENT_NAME");
if (string.IsNullOrWhiteSpace(clientName))
{
    Console.WriteLine("HATA: ENTUR_CLIENT_NAME ortam degiskeni tanimli degil.");
    Console.WriteLine("PowerShell ornegi: $env:ENTUR_CLIENT_NAME = \"adiniz-vehicle-tracker\"");
    return 1;
}

var feedUrl = Environment.GetEnvironmentVariable("ENTUR_FEED_URL")
              ?? "https://api.entur.io/realtime/v1/gtfs-rt/vehicle-positions";

var runMinutes = args.Length > 0
                 && double.TryParse(args[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedMinutes)
                 && parsedMinutes > 0
    ? parsedMinutes
    : 15d;

var intervalSeconds = args.Length > 1 && int.TryParse(args[1], out var parsedInterval) && parsedInterval > 0
    ? parsedInterval
    : 3;

var report = new StringBuilder();

void P(string line = "")
{
    Console.WriteLine(line);
    report.AppendLine(line);
}

P("==================================================");
P("ENTUR FEED PROBE  (yalnizca olcum, DB'ye yazmaz)");
P($"Baslangic (UTC) : {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}");
P($"Sure / aralik   : {runMinutes:0.#} dk / {intervalSeconds} sn");
P($"Feed            : {feedUrl}");
P("==================================================");

using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
http.DefaultRequestHeaders.TryAddWithoutValidation("ET-Client-Name", clientName);

// --- Tek seferlik sikistirma testi (uygulamanin mevcut HttpClient'i sikistirma istemiyor) ---
try
{
    using var rawHandler = new HttpClientHandler { AutomaticDecompression = DecompressionMethods.None };
    using var rawHttp = new HttpClient(rawHandler) { Timeout = TimeSpan.FromSeconds(15) };
    rawHttp.DefaultRequestHeaders.TryAddWithoutValidation("ET-Client-Name", clientName);
    rawHttp.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Encoding", "gzip");

    using var gzipResponse = await rawHttp.GetAsync(feedUrl);
    var gzipBytes = await gzipResponse.Content.ReadAsByteArrayAsync();
    var encodingValue = string.Join(",", gzipResponse.Content.Headers.ContentEncoding);
    var encodingText = encodingValue.Length == 0 ? "yok" : encodingValue;

    P($"SIKISTIRMA TESTI: HTTP {(int)gzipResponse.StatusCode} | Content-Encoding={encodingText} | govde {gzipBytes.Length:N0} byte");
}
catch (Exception ex)
{
    P($"SIKISTIRMA TESTI basarisiz: {ex.GetType().Name}");
}

P();

// --- Olcum durumu ---
var polls = new List<PollInfo>();
var failures = new SortedDictionary<string, int>();
var statsPerPoll = new List<EntityStats>();
var allShas = new HashSet<string>();
var requestMs = new List<double>();
var parseMs = new List<double>();
var payloadBytes = new List<double>();
var lagSeconds = new List<double>();
var headerSteps = new List<double>();
var rewindBehind = new List<double>();
var change = new ChangeTotals();

Dictionary<string, EntityState>? lastAccepted = null;
long? firstAcceptedHeader = null;
long? lastAcceptedHeader = null;
string? lastProcessedSha = null;
var rewinds = 0;
var duplicatePayloads = 0;
var acceptedSnapshots = 0;
List<Row>? firstRows = null;
List<Row>? lastRows = null;
DateTime firstAcceptedReceivedAt = default;
DateTime lastReceivedAt = default;
var headersPrinted = false;

void Count(string key) => failures[key] = failures.TryGetValue(key, out var existing) ? existing + 1 : 1;

void PrintInterestingHeaders(HttpResponseMessage message)
{
    P("YANIT HEADER'LARI (ilgili olanlar):");
    var pattern = new Regex("rate|limit|retry|etag|cache|age|modified|encoding|length|server|via|date|x-",
        RegexOptions.IgnoreCase);

    foreach (var kv in message.Headers.Concat(message.Content.Headers))
    {
        if (pattern.IsMatch(kv.Key))
        {
            P($"  {kv.Key}: {string.Join(", ", kv.Value)}");
        }
    }

    P();
}

var clock = Stopwatch.StartNew();
var totalDuration = TimeSpan.FromMinutes(runMinutes);
var pollIndex = 0;

Console.WriteLine("Olcum basladi... (her 20 poll'da bir ilerleme satiri yazilir)");

while (clock.Elapsed < totalDuration)
{
    var cycle = Stopwatch.StartNew();
    pollIndex++;

    try
    {
        var requestWatch = Stopwatch.StartNew();
        using var response = await http.GetAsync(feedUrl, HttpCompletionOption.ResponseHeadersRead);

        if (!headersPrinted)
        {
            PrintInterestingHeaders(response);
            headersPrinted = true;
        }

        if (!response.IsSuccessStatusCode)
        {
            Count($"HTTP {(int)response.StatusCode}");
        }
        else
        {
            var payload = await response.Content.ReadAsByteArrayAsync();
            requestWatch.Stop();
            var receivedAt = DateTime.UtcNow;
            var sha = Convert.ToHexString(SHA256.HashData(payload));

            var parseWatch = Stopwatch.StartNew();
            var feed = FeedMessage.Parser.ParseFrom(payload);
            parseWatch.Stop();

            var (snapRows, snapStats) = Extractor.Extract(feed);

            long? header = feed.Header != null && feed.Header.HasTimestamp
                ? (long?)feed.Header.Timestamp
                : null;

            polls.Add(new PollInfo(pollIndex, receivedAt, header, sha, payload.Length, snapStats.Total));
            allShas.Add(sha);
            requestMs.Add(requestWatch.Elapsed.TotalMilliseconds);
            parseMs.Add(parseWatch.Elapsed.TotalMilliseconds);
            payloadBytes.Add(payload.Length);
            statsPerPoll.Add(snapStats);

            if (header is long headerForLag)
            {
                var headerTime = DateTimeOffset.FromUnixTimeSeconds(headerForLag).UtcDateTime;
                lagSeconds.Add((receivedAt - headerTime).TotalSeconds);
            }

            var isRewind = false;
            if (header is long headerNow && lastAcceptedHeader is long headerPrevious && headerNow < headerPrevious)
            {
                isRewind = true;
                rewinds++;
                rewindBehind.Add(headerPrevious - headerNow);
            }

            if (!isRewind && sha == lastProcessedSha)
            {
                duplicatePayloads++;
            }
            else if (!isRewind)
            {
                var current = Analysis.BuildState(snapRows);

                if (lastAccepted == null)
                {
                    firstAcceptedHeader = header;
                    firstAcceptedReceivedAt = receivedAt;
                    firstRows = snapRows;
                }
                else
                {
                    if (header is long stepNow && lastAcceptedHeader is long stepPrevious && stepNow != stepPrevious)
                    {
                        headerSteps.Add(stepNow - stepPrevious);
                    }

                    Analysis.Compare(lastAccepted, current, change);
                }

                lastAccepted = current;
                if (header.HasValue)
                {
                    lastAcceptedHeader = header;
                }

                lastProcessedSha = sha;
                acceptedSnapshots++;
            }

            lastRows = snapRows;
            lastReceivedAt = receivedAt;
        }
    }
    catch (InvalidProtocolBufferException)
    {
        Count("Bozuk protobuf");
    }
    catch (TaskCanceledException)
    {
        Count("Zaman asimi");
    }
    catch (HttpRequestException ex)
    {
        Count($"Ag hatasi ({ex.GetType().Name})");
    }
    catch (Exception ex)
    {
        Count($"Diger hata ({ex.GetType().Name})");
    }

    if (pollIndex % 20 == 0)
    {
        Console.WriteLine(
            $"  ... poll {pollIndex} | gecen {(int)clock.Elapsed.TotalSeconds} sn | kabul edilen snapshot: {acceptedSnapshots}");
    }

    var wait = TimeSpan.FromSeconds(intervalSeconds) - cycle.Elapsed;
    if (wait > TimeSpan.Zero)
    {
        await Task.Delay(wait);
    }
}

// ===========================================================================
// RAPOR
// ===========================================================================
string MinAvgMax(Func<EntityStats, int> selector)
{
    if (statsPerPoll.Count == 0)
    {
        return "-";
    }

    var values = statsPerPoll.Select(selector).ToList();
    return $"min {values.Min():N0} | ort {values.Average():N0} | max {values.Max():N0}";
}

P();
P("==================================================");
P("RAPOR");
P("==================================================");

if (polls.Count == 0)
{
    P("Hic basarili poll yok. Hata ozeti:");
    foreach (var failure in failures)
    {
        P($"  {failure.Key}: {failure.Value}");
    }

    File.WriteAllText($"feed-probe-report-{DateTime.UtcNow:yyyyMMdd-HHmmss}.txt", report.ToString(), Encoding.UTF8);
    return 2;
}

P($"Bitis (UTC)               : {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}  (Norvec yerel saati = UTC+1, Kasim)");
P($"Toplam poll / basarili    : {pollIndex:N0} / {polls.Count:N0}");
P($"Hatalar                   : {(failures.Count == 0 ? "yok" : string.Join("; ", failures.Select(f => $"{f.Key}={f.Value}")))}");
P();

P("1) SURE VE BOYUT");
P($"  HTTP (govde dahil)      : {Analysis.Summ(requestMs, "ms")}");
P($"  Protobuf parse          : {Analysis.Summ(parseMs, "ms")}");
P($"  Payload boyutu          : {Analysis.Summ(payloadBytes, "byte")}");
P($"  Entity sayisi / poll    : {Analysis.Summ(polls.Select(p => (double)p.Entities).ToList(), "")}");
P();

P("2) SNAPSHOT DAVRANISI");
P($"  Farkli payload (SHA256)          : {allShas.Count:N0} / {polls.Count:N0} poll");
P($"  Onceki ile ayni payload          : {duplicatePayloads:N0}");
P($"  Header zamani GERIYE giden       : {rewinds:N0}" +
  (rewindBehind.Count > 0 ? $" (en fazla {rewindBehind.Max():N0} sn geride)" : ""));
P($"  Kabul edilen (ileri) snapshot    : {acceptedSnapshots:N0}");
P($"  Farkli header zamani sayisi      : {polls.Where(p => p.Header.HasValue).Select(p => p.Header).Distinct().Count():N0}");
var sameHeaderDifferentPayload = polls
    .Where(p => p.Header.HasValue)
    .GroupBy(p => p.Header)
    .Count(grp => grp.Select(p => p.Sha).Distinct().Count() > 1);
P($"  AYNI header, FARKLI payload      : {sameHeaderDifferentPayload:N0} header degeri");
P($"  Header adimi (ileri)             : {Analysis.Summ(headerSteps, "sn")}");
P($"  Kaynak gecikmesi (alinma-header) : {Analysis.Summ(lagSeconds, "sn")}");
P();

P("3) VERI KALITESI (poll basina)");
P($"  Entity (vehicle mesaji olan)     : {MinAvgMax(s => s.Total - s.NoVehicleMessage)}");
P($"  Vehicle mesaji OLMAYAN entity    : {MinAvgMax(s => s.NoVehicleMessage)}");
P($"  VehicleDescriptor yok            : {MinAvgMax(s => s.NoDescriptor)}");
P($"  VehicleId bos string             : {MinAvgMax(s => s.IdEmpty)}");
P($"  VehicleId sadece bosluk          : {MinAvgMax(s => s.IdWhitespace)}");
P($"  VehicleId == FeedEntityId        : {MinAvgMax(s => s.IdEqualsEntityId)}");
P($"  VehicleId var, FeedEntityId'den farkli: {MinAvgMax(s => s.IdUsableDifferent)}");
P($"  Zaman damgasi YOK                : {MinAvgMax(s => s.NoTimestamp)}");
P($"  Zaman damgasi 0                  : {MinAvgMax(s => s.ZeroTimestamp)}");
P($"  Position yok                     : {MinAvgMax(s => s.NoPosition)}");
P($"  Koordinat (0,0)                  : {MinAvgMax(s => s.ZeroCoords)}");
P($"  Gecersiz koordinat               : {MinAvgMax(s => s.BadCoords)}");
P($"  TripId yok/bos                   : {MinAvgMax(s => s.NoTripId)}");
P($"  RouteId yok/bos                  : {MinAvgMax(s => s.NoRouteId)}");
P($"  StopId yok/bos                   : {MinAvgMax(s => s.NoStopId)}");
P($"  CurrentStopSequence yok          : {MinAvgMax(s => s.NoStopSequence)}");
P($"  CurrentStatus yok                : {MinAvgMax(s => s.NoStatus)}");
P();

if (lastRows != null)
{
    var usable = lastRows.Where(r => !string.IsNullOrWhiteSpace(r.VehicleId)).ToList();
    var unusable = lastRows.Where(r => string.IsNullOrWhiteSpace(r.VehicleId)).ToList();
    var groups = usable
        .GroupBy(r => r.VehicleId)
        .Where(grp => grp.Count() > 1)
        .OrderByDescending(grp => grp.Count())
        .ToList();

    P("4) VEHICLE ID ANALIZI (son snapshot)");
    P($"  Entity sayisi                    : {lastRows.Count:N0}");
    P($"  Kullanilabilir VehicleId         : {usable.Count:N0} (benzersiz: {usable.Select(r => r.VehicleId).Distinct().Count():N0})");
    P($"  VehicleId yok/bos                : {unusable.Count:N0}");
    P($"    bunlardan Label'i olan         : {unusable.Count(r => r.Label != null):N0}");
    P($"    bunlardan TripId'si olan       : {unusable.Count(r => r.TripId != null):N0}");
    P($"  Birden fazla entity'li VehicleId : {groups.Count:N0} grup (toplam {groups.Sum(grp => grp.Count()):N0} entity)");

    foreach (var grp in groups.Take(4))
    {
        P($"    VehicleId={grp.Key} | entity={grp.Count()} | farkli TripId={grp.Select(r => r.TripId).Distinct().Count()} | farkli zaman damgasi={grp.Select(r => r.Timestamp).Distinct().Count()}");
        P($"      entity ornekleri: {string.Join(", ", grp.Take(3).Select(r => r.EntityId))}");
    }

    P("  Ornek satirlar (EntityId | VehicleId | Label | TripId):");
    foreach (var sample in usable.Take(3))
    {
        P($"    {sample.EntityId} | {sample.VehicleId} | {sample.Label ?? "-"} | {sample.TripId ?? "-"}");
    }

    foreach (var sample in unusable.Take(3))
    {
        P($"    [VehicleId bos] {sample.EntityId} | '{sample.VehicleId}' | {sample.Label ?? "-"} | {sample.TripId ?? "-"}");
    }

    P();

    // --- Zaman damgasi yas dagilimi ---
    var nowUnix = new DateTimeOffset(lastReceivedAt).ToUnixTimeSeconds();
    var labels = new[]
    {
        "zaman damgasi YOK", "gelecek (>60 sn)", "< 1 dk", "1-5 dk", "5-30 dk", "30 dk - 3 sa", "3-24 sa", "> 24 sa"
    };
    var counts = new int[labels.Length];

    foreach (var row in lastRows)
    {
        if (row.Timestamp is not long rowTimestamp)
        {
            counts[0]++;
            continue;
        }

        var age = nowUnix - rowTimestamp;
        var bucket = age < -60 ? 1
            : age < 60 ? 2
            : age < 300 ? 3
            : age < 1800 ? 4
            : age < 10800 ? 5
            : age < 86400 ? 6
            : 7;
        counts[bucket]++;
    }

    P("5) ZAMAN DAMGASI YASI (son snapshot)");
    for (var i = 0; i < labels.Length; i++)
    {
        var percent = lastRows.Count == 0 ? 0 : 100.0 * counts[i] / lastRows.Count;
        P($"  {labels[i],-20}: {counts[i],6:N0}  (%{percent:0.0})");
    }

    P();

    // --- 3 saatten eski olanlar kosu boyunca hic guncellendi mi? ---
    if (firstRows != null && firstAcceptedReceivedAt != default)
    {
        var firstUnix = new DateTimeOffset(firstAcceptedReceivedAt).ToUnixTimeSeconds();
        var lastTimestampById = new Dictionary<string, long>();
        foreach (var row in lastRows)
        {
            if (row.Timestamp.HasValue)
            {
                lastTimestampById[row.EntityId] = row.Timestamp.Value;
            }
        }

        var staleAtStart = 0;
        var staleAdvanced = 0;
        var staleMissingAtEnd = 0;

        foreach (var row in firstRows)
        {
            if (!row.Timestamp.HasValue || row.Timestamp.Value >= firstUnix - 10800)
            {
                continue;
            }

            staleAtStart++;

            if (!lastTimestampById.TryGetValue(row.EntityId, out var finalTimestamp))
            {
                staleMissingAtEnd++;
                continue;
            }

            if (finalTimestamp > row.Timestamp.Value)
            {
                staleAdvanced++;
            }
        }

        P("6) ESKI (>3 SA) KAYITLAR KOSU BOYUNCA GUNCELLENDI MI?");
        P($"  Basta 3 saatten eski             : {staleAtStart:N0}");
        P($"  Bunlardan zaman damgasi ilerleyen: {staleAdvanced:N0}");
        P($"  Bunlardan sonda feed'de olmayan  : {staleMissingAtEnd:N0}");
        P();
    }
}

P("7) DEGISIM ANALIZI (kabul edilen ardisik snapshot'lar, FeedEntityId bazinda)");
P($"  Karsilastirilan entity cifti          : {change.Common:N0}");
P($"  Zaman damgasi ilerledi                : {change.TsAdvanced:N0}");
P($"  Zaman damgasi geriledi                : {change.TsBackward:N0}");
P($"  Zaman damgasi ayni                    : {change.TsSame:N0}");
P($"  Zaman damgasi (biri/ikisi) yok        : {change.TsMissingEither:N0}");
P($"  Konum degisti                         : {change.PositionChanged:N0}");
P($"  Ilerledi ama icerik ayni              : {change.AdvancedWithoutContentChange:N0}");
P($"  AYNI zaman damgasi + icerik degisti   : {change.SameTsContentChanged:N0} (bunlardan konum degisen: {change.SameTsPositionChanged:N0})");
foreach (var example in change.Examples)
{
    P($"    ornek: {example}");
}

P($"  Yeni entity / kaybolan entity         : {change.NewEntities:N0} / {change.Gone:N0}");
P($"  Adim basina yazilacak kayit           : {Analysis.Summ(change.StepWrites.Select(w => (double)w).ToList(), "kayit")}");
P();

P("8) YAZMA HACMI TAHMINI (sadece bu orneklem icin)");
if (firstAcceptedHeader is long headerFirst
    && lastAcceptedHeader is long headerLast
    && headerLast > headerFirst
    && change.StepWrites.Count > 0)
{
    var spanSeconds = headerLast - headerFirst;
    var totalWrites = change.StepWrites.Sum();
    var perSecond = (double)totalWrites / spanSeconds;
    var averageEntities = polls.Average(p => (double)p.Entities);
    var naivePerHour = averageEntities * 3600.0 / intervalSeconds;

    P($"  Kaynak zamani kapsami                 : {spanSeconds:N0} sn | adim: {change.StepWrites.Count:N0} | yazilacak toplam: {totalWrites:N0}");
    P($"  Tahmini yazma                         : {perSecond:N1} kayit/sn ~ {perSecond * 3600:N0} kayit/saat ~ {perSecond * 3600 * 3:N0} kayit/3 saat");
    P($"  Naif (her poll'da hepsi)              : ~{naivePerHour:N0} kayit/saat");
}
else
{
    P("  Hesaplanamadi (yeterli ilerleyen snapshot yok).");
}

P();
P("NOT: Bu degerler tek bir kosunun orneklemidir; gun ici/hafta ici degisimi");
P("temsil etmez. Kesin hacim/politika karari icin farkli saatlerde tekrarlayin.");
P("==================================================");

var reportPath = $"feed-probe-report-{DateTime.UtcNow:yyyyMMdd-HHmmss}.txt";
File.WriteAllText(reportPath, report.ToString(), Encoding.UTF8);
Console.WriteLine($"Rapor dosyasi: {Path.GetFullPath(reportPath)}");
return 0;

// ===========================================================================
// TIPLER
// ===========================================================================
sealed record EntityState(
    long? Ts,
    float Lat,
    float Lon,
    string? TripId,
    string? StopId,
    int? Status,
    float? Bearing,
    float? Speed);

sealed record Row(
    string EntityId,
    string VehicleId,
    bool HasDescriptor,
    string? Label,
    string? TripId,
    long? Timestamp,
    EntityState? State);

sealed record PollInfo(
    int Index,
    DateTime ReceivedAtUtc,
    long? Header,
    string Sha,
    int Bytes,
    int Entities);

sealed class EntityStats
{
    public int Total;
    public int NoVehicleMessage;
    public int NoDescriptor;
    public int IdEmpty;
    public int IdWhitespace;
    public int IdEqualsEntityId;
    public int IdUsableDifferent;
    public int NoTimestamp;
    public int ZeroTimestamp;
    public int NoPosition;
    public int ZeroCoords;
    public int BadCoords;
    public int NoTripId;
    public int NoRouteId;
    public int NoStopId;
    public int NoStopSequence;
    public int NoStatus;
}

sealed class ChangeTotals
{
    public int Common;
    public int NewEntities;
    public int Gone;
    public int PositionChanged;
    public int TsAdvanced;
    public int TsBackward;
    public int TsSame;
    public int TsMissingEither;
    public int AdvancedWithoutContentChange;
    public int SameTsContentChanged;
    public int SameTsPositionChanged;
    public List<int> StepWrites { get; } = new();
    public List<string> Examples { get; } = new();
}

static class Extractor
{
    public static (List<Row> Rows, EntityStats Stats) Extract(FeedMessage feed)
    {
        var stats = new EntityStats();
        var rows = new List<Row>(feed.Entity.Count);

        foreach (var entity in feed.Entity)
        {
            stats.Total++;

            var vehicle = entity.Vehicle;
            if (vehicle == null)
            {
                stats.NoVehicleMessage++;
                continue;
            }

            var descriptor = vehicle.Vehicle;
            var vehicleId = descriptor?.Id ?? string.Empty;
            var label = descriptor != null && descriptor.HasLabel && descriptor.Label.Length > 0
                ? descriptor.Label
                : null;

            if (descriptor == null)
            {
                stats.NoDescriptor++;
            }
            else if (vehicleId.Length == 0)
            {
                stats.IdEmpty++;
            }
            else if (string.IsNullOrWhiteSpace(vehicleId))
            {
                stats.IdWhitespace++;
            }
            else if (vehicleId == entity.Id)
            {
                stats.IdEqualsEntityId++;
            }
            else
            {
                stats.IdUsableDifferent++;
            }

            long? timestamp = null;
            if (!vehicle.HasTimestamp)
            {
                stats.NoTimestamp++;
            }
            else if (vehicle.Timestamp == 0)
            {
                stats.ZeroTimestamp++;
            }
            else
            {
                timestamp = (long)vehicle.Timestamp;
            }

            var trip = vehicle.Trip;
            var tripId = trip != null && trip.HasTripId && trip.TripId.Length > 0 ? trip.TripId : null;
            var routeId = trip != null && trip.HasRouteId && trip.RouteId.Length > 0 ? trip.RouteId : null;
            var stopId = vehicle.HasStopId && vehicle.StopId.Length > 0 ? vehicle.StopId : null;

            if (tripId == null) stats.NoTripId++;
            if (routeId == null) stats.NoRouteId++;
            if (stopId == null) stats.NoStopId++;
            if (!vehicle.HasCurrentStopSequence) stats.NoStopSequence++;
            if (!vehicle.HasCurrentStatus) stats.NoStatus++;

            EntityState? state = null;
            var position = vehicle.Position;

            if (position == null)
            {
                stats.NoPosition++;
            }
            else
            {
                var latitude = position.Latitude;
                var longitude = position.Longitude;

                if (latitude == 0 && longitude == 0)
                {
                    stats.ZeroCoords++;
                }

                if (float.IsNaN(latitude) || float.IsNaN(longitude)
                    || latitude < -90 || latitude > 90
                    || longitude < -180 || longitude > 180)
                {
                    stats.BadCoords++;
                }

                int? status = vehicle.HasCurrentStatus ? (int)vehicle.CurrentStatus : null;
                float? bearing = position.HasBearing ? position.Bearing : null;
                float? speed = position.HasSpeed ? position.Speed : null;

                state = new EntityState(timestamp, latitude, longitude, tripId, stopId, status, bearing, speed);
            }

            rows.Add(new Row(entity.Id, vehicleId, descriptor != null, label, tripId, timestamp, state));
        }

        return (rows, stats);
    }
}

static class Analysis
{
    public static Dictionary<string, EntityState> BuildState(List<Row> rows)
    {
        var dictionary = new Dictionary<string, EntityState>(rows.Count);
        foreach (var row in rows)
        {
            if (row.State != null)
            {
                dictionary[row.EntityId] = row.State;
            }
        }

        return dictionary;
    }

    // Zaman damgasi HARIC icerik karsilastirmasi.
    public static bool SameContent(EntityState a, EntityState b) =>
        a.Lat == b.Lat
        && a.Lon == b.Lon
        && a.TripId == b.TripId
        && a.StopId == b.StopId
        && a.Status == b.Status
        && a.Bearing == b.Bearing
        && a.Speed == b.Speed;

    // Yazma kurali (taslak): zaman damgasi ilerledi VEYA (ayni/yok VE icerik degisti).
    public static void Compare(
        Dictionary<string, EntityState> previous,
        Dictionary<string, EntityState> current,
        ChangeTotals totals)
    {
        var stepWrites = 0;
        var commonThisStep = 0;

        foreach (var (id, now) in current)
        {
            if (!previous.TryGetValue(id, out var before))
            {
                totals.NewEntities++;
                stepWrites++;
                continue;
            }

            commonThisStep++;
            totals.Common++;

            var contentChanged = !SameContent(before, now);
            var positionChanged = before.Lat != now.Lat || before.Lon != now.Lon;
            if (positionChanged)
            {
                totals.PositionChanged++;
            }

            var write = false;

            if (before.Ts.HasValue && now.Ts.HasValue)
            {
                if (now.Ts.Value > before.Ts.Value)
                {
                    totals.TsAdvanced++;
                    write = true;
                    if (!contentChanged)
                    {
                        totals.AdvancedWithoutContentChange++;
                    }
                }
                else if (now.Ts.Value < before.Ts.Value)
                {
                    totals.TsBackward++;
                }
                else
                {
                    totals.TsSame++;
                    if (contentChanged)
                    {
                        write = true;
                        totals.SameTsContentChanged++;
                        if (positionChanged)
                        {
                            totals.SameTsPositionChanged++;
                        }

                        if (totals.Examples.Count < 5)
                        {
                            totals.Examples.Add(
                                $"{id} ts={now.Ts} ({before.Lat:0.00000},{before.Lon:0.00000}) -> ({now.Lat:0.00000},{now.Lon:0.00000})");
                        }
                    }
                }
            }
            else
            {
                totals.TsMissingEither++;
                write = contentChanged;
            }

            if (write)
            {
                stepWrites++;
            }
        }

        totals.Gone += previous.Count - commonThisStep;
        totals.StepWrites.Add(stepWrites);
    }

    public static string Summ(List<double> values, string unit)
    {
        if (values.Count == 0)
        {
            return "veri yok";
        }

        var sorted = values.OrderBy(v => v).ToList();
        double At(double fraction) => sorted[(int)Math.Floor(fraction * (sorted.Count - 1))];

        return $"min {sorted[0]:N0} | p50 {At(0.5):N0} | p95 {At(0.95):N0} | max {sorted[sorted.Count - 1]:N0} {unit}".TrimEnd();
    }
}
