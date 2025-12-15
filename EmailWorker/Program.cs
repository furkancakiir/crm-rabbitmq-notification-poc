using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shared;
using System.Text;
using System.Text.Json;

var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .AddEnvironmentVariables()
    .Build();

var amqpUri = config["Amqp:Uri"] ?? throw new Exception("Missing config: Amqp:Uri");
var sqlCs = config["Sql:ConnectionString"] ?? throw new Exception("Missing config: Sql:ConnectionString");
var queueName = config["Queues:EmailSend"] ?? "q.email.send";

var logStore = new MsSqlEmailLogStore(sqlCs);

var factory = new ConnectionFactory { Uri = new Uri(amqpUri) };
await using var conn = await factory.CreateConnectionAsync();
await using var ch = await conn.CreateChannelAsync();

await ch.QueueDeclareAsync(queue: queueName, durable: true, exclusive: false, autoDelete: false);
await ch.BasicQosAsync(0, 5, false);

Console.WriteLine($"[WORKER] Listening on {queueName}...");

var consumer = new AsyncEventingBasicConsumer(ch);
consumer.ReceivedAsync += async (_, ea) =>
{
    EmailMessage? msg = null;

    try
    {
        var json = Encoding.UTF8.GetString(ea.Body.ToArray());
        msg = JsonSerializer.Deserialize<EmailMessage>(json);

        if (msg is null) throw new Exception("Deserialized message is null.");

        await logStore.UpsertAsync(msg.MessageId, "Processing");

        
        Console.WriteLine($"[MAIL] To={string.Join(",", msg.To)} | Subject={msg.Subject}");

        await logStore.UpsertAsync(msg.MessageId, "Sent");
        await ch.BasicAckAsync(ea.DeliveryTag, multiple: false);
    }
    catch (Exception ex)
    {
        if (msg is not null)
            await logStore.UpsertAsync(msg.MessageId, "Failed", errorDetail: ex.Message);

        await ch.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false);
    }
};

await ch.BasicConsumeAsync(queue: queueName, autoAck: false, consumer: consumer);

Console.ReadLine();
