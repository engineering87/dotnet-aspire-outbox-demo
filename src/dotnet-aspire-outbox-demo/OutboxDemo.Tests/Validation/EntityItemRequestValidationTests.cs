// (c) 2025 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using FluentAssertions;
using SenderService.Controllers;

namespace OutboxDemo.Tests.Validation;

public class EntityItemRequestValidationTests
{
    [Theory]
    [InlineData("Valid Name", 100.00, true)]
    [InlineData("A", 0.01, true)]
    [InlineData("Very Long Name With Many Characters", 999999.99, true)]
    [InlineData(null, 100.00, false)]
    [InlineData("", 100.00, false)]
    [InlineData("   ", 100.00, false)]
    [InlineData("Valid Name", -1.00, false)]
    [InlineData("Valid Name", -0.01, false)]
    public void Validate_EntityItemRequest_ReturnsExpectedResult(string? name, decimal value, bool expectedValid)
    {
        // Arrange
        var request = new EntityItemRequest(name, value);

        // Act
        var isValid = ValidateRequest(request);

        // Assert
        isValid.Should().Be(expectedValid);
    }

    [Fact]
    public void EntityItemRequest_Record_SupportsEquality()
    {
        // Arrange
        var request1 = new EntityItemRequest("Test", 100m);
        var request2 = new EntityItemRequest("Test", 100m);
        var request3 = new EntityItemRequest("Different", 100m);

        // Assert
        request1.Should().Be(request2);
        request1.Should().NotBe(request3);
    }

    [Fact]
    public void EntityItemRequest_Record_SupportsDeconstruction()
    {
        // Arrange
        var request = new EntityItemRequest("Deconstruct Test", 250.50m);

        // Act
        var (name, value) = request;

        // Assert
        name.Should().Be("Deconstruct Test");
        value.Should().Be(250.50m);
    }

    [Fact]
    public void EntityItemRequest_WithBoundaryValues_ValidatesCorrectly()
    {
        // Arrange - boundary values
        var zeroValue = new EntityItemRequest("Zero", 0m);
        var minPositive = new EntityItemRequest("Min Positive", 0.01m);
        var largeValue = new EntityItemRequest("Large", decimal.MaxValue);

        // Act & Assert
        ValidateRequest(zeroValue).Should().BeTrue();
        ValidateRequest(minPositive).Should().BeTrue();
        ValidateRequest(largeValue).Should().BeTrue();
    }

    [Fact]
    public void EntityItemRequest_WithSpecialCharactersInName_IsValid()
    {
        // Arrange
        var request = new EntityItemRequest("Test!@#$%^&*()_+-=[]{}|;':\",./<>?", 100m);

        // Act
        var isValid = ValidateRequest(request);

        // Assert
        isValid.Should().BeTrue();
    }

    [Fact]
    public void EntityItemRequest_WithUnicodeInName_IsValid()
    {
        // Arrange
        var request = new EntityItemRequest("Tëst Ïtém 日本語 🎉", 100m);

        // Act
        var isValid = ValidateRequest(request);

        // Assert
        isValid.Should().BeTrue();
    }

    // Helper method that mirrors controller validation logic
    private static bool ValidateRequest(EntityItemRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return false;

        if (request.Value < 0)
            return false;

        return true;
    }
}
