using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using RabbitMQ.Client;
using Shared;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var amqpUri = builder.Configuration["Amqp:Uri"]
             ?? Environment.GetEnvironmentVariable("AMQP_URI")
             ?? "amqp://guest:guest@localhost:5672/";

var sqlCs = builder.Configuration["Sql:ConnectionString"]
          ?? Environment.GetEnvironmentVariable("SQL_CS")
          ?? "Server=localhost\\SQLEXPRESS;Database=NotificationDb;Trusted_Connection=True;TrustServerCertificate=True;";

var queueName = builder.Configuration["Queues:EmailSend"] ?? "q.email.send";

var logStore = new MsSqlEmailLogStore(sqlCs);

app.MapPost("/api/email/enqueue", async ([FromBody] EmailMessage input) =>
{
    if (input is null) return Results.BadRequest("Body required.");
    if (input.To is null || input.To.Length == 0) return Results.BadRequest("'to' is required.");

    if (input.MessageId == Guid.Empty) input.MessageId = Guid.NewGuid();
    if (input.RequestedAtUtc == default) input.RequestedAtUtc = DateTime.UtcNow;

    // 1) SQL -> Queued
    await logStore.UpsertAsync(
        input.MessageId,
        status: "Queued",
        toList: string.Join(";", input.To),
        subject: input.Subject,
        requestedAtUtc: input.RequestedAtUtc
    );

    // 2) RabbitMQ -> publish
    var factory = new ConnectionFactory { Uri = new Uri(amqpUri) };
    await using var conn = await factory.CreateConnectionAsync();
    await using var ch = await conn.CreateChannelAsync();

    await ch.QueueDeclareAsync(queue: queueName, durable: true, exclusive: false, autoDelete: false);

    var json = JsonSerializer.Serialize(input);
    var body = Encoding.UTF8.GetBytes(json);

    var props = new BasicProperties
    {
        Persistent = true,
        MessageId = input.MessageId.ToString(),
        Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
    };

    await ch.BasicPublishAsync(
        exchange: "",
        routingKey: queueName,
        mandatory: false,
        basicProperties: props,
        body: body
    );

    return Results.Ok(new { input.MessageId, Status = "Queued" });
});

app.MapGet("/api/email/status/{messageId:guid}", async (Guid messageId) =>
{
    var row = await logStore.GetAsync(messageId);
    return row is null ? Results.NotFound() : Results.Ok(row);
});

app.MapGet("/api/email/recent", async ([FromQuery] int take = 20) =>
{
    take = Math.Clamp(take, 1, 200);
    var rows = await logStore.GetRecentAsync(take);
    return Results.Ok(rows);
});

app.MapGet("/health", () => Results.Ok("ok"));

app.Run();
