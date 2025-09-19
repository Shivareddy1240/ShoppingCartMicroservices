using Microsoft.EntityFrameworkCore;

namespace Order.API.Data;

public class OrderContext : DbContext
{
    public OrderContext(DbContextOptions<OrderContext> options) : base(options) { }
    public DbSet<Models.Order> Orders { get; set; } = null!;
    public DbSet<Models.OrderItem> OrderItems { get; set; } = null!;
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Models.Order>()
            .HasMany(o => o.Items)
            .WithOne(oi => oi.Order)
            .HasForeignKey(oi => oi.OrderId);
        modelBuilder.Entity<Models.OrderItem>().Property(oi => oi.Price).HasColumnType("decimal(18,2)");
    }
}
