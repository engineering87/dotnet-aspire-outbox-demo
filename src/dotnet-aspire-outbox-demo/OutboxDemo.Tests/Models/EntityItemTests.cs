// (c) 2025 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using FluentAssertions;
using SenderService.Data;

namespace OutboxDemo.Tests.Models;

public class EntityItemTests
{
    [Fact]
    public void EntityItem_NewInstance_HasDefaultValues()
    {
        // Arrange & Act
        var entity = new EntityItem();

        // Assert
        entity.Id.Should().Be(Guid.Empty);
        entity.Name.Should().BeNull();
        entity.Value.Should().Be(0m);
        entity.CreatedAt.Should().Be(default);
    }

    [Fact]
    public void EntityItem_SetProperties_PropertiesAreSet()
    {
        // Arrange
        var id = Guid.NewGuid();
        var name = "Test Item";
        var value = 123.45m;
        var createdAt = DateTime.UtcNow;

        // Act
        var entity = new EntityItem
        {
            Id = id,
            Name = name,
            Value = value,
            CreatedAt = createdAt
        };

        // Assert
        entity.Id.Should().Be(id);
        entity.Name.Should().Be(name);
        entity.Value.Should().Be(value);
        entity.CreatedAt.Should().Be(createdAt);
    }

    [Fact]
    public void EntityItem_WithDecimalPrecision_MaintainsPrecision()
    {
        // Arrange
        var preciseValue = 12345.6789m;

        // Act
        var entity = new EntityItem
        {
            Id = Guid.NewGuid(),
            Name = "Precision Test",
            Value = preciseValue,
            CreatedAt = DateTime.UtcNow
        };

        // Assert
        entity.Value.Should().Be(12345.6789m);
    }

    [Fact]
    public void EntityItem_WithNegativeValue_AcceptsNegative()
    {
        // Note: Validation is done at the controller level, model accepts any value
        // Arrange & Act
        var entity = new EntityItem
        {
            Id = Guid.NewGuid(),
            Name = "Negative Test",
            Value = -100.00m,
            CreatedAt = DateTime.UtcNow
        };

        // Assert
        entity.Value.Should().Be(-100.00m);
    }

    [Fact]
    public void EntityItem_WithZeroValue_AcceptsZero()
    {
        // Arrange & Act
        var entity = new EntityItem
        {
            Id = Guid.NewGuid(),
            Name = "Zero Test",
            Value = 0m,
            CreatedAt = DateTime.UtcNow
        };

        // Assert
        entity.Value.Should().Be(0m);
    }
}
