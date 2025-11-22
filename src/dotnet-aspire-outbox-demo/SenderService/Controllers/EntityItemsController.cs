// (c) 2025 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SenderService.Data;
using SenderService.Data.Entities;

namespace SenderService.Controllers
{
    // SenderService.Controllers/EntityItemsController.cs
    [ApiController]
    [Route("api/[controller]")]
    public class EntityItemsController : ControllerBase
    {
        private readonly SenderDbContext _db;

        public EntityItemsController(SenderDbContext db) => _db = db;

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] EntityItemRequest request)
        {
            var entity = new EntityItem
            {
                Id = Guid.NewGuid(),
                Name = request.Name,
                Value = request.Value,
                CreatedAt = DateTime.UtcNow
            };

            var evt = new
            {
                entity.Id,
                entity.Name,
                entity.Value,
                entity.CreatedAt
            };

            var outboxMessage = new OutboxMessage
            {
                Id = Guid.NewGuid(),
                AggregateId = entity.Id.ToString(),
                Type = "EntityItemCreated",
                Payload = System.Text.Json.JsonSerializer.Serialize(evt),
                CreatedAt = DateTime.UtcNow
            };

            await using var tx = await _db.Database.BeginTransactionAsync();
            _db.EntityItems.Add(entity);
            _db.OutboxMessages.Add(outboxMessage);
            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            return CreatedAtAction(nameof(GetById), new { id = entity.Id }, entity);
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<EntityItem>>> GetAll() =>
            Ok(await _db.EntityItems
                .OrderByDescending(e => e.CreatedAt)
                .ToListAsync());

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<EntityItem>> GetById(Guid id)
        {
            var entity = await _db.EntityItems.FindAsync(id);
            return entity is null ? NotFound() : Ok(entity);
        }
    }

    public record EntityItemRequest(string Name, decimal Value);
}
