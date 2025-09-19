using RabbitMQ.Client;
using System.Text;
using System.Text.Json;
using ShoppingCart.API.Models;
using Microsoft.Extensions.Logging;

namespace ShoppingCart.API.Services;

public class MessageQueueService : IMessageQueueService, IDisposable
{
    private readonly ILogger<MessageQueueService> _logger;
    private bool _disposed;

    public MessageQueueService(ILogger<MessageQueueService> logger)
    {
        _logger = logger;
    }

    public async Task PublishCheckoutEventAsync(Cart cart)
    {
        if (cart == null || !cart.Items.Any())
        {
            _logger.LogWarning("Attempted to publish checkout event with empty or null cart for user {UserId}.", cart?.UserId);
            return;
        }

        var factory = new ConnectionFactory
        {
            HostName = "localhost",
            Port = 5672,
            UserName = "guest",
            Password = "guest",
            AutomaticRecoveryEnabled = true
        };

        try
        {
            using var connection = await factory.CreateConnectionAsync();
            using var channel = await connection.CreateChannelAsync();

            await channel.QueueDeclareAsync(queue: "cart.checkout",
                                            durable: false,
                                            exclusive: false,
                                            autoDelete: false,
                                            arguments: null);

            // Serialize only necessary fields to avoid cycles
            var message = new
            {
                UserId = cart.UserId,
                Items = cart.Items.Select(i => new
                {
                    i.ProductId,
                    i.ProductName,
                    i.Quantity,
                    i.Price
                }).ToList()
            };
            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));

            await channel.BasicPublishAsync(exchange: "",
                                           routingKey: "cart.checkout",
                                           body: body);

            _logger.LogInformation("Published checkout event for user {UserId} to queue 'cart.checkout'.", cart.UserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish checkout event for user {UserId}. Check if RabbitMQ is running in Docker.", cart.UserId);
            throw;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _logger.LogInformation("MessageQueueService disposed.");
    }
}

public interface IMessageQueueService
{
    Task PublishCheckoutEventAsync(Cart cart);
}