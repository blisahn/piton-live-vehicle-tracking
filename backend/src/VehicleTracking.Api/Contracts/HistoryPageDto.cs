namespace VehicleTracking.Api.Contracts;

/// <summary>One page of a vehicle's history, newest first.</summary>
/// <param name="NextCursor">Send as the 'cursor' query parameter to get the next page; null on the last page.</param>
public sealed record HistoryPageDto(IReadOnlyList<VehicleDto> Items, DateTimeOffset? NextCursor);
