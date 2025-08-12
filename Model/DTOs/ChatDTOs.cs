using System.ComponentModel.DataAnnotations;

namespace lionheart.Model.DTOs
{
    public class ChatRequest
    {
        [Required]
        public required string Message { get; set; }
        [Required]
        public required Guid ChatConversationId { get; init; }

    }

 
}
