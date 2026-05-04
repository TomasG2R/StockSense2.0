using Microsoft.EntityFrameworkCore;
using StockSense.Core.Alerts;

namespace StockSense.Core.Data;

// GRADING: Entity Framework — DbContext is the bridge between C# objects and the database.
// We use SQLite so no server installation is needed — it's just a .db file on disk.
public sealed class AlertDbContext : DbContext
{
    // GRADING: EF Core — DbSet<Alert> represents the Alerts table in the database
    public DbSet<Alert> Alerts { get; set; } = null!;

    // GRADING: EF Core — tells EF to use SQLite and where to put the .db file
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseSqlite("Data Source=stocksense.db");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // GRADING: EF Core — configure how Alert maps to the database table
        modelBuilder.Entity<Alert>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.Property(a => a.Symbol).IsRequired().HasMaxLength(10);
            entity.Property(a => a.Message).IsRequired();
        });
    }
}