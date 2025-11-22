// (c) 2025 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
namespace ReceiverService.Data.Entities
{
    public class EntityItemProjection
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
        public decimal Value { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ReceivedAt { get; set; }
    }
}
