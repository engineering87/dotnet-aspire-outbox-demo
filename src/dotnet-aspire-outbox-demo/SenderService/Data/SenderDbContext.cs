// (c) 2025 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using Microsoft.EntityFrameworkCore;
using SenderService.Data.Entities;

namespace SenderService.Data
{
    public class SenderDbContext : DbContext
    {
        public SenderDbContext(DbContextOptions<SenderDbContext> options)
            : base(options) { }

        public DbSet<EntityItem> EntityItems => Set<EntityItem>();
        public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<EntityItem>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.Name).IsRequired().HasMaxLength(200);
            });

            modelBuilder.Entity<OutboxMessage>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.Type).IsRequired().HasMaxLength(100);
                e.Property(x => x.Payload).IsRequired();
            });
        }
    }
}