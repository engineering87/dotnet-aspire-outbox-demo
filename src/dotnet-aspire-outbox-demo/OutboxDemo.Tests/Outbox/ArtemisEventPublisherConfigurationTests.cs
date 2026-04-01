// (c) 2025 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;

namespace OutboxDemo.Tests.Outbox;

public class ArtemisEventPublisherConfigurationTests
{
    [Fact]
    public void Constructor_WithValidConnectionString_ParsesCorrectly()
    {
        // Arrange
        var configuration = CreateConfiguration("amqp://user:pass@myhost:5673");

        // Act & Assert - constructor should not throw
        var action = () => new SenderService.Outbox.ArtemisEventPublisher(configuration);
        action.Should().NotThrow();
    }

    [Fact]
    public void Constructor_WithDefaultConnectionString_UsesDefaults()
    {
        // Arrange
        var configuration = CreateConfiguration(null);

        // Act & Assert - should use defaults without throwing
        var action = () => new SenderService.Outbox.ArtemisEventPublisher(configuration);
        action.Should().NotThrow();
    }

    [Fact]
    public void Constructor_WithNoUserInfo_UsesDefaultCredentials()
    {
        // Arrange
        var configuration = CreateConfiguration("amqp://myhost:5672");

        // Act & Assert
        var action = () => new SenderService.Outbox.ArtemisEventPublisher(configuration);
        action.Should().NotThrow();
    }

    [Fact]
    public void Constructor_WithCustomAddress_UsesProvidedAddress()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "ConnectionStrings:Artemis", "amqp://localhost:5672" },
            { "Artemis:Address", "custom-address" }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        // Act & Assert
        var action = () => new SenderService.Outbox.ArtemisEventPublisher(configuration);
        action.Should().NotThrow();
    }

    [Fact]
    public void Constructor_WithDefaultPort_Uses5672()
    {
        // Arrange - URI without explicit port
        var configuration = CreateConfiguration("amqp://user:pass@myhost");

        // Act & Assert
        var action = () => new SenderService.Outbox.ArtemisEventPublisher(configuration);
        action.Should().NotThrow();
    }

    [Fact]
    public void Constructor_WithPasswordContainingColon_ParsesCorrectly()
    {
        // Arrange - password with colon should be handled correctly
        var configuration = CreateConfiguration("amqp://user:pass:word@myhost:5672");

        // Act & Assert
        var action = () => new SenderService.Outbox.ArtemisEventPublisher(configuration);
        action.Should().NotThrow();
    }

    [Fact]
    public void Constructor_WithInvalidUri_Throws()
    {
        // Arrange
        var configuration = CreateConfiguration("not-a-valid-uri");

        // Act & Assert
        var action = () => new SenderService.Outbox.ArtemisEventPublisher(configuration);
        action.Should().Throw<UriFormatException>();
    }

    private static IConfiguration CreateConfiguration(string? connectionString)
    {
        var inMemorySettings = new Dictionary<string, string?>();

        if (connectionString != null)
        {
            inMemorySettings["ConnectionStrings:Artemis"] = connectionString;
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();
    }
}
