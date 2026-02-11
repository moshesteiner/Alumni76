// Data/ApplicationDbContext.cs
using Alumni76.Models; // Your Models namespace
using Microsoft.EntityFrameworkCore;

namespace Alumni76.Data // Your DbContext namespace
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }
        // Existing DbSets
        public DbSet<User> Users { get; set; }
        public DbSet<Participate> Participates { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);          
        }
    }
}