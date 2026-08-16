using Microsoft.EntityFrameworkCore;
using TodayLOL.Models;

namespace TodayLOL.Data
{
    public class RecordDbContext : DbContext
    {
        public DbSet<Record> Records { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            options.UseSqlite($"Data Source={App.DbPath}");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Record>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.FilePath).IsRequired();
                entity.Property(e => e.Description);
                entity.Property(e => e.CreateTime).IsRequired();
                entity.Property(e => e.WatermarkPosition).IsRequired();
            });
        }
    }
}