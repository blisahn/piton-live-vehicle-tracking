namespace VehicleTracking.Application.Common;

/// <summary>The caller asked for something that cannot be answered as asked. Becomes an HTTP 400.</summary>
public sealed class RequestValidationException(string message) : Exception(message);
