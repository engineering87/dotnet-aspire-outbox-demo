// (c) 2025 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReceiverService.Data;
using ReceiverService.Data.Entities;

namespace ReceiverService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EntityItemsProjectionController : ControllerBase
    {
        private readonly ReceiverDbContext _db;

        public EntityItemsProjectionController(ReceiverDbContext db)
        {
            _db = db;
        }

        // GET api/entityitemsprojection
        [HttpGet]
        public async Task<ActionResult<IEnumerable<EntityItemProjection>>> GetAll()
        {
            var items = await _db.EntityItems
                .OrderByDescending(e => e.ReceivedAt)
                .ToListAsync();

            return Ok(items);
        }

        // GET api/entityitemsprojection/{id}
        [HttpGet("{id:guid}")]
        public async Task<ActionResult<EntityItemProjection>> GetById(Guid id)
        {
            var entity = await _db.EntityItems.FindAsync(id);
            return entity is null ? NotFound() : Ok(entity);
        }
    }
}
