using CustomAppTemplate.Models;

namespace CustomAppTemplate.Services;

public class MessageTransformer
{
    private readonly List<Func<List<ChatMessage>, List<ChatMessage>>> _transforms = [];

    public MessageTransformer AddTransform(Func<List<ChatMessage>, List<ChatMessage>> fn)
    {
        _transforms.Add(fn);
        return this;
    }

    public List<ChatMessage> Transform(List<ChatMessage> messages)
    {
        return _transforms.Aggregate(
            new List<ChatMessage>(messages),
            (msgs, fn) => fn(msgs));
    }

    public void ClearTransforms() => _transforms.Clear();
}
