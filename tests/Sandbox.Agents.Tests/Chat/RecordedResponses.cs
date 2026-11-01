namespace Sandbox.Agents.Tests.Chat;

/// <summary>Respuestas reales de la API de DeepSeek grabadas en el explore del 2026-11-01 (ids recortados).</summary>
internal static class RecordedResponses
{
    public const string HelloWithoutThinking = """
        {"id":"4170529b","object":"chat.completion","created":1793533081,"model":"deepseek-flash",
         "choices":[{"index":0,"message":{"role":"assistant","content":"hola"},"logprobs":null,"finish_reason":"stop"}],
         "usage":{"prompt_tokens":13,"completion_tokens":2,"total_tokens":15,"prompt_tokens_details":{"cached_tokens":0},
                  "prompt_cache_hit_tokens":0,"prompt_cache_miss_tokens":13},
         "system_fingerprint":"bc19ba97cbe016479903222bb1578db6"}
        """;

    public const string HelloWithThinking = """
        {"id":"8d7ee130","object":"chat.completion","created":1793533069,"model":"deepseek-flash",
         "choices":[{"index":0,"message":{"role":"assistant","content":"hola",
                     "reasoning_content":"We need answer only word hola. Need obey."},"logprobs":null,"finish_reason":"stop"}],
         "usage":{"prompt_tokens":39,"completion_tokens":14,"total_tokens":53,"prompt_tokens_details":{"cached_tokens":0},
                  "completion_tokens_details":{"reasoning_tokens":11},"prompt_cache_hit_tokens":0,"prompt_cache_miss_tokens":39},
         "system_fingerprint":"bc19ba97cbe016479903222bb1578db6"}
        """;

    public const string InvalidApiKey = """
        {"error":{"message":"Authentication Fails, Your api key: ****pike is invalid (request_id: 5c2fc46f-32a2-4ee4-8185-21d613e3bc73)",
                  "type":"authentication_error","param":null,"code":"invalid_request_error"}}
        """;

    public const string RateLimited = """
        {"error":{"message":"Rate limit reached","type":"rate_limit_error","param":null,"code":"rate_limit_exceeded"}}
        """;

    /// <summary>Respuesta sin razonamiento cuyo texto no debe aparecer nunca en los registros.</summary>
    public static string WithContent(string content, string model = "deepseek-flash") => $$$"""
        {"id":"0001","object":"chat.completion","created":1793533081,"model":"{{{model}}}",
         "choices":[{"index":0,"message":{"role":"assistant","content":"{{{content}}}"},"logprobs":null,"finish_reason":"stop"}],
         "usage":{"prompt_tokens":13,"completion_tokens":2,"total_tokens":15}}
        """;
}
