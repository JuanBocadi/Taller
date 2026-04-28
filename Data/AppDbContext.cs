using Microsoft.EntityFrameworkCore;
using Taller.Models;

namespace Taller.Data;

public class AppDbContext : DbContext
{
    public DbSet<Vehiculo> Vehiculos => Set<Vehiculo>();
    public DbSet<Reparacion> Reparaciones => Set<Reparacion>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Reparacion>()
            .HasOne(r => r.Vehiculo)
            .WithMany(v => v.Reparaciones)
            .HasForeignKey(r => r.Patente);
    }
}