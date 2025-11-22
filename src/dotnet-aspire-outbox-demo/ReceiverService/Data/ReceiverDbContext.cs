// (c) 2025 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using Microsoft.EntityFrameworkCore;
using ReceiverService.Data.Entities;

namespace ReceiverService.Data
{
    public class ReceiverDbContext : DbContext
    {
        public ReceiverDbContext(DbContextOptions<ReceiverDbContext> options)
            : base(options) { }

        public DbSet<EntityItemProjection> EntityItems => Set<EntityItemProjection>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<EntityItemProjection>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.Name).IsRequired().HasMaxLength(200);
            });
        }
    }
}
