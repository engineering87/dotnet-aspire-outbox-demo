// (c) 2025 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OutboxDemo.Tests.TestHelpers;
using ReceiverService.Data;
using ReceiverService.Data.Entities;

namespace OutboxDemo.Tests.Data;

public class ReceiverDbContextTests : IDisposable
{
    private readonly ReceiverDbContext _dbContext;

    public ReceiverDbContextTests()
    {
        _dbContext = InMemoryDbContextFactory.CreateReceiverDbContext();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public async Task CanAddEntityItemProjection()
    {
        // Arrange
        var projection = new EntityItemProjection
        {
            Id = Guid.NewGuid(),
            Name = "Test Item",
            Value = 100.50m,
            CreatedAt = DateTime.UtcNow,
            ReceivedAt = DateTime.UtcNow
        };

        // Act
        _dbContext.EntityItems.Add(projection);
        await _dbContext.SaveChangesAsync();

        // Assert
        var saved = await _dbContext.EntityItems.FindAsync(projection.Id);
        saved.Should().NotBeNull();
        saved!.Name.Should().Be("Test Item");
        saved.Value.Should().Be(100.50m);
    }

    [Fact]
    public async Task CanUpdateEntityItemProjection()
    {
        // Arrange
        var projection = new EntityItemProjection
        {
            Id = Guid.NewGuid(),
            Name = "Original Name",
            Value = 50.00m,
            CreatedAt = DateTime.UtcNow,
            ReceivedAt = DateTime.UtcNow
        };

        _dbContext.EntityItems.Add(projection);
        await _dbContext.SaveChangesAsync();

        // Act
        projection.Name = "Updated Name";
        projection.Value = 75.00m;
        await _dbContext.SaveChangesAsync();

        // Assert
        var updated = await _dbContext.EntityItems.FindAsync(projection.Id);
        updated!.Name.Should().Be("Updated Name");
        updated.Value.Should().Be(75.00m);
    }

    [Fact]
    public async Task CanFindEntityItemProjectionById()
    {
        // Arrange
        var id = Guid.NewGuid();
        var projection = new EntityItemProjection
        {
            Id = id,
            Name = "Findable Item",
            Value = 200.00m,
            CreatedAt = DateTime.UtcNow,
            ReceivedAt = DateTime.UtcNow
        };

        _dbContext.EntityItems.Add(projection);
        await _dbContext.SaveChangesAsync();

        // Act
        var found = await _dbContext.EntityItems.FirstOrDefaultAsync(e => e.Id == id);

        // Assert
        found.Should().NotBeNull();
        found!.Name.Should().Be("Findable Item");
    }

    [Fact]
    public async Task FindNonExistentId_ReturnsNull()
    {
        // Act
        var found = await _dbContext.EntityItems.FindAsync(Guid.NewGuid());

        // Assert
        found.Should().BeNull();
    }
}
