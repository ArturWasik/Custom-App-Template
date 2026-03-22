namespace CustomAppTemplate.Models;

public class OpenAIOptions
{
    public string ApiKey { get; set; } = "";
}

public class AuthOptions
{
    public string ApiKey { get; set; } = "";
    public bool Enabled => !string.IsNullOrEmpty(ApiKey);
}

public class ServerOptions
{
    public int Port { get; set; } = 3000;
}
