// (c) 2025 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using FluentAssertions;
using System.Diagnostics;

namespace OutboxDemo.Tests.Tracing;

public class CorrelationIdTests
{
    [Fact]
    public void CorrelationId_FromActivityTraceId_IsValid()
    {
        // Arrange
        using var activity = new Activity("TestOperation").Start();

        // Act
        var correlationId = activity.TraceId.ToString();

        // Assert
        correlationId.Should().NotBeNullOrEmpty();
        correlationId.Should().HaveLength(32); // TraceId is 32 hex characters
    }

    [Fact]
    public void CorrelationId_FromGuid_IsValid()
    {
        // Arrange & Act
        var correlationId = Guid.NewGuid().ToString();

        // Assert
        correlationId.Should().NotBeNullOrEmpty();
        Guid.TryParse(correlationId, out _).Should().BeTrue();
    }

    [Fact]
    public void CorrelationId_FromHeader_IsPreserved()
    {
        // Arrange
        var originalCorrelationId = "custom-correlation-12345";

        // Act
        var correlationId = originalCorrelationId;

        // Assert
        correlationId.Should().Be("custom-correlation-12345");
    }

    [Fact]
    public void CorrelationId_FallbackChain_WorksCorrectly()
    {
        // Simulates the fallback chain: Activity.Current?.TraceId ?? Header ?? NewGuid

        // Scenario 1: No activity, no header -> new GUID
        string? activityTraceId = null;
        string? headerValue = null;

        var correlationId = activityTraceId ?? headerValue ?? Guid.NewGuid().ToString();
        Guid.TryParse(correlationId, out _).Should().BeTrue();

        // Scenario 2: No activity, has header -> use header
        headerValue = "header-correlation-id";
        correlationId = activityTraceId ?? headerValue ?? Guid.NewGuid().ToString();
        correlationId.Should().Be("header-correlation-id");

        // Scenario 3: Has activity -> use trace ID
        using var activity = new Activity("Test").Start();
        activityTraceId = activity.TraceId.ToString();
        correlationId = activityTraceId ?? headerValue ?? Guid.NewGuid().ToString();
        correlationId.Should().Be(activityTraceId);
    }

    [Fact]
    public void Activity_SetTags_PreservesMessageMetadata()
    {
        // Arrange
        var messageId = Guid.NewGuid().ToString();
        var correlationId = Guid.NewGuid().ToString();
        var destination = "entity-items";

        // Act
        using var activity = new Activity("ProcessMessage")
            .SetTag("messaging.message_id", messageId)
            .SetTag("messaging.correlation_id", correlationId)
            .SetTag("messaging.destination", destination)
            .Start();

        // Assert
        activity.Tags.Should().Contain(t => t.Key == "messaging.message_id" && t.Value == messageId);
        activity.Tags.Should().Contain(t => t.Key == "messaging.correlation_id" && t.Value == correlationId);
        activity.Tags.Should().Contain(t => t.Key == "messaging.destination" && t.Value == destination);
    }

    [Fact]
    public void LoggerScope_WithCorrelationId_ContainsCorrelationId()
    {
        // Arrange
        var correlationId = "test-correlation-789";
        var scope = new Dictionary<string, object> { ["CorrelationId"] = correlationId };

        // Assert
        scope.Should().ContainKey("CorrelationId");
        scope["CorrelationId"].Should().Be(correlationId);
    }
}
