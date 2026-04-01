// (c) 2025 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using OutboxDemo.Tests.TestHelpers;
using SenderService.Controllers;
using SenderService.Data;

namespace OutboxDemo.Tests.Controllers;

public class EntityItemsControllerTests : IDisposable
{
    private readonly SenderDbContext _dbContext;
    private readonly EntityItemsController _controller;

    public EntityItemsControllerTests()
    {
        _dbContext = InMemoryDbContextFactory.CreateSenderDbContext();
        _controller = new EntityItemsController(_dbContext);

        // Setup HttpContext for correlation ID tests
        var httpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public async Task Create_WithValidRequest_ReturnsCreatedResult()
    {
        // Arrange
        var request = new EntityItemRequest("Test Item", 100.50m);

        // Act
        var result = await _controller.Create(request);

        // Assert
        var createdResult = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.ActionName.Should().Be(nameof(EntityItemsController.GetById));

        var entity = createdResult.Value.Should().BeOfType<EntityItem>().Subject;
        entity.Name.Should().Be("Test Item");
        entity.Value.Should().Be(100.50m);
        entity.Id.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Create_WithValidRequest_SavesEntityToDatabase()
    {
        // Arrange
        var request = new EntityItemRequest("Saved Item", 50.00m);

        // Act
        await _controller.Create(request);

        // Assert
        var savedEntity = await _dbContext.EntityItems.FindAsync(
            _dbContext.EntityItems.First().Id);
        savedEntity.Should().NotBeNull();
        savedEntity!.Name.Should().Be("Saved Item");
    }

    [Fact]
    public async Task Create_WithValidRequest_CreatesOutboxMessage()
    {
        // Arrange
        var request = new EntityItemRequest("Outbox Test", 75.00m);

        // Act
        await _controller.Create(request);

        // Assert
        var outboxMessage = _dbContext.OutboxMessages.First();
        outboxMessage.Should().NotBeNull();
        outboxMessage.Type.Should().Be("EntityItemCreated");
        outboxMessage.CorrelationId.Should().NotBeNullOrEmpty();
        outboxMessage.Payload.Should().Contain("Outbox Test");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_WithEmptyName_ReturnsBadRequest(string? name)
    {
        // Arrange
        var request = new EntityItemRequest(name, 100.00m);

        // Act
        var result = await _controller.Create(request);

        // Assert
        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.Value.Should().Be("Name is required.");
    }

    [Fact]
    public async Task Create_WithNegativeValue_ReturnsBadRequest()
    {
        // Arrange
        var request = new EntityItemRequest("Valid Name", -10.00m);

        // Act
        var result = await _controller.Create(request);

        // Assert
        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.Value.Should().Be("Value cannot be negative.");
    }

    [Fact]
    public async Task Create_WithZeroValue_Succeeds()
    {
        // Arrange
        var request = new EntityItemRequest("Zero Value Item", 0m);

        // Act
        var result = await _controller.Create(request);

        // Assert
        result.Should().BeOfType<CreatedAtActionResult>();
    }

    [Fact]
    public async Task Create_WithCorrelationIdHeader_UsesProvidedCorrelationId()
    {
        // Arrange
        var expectedCorrelationId = "test-correlation-123";
        _controller.HttpContext.Request.Headers["X-Correlation-ID"] = expectedCorrelationId;
        var request = new EntityItemRequest("Correlation Test", 100.00m);

        // Act
        await _controller.Create(request);

        // Assert
        var outboxMessage = _dbContext.OutboxMessages.First();
        outboxMessage.CorrelationId.Should().Be(expectedCorrelationId);
    }

    [Fact]
    public async Task GetAll_WhenNoEntities_ReturnsEmptyList()
    {
        // Act
        var result = await _controller.GetAll();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var entities = okResult.Value.Should().BeAssignableTo<IEnumerable<EntityItem>>().Subject;
        entities.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAll_WithEntities_ReturnsAllOrderedByCreatedAtDescending()
    {
        // Arrange
        var entity1 = new EntityItem
        {
            Id = Guid.NewGuid(),
            Name = "First",
            Value = 10m,
            CreatedAt = DateTime.UtcNow.AddMinutes(-10)
        };
        var entity2 = new EntityItem
        {
            Id = Guid.NewGuid(),
            Name = "Second",
            Value = 20m,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.EntityItems.AddRange(entity1, entity2);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _controller.GetAll();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var entities = okResult.Value.Should().BeAssignableTo<IEnumerable<EntityItem>>().Subject.ToList();
        entities.Should().HaveCount(2);
        entities[0].Name.Should().Be("Second"); // Most recent first
        entities[1].Name.Should().Be("First");
    }

    [Fact]
    public async Task GetById_WithExistingId_ReturnsEntity()
    {
        // Arrange
        var entity = new EntityItem
        {
            Id = Guid.NewGuid(),
            Name = "Test Entity",
            Value = 100m,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.EntityItems.Add(entity);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _controller.GetById(entity.Id);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedEntity = okResult.Value.Should().BeOfType<EntityItem>().Subject;
        returnedEntity.Id.Should().Be(entity.Id);
        returnedEntity.Name.Should().Be("Test Entity");
    }

    [Fact]
    public async Task GetById_WithNonExistingId_ReturnsNotFound()
    {
        // Arrange
        var nonExistingId = Guid.NewGuid();

        // Act
        var result = await _controller.GetById(nonExistingId);

        // Assert
        result.Result.Should().BeOfType<NotFoundResult>();
    }
}
