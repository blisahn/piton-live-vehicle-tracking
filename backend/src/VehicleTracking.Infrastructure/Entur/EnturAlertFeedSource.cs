using Google.Protobuf;
using Microsoft.Extensions.Options;
using VehicleTracking.Application.Abstractions;
using VehicleTracking.Application.Alerts;
using VehicleTracking.Application.Configuration;

namespace VehicleTracking.Infrastructure.Entur;

/// <summary>
/// Downloads the Entur GTFS-Realtime service alerts and translates them field by field.
/// Language tags are passed on as published; guessing missing ones is the application's job.
/// </summary>
internal sealed class EnturAlertFeedSource(HttpClient http, IOptions<AlertOptions> options) : IAlertFeedSource
{
    public async Task<AlertFeedSnapshot> ReadAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var response = await http.GetAsync(
                options.Value.Url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw new FeedReadException($"The alerts feed answered with HTTP {(int)response.StatusCode}.");

            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            var feed = TransitRealtime.FeedMessage.Parser.ParseFrom(bytes);

            return Map(feed);
        }
        catch (HttpRequestException ex)
        {
            throw new FeedReadException($"The alerts feed could not be reached: {ex.Message}", ex);
        }
        catch (InvalidProtocolBufferException ex)
        {
            throw new FeedReadException($"The alerts feed content could not be parsed: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new FeedReadException("The alerts feed request timed out.", ex);
        }
    }

    private static AlertFeedSnapshot Map(TransitRealtime.FeedMessage feed)
    {
        DateTimeOffset? generatedAt = feed.Header.HasTimestamp
            ? DateTimeOffset.FromUnixTimeSeconds((long)feed.Header.Timestamp)
            : null;

        var alerts = new List<ServiceAlert>(feed.Entity.Count);
        foreach (var entity in feed.Entity)
        {
            if (entity.IsDeleted || entity.Alert is null) continue;
            var alert = entity.Alert;

            alerts.Add(new ServiceAlert(
                entity.Id,
#pragma warning disable CS0612 // active_period is deprecated in the spec, but it is the one Entur fills in.
                alert.ActivePeriod.Select(p => new AlertPeriod(
                    p.HasStart ? DateTimeOffset.FromUnixTimeSeconds((long)p.Start) : null,
                    p.HasEnd ? DateTimeOffset.FromUnixTimeSeconds((long)p.End) : null)).ToList(),
#pragma warning restore CS0612
                alert.InformedEntity.Select(e => new AlertTarget(
                    e.HasRouteId ? e.RouteId : null,
                    e.Trip is { HasTripId: true } ? e.Trip.TripId : null,
                    e.HasStopId ? e.StopId : null)).ToList(),
                Texts(alert.HeaderText),
                Texts(alert.DescriptionText),
                alert.Url?.Translation.Select(t => t.Text).FirstOrDefault(t => !string.IsNullOrWhiteSpace(t))));
        }

        return new AlertFeedSnapshot(generatedAt, alerts);
    }

    private static List<AlertText> Texts(TransitRealtime.TranslatedString? translated) =>
        translated is null
            ? []
            : translated.Translation
                .Select(t => new AlertText(t.Text, t.HasLanguage ? t.Language : null, LanguageDetected: false))
                .ToList();
}
