using Microsoft.AspNetCore.Mvc;
using NetURLScanner.Services;

namespace NetURLScanner.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    public class ChatController : ControllerBase
    {
        private readonly GeminiChatService _chatService;

        public ChatController(GeminiChatService chatService)
        {
            _chatService = chatService;
        }

        public class ChatRequest
        {
            public string Message { get; set; } = string.Empty;
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] ChatRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Message))
            {
                return BadRequest(new { message = "Tin nhắn không được để trống." });
            }

            var response = await _chatService.GenerateResponseAsync(request.Message);
            return Ok(new { message = response });
        }
    }
}
