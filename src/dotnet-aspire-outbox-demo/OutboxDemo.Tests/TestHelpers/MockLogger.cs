// (c) 2025 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using Microsoft.Extensions.Logging;
using Moq;

namespace OutboxDemo.Tests.TestHelpers;

public static class MockLogger
{
    public static Mock<ILogger<T>> Create<T>()
    {
        return new Mock<ILogger<T>>();
    }

    public static void VerifyLogWasCalled<T>(this Mock<ILogger<T>> logger, LogLevel level, string messageContains)
    {
        logger.Verify(
            x => x.Log(
                level,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains(messageContains)),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    public static void VerifyLogWasNotCalled<T>(this Mock<ILogger<T>> logger, LogLevel level)
    {
        logger.Verify(
            x => x.Log(
                level,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }
}
