namespace server.Models.Passport;

public sealed record PassportSsoCallbackResult(
    PassportSsoCallbackStatus Status,
    string? Ticket = null,
    string? Subject = null,
    string? Detail = null);
