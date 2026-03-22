using CustomAppTemplate.Models;

namespace CustomAppTemplate.Services;

public interface IOpenAIService
{
    Task<ChatResponse> GenerateResponseAsync(ChatRequest request);
    Task<Stream> GenerateStreamedResponseAsync(ChatRequest request);
}
