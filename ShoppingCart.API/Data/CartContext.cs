using Microsoft.EntityFrameworkCore;
using ShoppingCart.API.Models;

namespace ShoppingCart.API.Data;

public class CartContext : DbContext
{
    public CartContext(DbContextOptions<CartContext> options) : base(options) { }
    public DbSet<Cart> Carts { get; set; }
    public DbSet<CartItem> CartItems { get; set; }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Cart>()
            .HasMany(c => c.Items)
            .WithOne(ci => ci.Cart)
            .HasForeignKey(ci => ci.CartId);
        modelBuilder.Entity<CartItem>().Property(ci => ci.Price).HasColumnType("decimal(18,2)");
    }
}
