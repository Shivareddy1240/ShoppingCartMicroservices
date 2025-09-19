using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;
using Order.API.Data;
using Order.API.Models;
using Microsoft.Extensions.Logging;

namespace Order.API.Services;

public class OrderProcessingService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OrderProcessingService> _logger;

    public OrderProcessingService(IServiceProvider serviceProvider, ILogger<OrderProcessingService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
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

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += async (model, ea) =>
            {
                try
                {
                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);
                    var data = JsonSerializer.Deserialize<CheckoutMessage>(message);

                    if (data != null && data.Items.Any())
                    {
                        using var scope = _serviceProvider.CreateScope();
                        var context = scope.ServiceProvider.GetRequiredService<OrderContext>();

                        var order = new Models.Order
                        {
                            UserId = data.UserId,
                            CreatedAt = DateTime.UtcNow,
                            Items = data.Items.Select(i => new OrderItem
                            {
                                ProductId = i.ProductId,
                                ProductName = i.ProductName,
                                Quantity = i.Quantity,
                                Price = i.Price
                            }).ToList()
                        };

                        await context.Orders.AddAsync(order);
                        await context.SaveChangesAsync(stoppingToken);
                        _logger.LogInformation("Processed order for user {UserId}.", data.UserId);
                    }
                    else
                    {
                        _logger.LogWarning("Received invalid or empty message from cart.checkout queue.");
                    }

                    await channel.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process message from cart.checkout queue.");
                    await channel.BasicNackAsync(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true);
                }
            };

            await channel.BasicConsumeAsync(queue: "cart.checkout",
                                            autoAck: false,
                                            consumer: consumer);

            _logger.LogInformation("RabbitMQ consumer started for queue 'cart.checkout'.");
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize RabbitMQ consumer. Check if RabbitMQ is running in Docker.");
            throw;
        }
    }
}

public class CheckoutMessage
{
    public string UserId { get; set; } = string.Empty;
    public List<CheckoutItem> Items { get; set; } = new();
}

public class CheckoutItem
{
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Price { get; set; }
}