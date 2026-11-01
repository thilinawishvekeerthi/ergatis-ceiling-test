using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Options;
using Talent.Notification.Email;
using static Talent.Notification.IntegrationTests.NotificationHostFixture;

namespace Talent.Notification.IntegrationTests;

/// <summary>The real SMTP sender talking to a tiny in-process SMTP server, so "email delivery" is proven on the wire.</summary>
public sealed class SmtpSenderTests
{
    [Fact]
    public async Task SmtpSender_DeliversFromToSubjectAndBody()
    {
        using var server = new FakeSmtpServer();
        var sender = new SmtpEmailSender(Options.Create(new EmailOptions { Mode = "Smtp", Host = "127.0.0.1", Port = server.Port, From = "talent@example.com" }));

        await sender.SendAsync("ada@example.com", "Review cycle 2026 is open", "Please rate your team.", Ct);

        var mail = await server.ReceivedAsync();
        mail.From.ShouldBe("talent@example.com");
        mail.To.ShouldBe("ada@example.com");
        mail.Data.ShouldContain("Subject: Review cycle 2026 is open");
        mail.Data.ShouldContain("Please rate your team.");
    }

    [Fact]
    public async Task SmtpSender_WhenServerIsDown_Throws_SoTheQueueCanRetry()
    {
        var port = FakeSmtpServer.FreePort();
        var sender = new SmtpEmailSender(Options.Create(new EmailOptions { Mode = "Smtp", Host = "127.0.0.1", Port = port, From = "talent@example.com" }));

        await Should.ThrowAsync<Exception>(() => sender.SendAsync("ada@example.com", "S", "B", Ct));
    }

    [Theory]
    [InlineData("Pigeon", 25, "Mode")]
    [InlineData("Smtp", 25, "Host")]
    [InlineData("Log", 0, "Port")]
    public void Validator_RejectsBadSettings(string mode, int port, string mentions)
    {
        var result = new EmailOptionsValidator().Validate(null, new EmailOptions { Mode = mode, Port = port });

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(mentions);
    }

    [Fact]
    public void Validator_AcceptsLogAndAFullSmtpSetup()
    {
        new EmailOptionsValidator().Validate(null, new EmailOptions { Mode = "Log" }).Succeeded.ShouldBeTrue();
        new EmailOptionsValidator().Validate(null, new EmailOptions { Mode = "smtp", Host = "mail.example.com", From = "talent@example.com" }).Succeeded.ShouldBeTrue();
    }

    private sealed class FakeSmtpServer : IDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly Task<(string From, string To, string Data)> _received;

        public FakeSmtpServer()
        {
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            _received = Task.Run(HandleOneAsync);
        }

        public int Port { get; }

        public static int FreePort()
        {
            var l = new TcpListener(IPAddress.Loopback, 0);
            l.Start();
            var port = ((IPEndPoint)l.LocalEndpoint).Port;
            l.Stop();
            return port;
        }

        public Task<(string From, string To, string Data)> ReceivedAsync() => _received.WaitAsync(TimeSpan.FromSeconds(15), Ct);

        public void Dispose() => _listener.Stop();

        private async Task<(string From, string To, string Data)> HandleOneAsync()
        {
            using var client = await _listener.AcceptTcpClientAsync(Ct);
            using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII);
            await using var writer = new StreamWriter(stream, Encoding.ASCII) { NewLine = "\r\n", AutoFlush = true };
            string from = "", to = "";
            var data = new StringBuilder();

            await writer.WriteLineAsync("220 fake ready");
            while (await reader.ReadLineAsync(Ct) is { } line)
            {
                if (line.StartsWith("EHLO", StringComparison.OrdinalIgnoreCase) || line.StartsWith("HELO", StringComparison.OrdinalIgnoreCase))
                {
                    await writer.WriteLineAsync("250 fake");
                }
                else if (line.StartsWith("MAIL FROM:", StringComparison.OrdinalIgnoreCase))
                {
                    from = line["MAIL FROM:".Length..].Trim().Trim('<', '>');
                    await writer.WriteLineAsync("250 ok");
                }
                else if (line.StartsWith("RCPT TO:", StringComparison.OrdinalIgnoreCase))
                {
                    to = line["RCPT TO:".Length..].Trim().Trim('<', '>');
                    await writer.WriteLineAsync("250 ok");
                }
                else if (line.Equals("DATA", StringComparison.OrdinalIgnoreCase))
                {
                    await writer.WriteLineAsync("354 go");
                    while (await reader.ReadLineAsync(Ct) is { } body && body != ".")
                    {
                        data.AppendLine(body);
                    }

                    await writer.WriteLineAsync("250 queued");
                }
                else if (line.Equals("QUIT", StringComparison.OrdinalIgnoreCase))
                {
                    await writer.WriteLineAsync("221 bye");
                    break;
                }
                else
                {
                    await writer.WriteLineAsync("250 ok");
                }
            }

            return (from, to, data.ToString());
        }
    }
}
