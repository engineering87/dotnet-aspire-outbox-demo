// (c) 2025 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OutboxDemo.Tests.TestHelpers;
using ReceiverService.Controllers;
using ReceiverService.Data;
using ReceiverService.Data.Entities;

namespace OutboxDemo.Tests.Controllers;

public class EntityItemsProjectionControllerTests : IDisposable
{
    private readonly ReceiverDbContext _dbContext;
    private readonly EntityItemsProjectionController _controller;

    public EntityItemsProjectionControllerTests()
    {
        _dbContext = InMemoryDbContextFactory.CreateReceiverDbContext();
        _controller = new EntityItemsProjectionController(_dbContext);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public async Task GetAll_WhenNoProjections_ReturnsEmptyList()
    {
        // Act
        var result = await _controller.GetAll();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var projections = okResult.Value.Should().BeAssignableTo<IEnumerable<EntityItemProjection>>().Subject;
        projections.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAll_WithProjections_ReturnsAllOrderedByReceivedAtDescending()
    {
        // Arrange
        var projection1 = new EntityItemProjection
        {
            Id = Guid.NewGuid(),
            Name = "First",
            Value = 10m,
            CreatedAt = DateTime.UtcNow.AddMinutes(-10),
            ReceivedAt = DateTime.UtcNow.AddMinutes(-5)
        };
        var projection2 = new EntityItemProjection
        {
            Id = Guid.NewGuid(),
            Name = "Second",
            Value = 20m,
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            ReceivedAt = DateTime.UtcNow
        };

        _dbContext.EntityItems.AddRange(projection1, projection2);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _controller.GetAll();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var projections = okResult.Value.Should().BeAssignableTo<IEnumerable<EntityItemProjection>>().Subject.ToList();
        projections.Should().HaveCount(2);
        projections[0].Name.Should().Be("Second"); // Most recently received first
        projections[1].Name.Should().Be("First");
    }

    [Fact]
    public async Task GetById_WithExistingId_ReturnsProjection()
    {
        // Arrange
        var projection = new EntityItemProjection
        {
            Id = Guid.NewGuid(),
            Name = "Test Projection",
            Value = 100m,
            CreatedAt = DateTime.UtcNow,
            ReceivedAt = DateTime.UtcNow
        };
        _dbContext.EntityItems.Add(projection);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _controller.GetById(projection.Id);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedProjection = okResult.Value.Should().BeOfType<EntityItemProjection>().Subject;
        returnedProjection.Id.Should().Be(projection.Id);
        returnedProjection.Name.Should().Be("Test Projection");
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

    [Fact]
    public async Task GetAll_WithManyProjections_ReturnsAll()
    {
        // Arrange
        var projections = Enumerable.Range(1, 10).Select(i => new EntityItemProjection
        {
            Id = Guid.NewGuid(),
            Name = $"Item {i}",
            Value = i * 10m,
            CreatedAt = DateTime.UtcNow,
            ReceivedAt = DateTime.UtcNow.AddMinutes(-i)
        }).ToList();

        _dbContext.EntityItems.AddRange(projections);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _controller.GetAll();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedProjections = okResult.Value.Should().BeAssignableTo<IEnumerable<EntityItemProjection>>().Subject;
        returnedProjections.Should().HaveCount(10);
    }
}
