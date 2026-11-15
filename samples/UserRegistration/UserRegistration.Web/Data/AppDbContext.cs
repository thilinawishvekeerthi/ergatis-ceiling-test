using Microsoft.EntityFrameworkCore;
using UserRegistration.Web.Models;

namespace UserRegistration.Web.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("users");
            e.Property(u => u.FullName).HasMaxLength(100);
            e.Property(u => u.Email).HasMaxLength(254);
            // Emails are compared case-insensitively, so they are stored lower-case and unique.
            e.HasIndex(u => u.Email).IsUnique();
        });
    }
}
