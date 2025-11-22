// (c) 2025 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
namespace SenderService.Data.Entities
{
    public class OutboxMessage
    {
        public Guid Id { get; set; }
        public string AggregateId { get; set; } = default!;
        public string Type { get; set; } = default!;
        public string Payload { get; set; } = default!;
        public DateTime CreatedAt { get; set; }
        public DateTime? ProcessedAt { get; set; }
        public int RetryCount { get; set; }
    }
}
