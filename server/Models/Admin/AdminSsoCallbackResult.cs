namespace server.Models.Admin;

public sealed record AdminSsoCallbackResult(
    AdminSsoCallbackStatus Status,
    string? Ticket = null,
    string? ReturnPath = null,
    string? Subject = null,
    string? Detail = null);
