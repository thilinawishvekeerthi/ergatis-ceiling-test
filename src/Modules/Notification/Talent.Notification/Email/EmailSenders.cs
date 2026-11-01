using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Talent.Notification.Email;

/// <summary>Bound from <c>Notification:Email</c>.</summary>
internal sealed class EmailOptions
{
    public const string Section = "Notification:Email";

    /// <summary><c>Log</c> records that an email would go out but sends nothing (development and tests). <c>Smtp</c> sends for real.</summary>
    public string Mode { get; set; } = "Log";

    public string From { get; set; } = "";
    public string Host { get; set; } = "";
    public int Port { get; set; } = 25;
    public bool EnableSsl { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }

    /// <summary>How often the mail queue is checked. 0 turns the worker off (tests call the delivery service directly).</summary>
    public int PollSeconds { get; set; } = 5;

    public int BatchSize { get; set; } = 20;

    public bool IsSmtp => string.Equals(Mode, "Smtp", StringComparison.OrdinalIgnoreCase);
}

internal sealed class EmailOptionsValidator : IValidateOptions<EmailOptions>
{
    public ValidateOptionsResult Validate(string? name, EmailOptions options)
    {
        var errors = new List<string>();
        if (!options.IsSmtp && !string.Equals(options.Mode, "Log", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add($"Notification:Email:Mode '{options.Mode}' is not supported. Use Log or Smtp.");
        }

        if (options.IsSmtp && (string.IsNullOrWhiteSpace(options.Host) || string.IsNullOrWhiteSpace(options.From)))
        {
            errors.Add("Notification:Email:Host and From are required when Mode is Smtp.");
        }

        if (options.Port is < 1 or > 65535)
        {
            errors.Add("Notification:Email:Port must be 1 to 65535.");
        }

        if (options.BatchSize < 1)
        {
            errors.Add("Notification:Email:BatchSize must be at least 1.");
        }

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}

/// <summary>Sends one email. A failure throws; the delivery service records it and tries again later.</summary>
internal interface IEmailSender
{
    Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken);
}

/// <summary>Development sender: sends nothing. It logs that a mail was handled, never the address or the text (personal data).</summary>
internal sealed partial class LogEmailSender(ILogger<LogEmailSender> logger) : IEmailSender
{
    public Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken)
    {
        LogNotSent(logger);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Email mode is Log: a message was accepted but not sent.")]
    private static partial void LogNotSent(ILogger logger);
}

internal sealed class SmtpEmailSender(IOptions<EmailOptions> options) : IEmailSender
{
    public async Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken)
    {
        var o = options.Value;
        using var client = new SmtpClient(o.Host, o.Port) { EnableSsl = o.EnableSsl, Timeout = 15_000 };
        if (!string.IsNullOrEmpty(o.Username))
        {
            client.Credentials = new NetworkCredential(o.Username, o.Password);
        }

        using var message = new MailMessage(o.From, to, subject, body) { IsBodyHtml = false };
        await client.SendMailAsync(message, cancellationToken);
    }
}
