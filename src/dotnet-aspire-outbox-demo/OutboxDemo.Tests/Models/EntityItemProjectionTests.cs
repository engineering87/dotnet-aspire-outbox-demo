// (c) 2025 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using FluentAssertions;
using ReceiverService.Data.Entities;

namespace OutboxDemo.Tests.Models;

public class EntityItemProjectionTests
{
    [Fact]
    public void EntityItemProjection_NewInstance_HasDefaultValues()
    {
        // Arrange & Act
        var projection = new EntityItemProjection();

        // Assert
        projection.Id.Should().Be(Guid.Empty);
        projection.Name.Should().BeNull();
        projection.Value.Should().Be(0m);
        projection.CreatedAt.Should().Be(default);
        projection.ReceivedAt.Should().Be(default);
    }

    [Fact]
    public void EntityItemProjection_SetProperties_PropertiesAreSet()
    {
        // Arrange
        var id = Guid.NewGuid();
        var name = "Test Projection";
        var value = 250.75m;
        var createdAt = DateTime.UtcNow.AddMinutes(-5);
        var receivedAt = DateTime.UtcNow;

        // Act
        var projection = new EntityItemProjection
        {
            Id = id,
            Name = name,
            Value = value,
            CreatedAt = createdAt,
            ReceivedAt = receivedAt
        };

        // Assert
        projection.Id.Should().Be(id);
        projection.Name.Should().Be(name);
        projection.Value.Should().Be(value);
        projection.CreatedAt.Should().Be(createdAt);
        projection.ReceivedAt.Should().Be(receivedAt);
    }

    [Fact]
    public void EntityItemProjection_ReceivedAtIsAfterCreatedAt()
    {
        // Arrange
        var createdAt = DateTime.UtcNow.AddSeconds(-10);
        var receivedAt = DateTime.UtcNow;

        // Act
        var projection = new EntityItemProjection
        {
            Id = Guid.NewGuid(),
            Name = "Timing Test",
            Value = 100m,
            CreatedAt = createdAt,
            ReceivedAt = receivedAt
        };

        // Assert
        projection.ReceivedAt.Should().BeAfter(projection.CreatedAt);
    }

    [Fact]
    public void EntityItemProjection_CanUpdateNameAndValue()
    {
        // Arrange
        var projection = new EntityItemProjection
        {
            Id = Guid.NewGuid(),
            Name = "Original",
            Value = 100m,
            CreatedAt = DateTime.UtcNow,
            ReceivedAt = DateTime.UtcNow
        };

        // Act
        projection.Name = "Updated";
        projection.Value = 200m;

        // Assert
        projection.Name.Should().Be("Updated");
        projection.Value.Should().Be(200m);
    }
}
