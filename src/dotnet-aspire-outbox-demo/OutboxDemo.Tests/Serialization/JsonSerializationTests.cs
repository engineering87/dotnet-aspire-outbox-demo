// (c) 2025 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using FluentAssertions;
using System.Text.Json;

namespace OutboxDemo.Tests.Serialization;

public class JsonSerializationTests
{
    [Fact]
    public void SerializeEntityEvent_ProducesValidJson()
    {
        // Arrange
        var evt = new
        {
            Id = Guid.NewGuid(),
            Name = "Test Entity",
            Value = 123.45m,
            CreatedAt = DateTime.UtcNow
        };

        // Act
        var json = JsonSerializer.Serialize(evt);

        // Assert
        json.Should().Contain("\"Id\":");
        json.Should().Contain("\"Name\":\"Test Entity\"");
        json.Should().Contain("\"Value\":123.45");
        json.Should().Contain("\"CreatedAt\":");
    }

    [Fact]
    public void DeserializeEntityEvent_WithCaseInsensitive_Works()
    {
        // Arrange
        var json = """
        {
            "id": "550e8400-e29b-41d4-a716-446655440000",
            "name": "Test Item",
            "value": 99.99,
            "createdAt": "2025-01-01T12:00:00Z"
        }
        """;

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        // Act
        var result = JsonSerializer.Deserialize<TestEntityDto>(json, options);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(Guid.Parse("550e8400-e29b-41d4-a716-446655440000"));
        result.Name.Should().Be("Test Item");
        result.Value.Should().Be(99.99m);
    }

    [Fact]
    public void DeserializeEntityEvent_WithPascalCase_Works()
    {
        // Arrange
        var json = """
        {
            "Id": "550e8400-e29b-41d4-a716-446655440000",
            "Name": "Pascal Case Item",
            "Value": 50.00,
            "CreatedAt": "2025-01-01T12:00:00Z"
        }
        """;

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        // Act
        var result = JsonSerializer.Deserialize<TestEntityDto>(json, options);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("Pascal Case Item");
    }

    [Fact]
    public void DeserializeEntityEvent_WithMissingOptionalFields_Works()
    {
        // Arrange
        var json = """
        {
            "Id": "550e8400-e29b-41d4-a716-446655440000",
            "Name": "Minimal Item",
            "Value": 10.00,
            "CreatedAt": "2025-01-01T12:00:00Z"
        }
        """;

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        // Act
        var result = JsonSerializer.Deserialize<TestEntityDto>(json, options);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public void DeserializeEntityEvent_WithInvalidJson_ThrowsException()
    {
        // Arrange
        var invalidJson = "{ invalid json }";

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        // Act
        var action = () => JsonSerializer.Deserialize<TestEntityDto>(invalidJson, options);

        // Assert
        action.Should().Throw<JsonException>();
    }

    [Fact]
    public void DeserializeEntityEvent_WithNullPayload_ReturnsNull()
    {
        // Arrange
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        // Act
        var result = JsonSerializer.Deserialize<TestEntityDto>("null", options);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void SerializeAndDeserialize_RoundTrip_PreservesData()
    {
        // Arrange
        var original = new TestEntityDto(
            Guid.NewGuid(),
            "Round Trip Test",
            999.99m,
            DateTime.UtcNow
        );

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        // Act
        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<TestEntityDto>(json, options);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.Id.Should().Be(original.Id);
        deserialized.Name.Should().Be(original.Name);
        deserialized.Value.Should().Be(original.Value);
    }

    // Test DTO matching the structure used in the consumer
    private sealed record TestEntityDto(
        Guid Id,
        string Name,
        decimal Value,
        DateTime CreatedAt);
}
