using CustomAppTemplate.Models;
using CustomAppTemplate.Services;
using Microsoft.AspNetCore.Mvc;

namespace CustomAppTemplate.Controllers;

[ApiController]
[Route("api")]
public class ChatController(
    IOpenAIService openAIService,
    MessageTransformer transformer,
    ILogger<ChatController> logger) : ControllerBase
{
    [HttpPost("chat")]
    public async Task<IActionResult> Chat([FromBody] ChatRequest request)
    {
        if (request.Messages is null || request.Messages.Count == 0)
            return BadRequest(new { error = "Messages are required and must be an array" });

        var invalidMessage = request.Messages.FirstOrDefault(msg =>
            string.IsNullOrEmpty(msg.Role) ||
            msg.Content is null ||
            (msg.Content.IsString
                ? string.IsNullOrEmpty(msg.Content.StringValue)
                : msg.Content.ArrayValue is null || msg.Content.ArrayValue.Count == 0));

        if (invalidMessage is not null)
            return BadRequest(new { error = "Each message must have a role and content (string or non-empty array)" });

        var originalCount = request.Messages.Count;
        request.Messages = transformer.Transform(request.Messages);

        logger.LogInformation(
            "Messages transformed: originalCount={Original}, transformedCount={Transformed}",
            originalCount, request.Messages.Count);

        if (request.Stream)
        {
            Response.Headers.ContentType = "text/event-stream";
            Response.Headers.CacheControl = "no-cache";
            Response.Headers.Connection = "keep-alive";

            var stream = await openAIService.GenerateStreamedResponseAsync(request);
            await SseFormatter.FormatAndSendAsync(stream, Response, HttpContext.RequestAborted);
            return new EmptyResult();
        }

        var response = await openAIService.GenerateResponseAsync(request);
        return Ok(response);
    }
}
