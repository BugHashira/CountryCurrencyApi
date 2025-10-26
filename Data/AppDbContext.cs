using CountryCurrencyApi.Models;
using Microsoft.EntityFrameworkCore;

namespace CountryCurrencyApi.Data;

public class AppDbContext : DbContext
{
    public DbSet<Country> Countries { get; set; } = null!;

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Country>()
            .HasIndex(c => c.Name)
            .IsUnique();

        base.OnModelCreating(modelBuilder);
    }
}
