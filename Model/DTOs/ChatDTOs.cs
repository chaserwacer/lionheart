using System.ComponentModel.DataAnnotations;

namespace lionheart.Model.DTOs
{
    public class ChatRequest
    {
        [Required]
        public required string Message { get; set; }
        [Required]
        [Range(1, 5)]
        public int CreativityLevel { get; set; }

    }
    
    public class ChatResponse
    {
        public required string Response { get; set; }
    }
}
