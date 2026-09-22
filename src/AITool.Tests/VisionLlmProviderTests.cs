using System;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AITool.AIProviders;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace AITool.Tests;

public class VisionLlmProviderRequestTests
{
    [Fact]
    public void BuildOpenAiRequest_ContainsModelPromptAndImageDataUri()
    {
        JObject body = VisionLlmProvider.BuildOpenAiRequest("llava", "Describe this image", "ZmFrZQ==", 512);

        Assert.Equal("llava", (string)body["model"]);
        Assert.Equal(512, (int)body["max_tokens"]);

        JArray content = (JArray)body["messages"][0]["content"];
        Assert.Equal("text", (string)content[0]["type"]);
        Assert.Equal("Describe this image", (string)content[0]["text"]);
        Assert.Equal("image_url", (string)content[1]["type"]);
        Assert.Equal("data:image/jpeg;base64,ZmFrZQ==", (string)content[1]["image_url"]["url"]);
    }

    [Fact]
    public void BuildAnthropicRequest_ContainsModelPromptAndBase64ImageSource()
    {
        JObject body = VisionLlmProvider.BuildAnthropicRequest("claude-opus-5", "Describe this image", "ZmFrZQ==", 512);

        Assert.Equal("claude-opus-5", (string)body["model"]);
        Assert.Equal(512, (int)body["max_tokens"]);

        JArray content = (JArray)body["messages"][0]["content"];
        Assert.Equal("image", (string)content[0]["type"]);
        Assert.Equal("base64", (string)content[0]["source"]["type"]);
        Assert.Equal("image/jpeg", (string)content[0]["source"]["media_type"]);
        Assert.Equal("ZmFrZQ==", (string)content[0]["source"]["data"]);
        Assert.Equal("text", (string)content[1]["type"]);
        Assert.Equal("Describe this image", (string)content[1]["text"]);
    }

    [Fact]
    public void ApplyOpenAiHeaders_SetsBearerAuthorizationWhenKeyProvided()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "http://127.0.0.1:11434/v1/chat/completions");
        VisionLlmProvider.ApplyOpenAiHeaders(request, "sk-test-key");

        Assert.NotNull(request.Headers.Authorization);
        Assert.Equal("Bearer", request.Headers.Authorization.Scheme);
        Assert.Equal("sk-test-key", request.Headers.Authorization.Parameter);
    }

    [Fact]
    public void ApplyOpenAiHeaders_OmitsAuthorizationWhenKeyEmpty()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "http://127.0.0.1:11434/v1/chat/completions");
        VisionLlmProvider.ApplyOpenAiHeaders(request, "");

        Assert.Null(request.Headers.Authorization);
    }

    [Fact]
    public void ApplyAnthropicHeaders_SetsApiKeyAndVersion()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
        VisionLlmProvider.ApplyAnthropicHeaders(request, "sk-ant-test");

        Assert.Equal("sk-ant-test", request.Headers.GetValues("x-api-key").Single());
        Assert.Equal("2023-06-01", request.Headers.GetValues("anthropic-version").Single());
    }

    [Fact]
    public void ExtractOpenAiText_ReadsChoicesMessageContent()
    {
        string json = "{\"choices\":[{\"message\":{\"content\":\"hello world\"}}]}";
        Assert.Equal("hello world", VisionLlmProvider.ExtractOpenAiText(json));
    }

    [Fact]
    public void ExtractAnthropicText_ReadsFirstTextContentBlock()
    {
        string json = "{\"content\":[{\"type\":\"text\",\"text\":\"hello world\"}]}";
        Assert.Equal("hello world", VisionLlmProvider.ExtractAnthropicText(json));
    }
}

public class VisionLlmProviderParseReplyTests
{
    [Fact]
    public void ParseModelReply_PlainJson_ParsesDescriptionAndObjects()
    {
        string reply = "{\"description\":\"A person walks by\",\"objects\":[{\"label\":\"person\",\"detail\":\"carrying a package\",\"confidence\":0.9,\"box\":[0.1,0.2,0.3,0.4]}]}";

        var result = VisionLlmProvider.ParseModelReply(reply);

        Assert.Equal("A person walks by", result.Description);
        Assert.Single(result.Objects);
        Assert.Equal("person", result.Objects[0].Label);
        Assert.Equal("carrying a package", result.Objects[0].Detail);
        Assert.Equal(0.9, result.Objects[0].Confidence, 5);
        Assert.Equal(new[] { 0.1, 0.2, 0.3, 0.4 }, result.Objects[0].Box);
    }

    [Fact]
    public void ParseModelReply_WrappedInMarkdownCodeFence_StripsFenceBeforeParsing()
    {
        string reply = "```json\n{\"description\":\"A dog in the yard\",\"objects\":[]}\n```";

        var result = VisionLlmProvider.ParseModelReply(reply);

        Assert.Equal("A dog in the yard", result.Description);
        Assert.Empty(result.Objects);
    }

    [Fact]
    public void ParseModelReply_ObjectsWithoutBoxes_LeavesBoxNull()
    {
        string reply = "{\"description\":\"Something happened\",\"objects\":[{\"label\":\"cat\",\"confidence\":0.5}]}";

        var result = VisionLlmProvider.ParseModelReply(reply);

        Assert.Single(result.Objects);
        Assert.Null(result.Objects[0].Box);
    }

    [Fact]
    public void ParseModelReply_NoObjectsField_ReturnsEmptyObjectList()
    {
        string reply = "{\"description\":\"Nothing notable\"}";

        var result = VisionLlmProvider.ParseModelReply(reply);

        Assert.Equal("Nothing notable", result.Description);
        Assert.Empty(result.Objects);
    }

    [Fact]
    public void ParseModelReply_GarbageText_TreatsWholeReplyAsDescription()
    {
        string reply = "Sorry, I can't help with that request.";

        var result = VisionLlmProvider.ParseModelReply(reply);

        Assert.Equal(reply, result.Description);
        Assert.Empty(result.Objects);
    }

    [Fact]
    public void ParseModelReply_TextWithEmbeddedBracesButInvalidJson_FallsBackToWholeText()
    {
        string reply = "The image shows {a person} near a car, roughly {2 vehicles} total.";

        var result = VisionLlmProvider.ParseModelReply(reply);

        Assert.Equal(reply, result.Description);
        Assert.Empty(result.Objects);
    }

    [Fact]
    public void ParseModelReply_EmptyString_ReturnsEmptyDescription()
    {
        var result = VisionLlmProvider.ParseModelReply("");

        Assert.Equal("", result.Description);
        Assert.Empty(result.Objects);
    }
}

public class VisionLlmProviderCoordinateMappingTests
{
    [Fact]
    public void ToDeepstackDetection_NormalizedBoxMapsToImagePixels()
    {
        var obj = new VisionLlmProvider.ParsedVisionObject
        {
            Label = "person",
            Detail = "walking",
            Confidence = 0.75,
            Box = new[] { 0.25, 0.5, 0.75, 1.0 }
        };

        ClsDeepstackDetection det = VisionLlmProvider.ToDeepstackDetection(obj, 1000, 500);

        Assert.Equal("person", det.label);
        Assert.Equal("walking", det.Detail);
        Assert.Equal(0.75, det.confidence, 5);
        Assert.Equal(250, det.x_min, 5);
        Assert.Equal(250, det.y_min, 5);
        Assert.Equal(750, det.x_max, 5);
        Assert.Equal(500, det.y_max, 5);
    }

    [Fact]
    public void ToDeepstackDetection_NoBox_DefaultsToFullImage()
    {
        var obj = new VisionLlmProvider.ParsedVisionObject { Label = "dog", Confidence = 0.6, Box = null };

        ClsDeepstackDetection det = VisionLlmProvider.ToDeepstackDetection(obj, 640, 480);

        Assert.Equal(0, det.x_min);
        Assert.Equal(0, det.y_min);
        Assert.Equal(640, det.x_max);
        Assert.Equal(480, det.y_max);
    }
}

/// <summary>
/// Best-effort live smoke test against a local Ollama server. Skips cleanly (never fails the run) when
/// Ollama isn't reachable at 127.0.0.1:11434 or has no vision-capable model pulled.
/// </summary>
public class VisionLlmProviderLiveOllamaTests
{
    private readonly ITestOutputHelper _output;

    public VisionLlmProviderLiveOllamaTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task LiveRequest_AgainstLocalOllama_IfAvailable()
    {
        using HttpClient http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };

        string modelName;
        try
        {
            string tagsJson = await http.GetStringAsync("http://127.0.0.1:11434/api/tags");
            JObject tags = JObject.Parse(tagsJson);
            JArray models = tags["models"] as JArray;

            if (models == null || models.Count == 0)
            {
                _output.WriteLine("Skipped: Ollama is running but has no models pulled.");
                return;
            }

            string[] visionHints = { "llava", "vision", "bakllava", "moondream", "minicpm" };
            JToken visionModel = models.FirstOrDefault(m => visionHints.Any(h => ((string)m["name"] ?? "").Contains(h, StringComparison.OrdinalIgnoreCase)));

            modelName = (string)(visionModel ?? models[0])["name"];
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Skipped: Ollama not reachable at 127.0.0.1:11434 ({ex.Message}).");
            return;
        }

        try
        {
            string testImagePath = GetTestImagePath();
            if (!System.IO.File.Exists(testImagePath))
            {
                _output.WriteLine($"Skipped: could not find TestImage.jpg at '{testImagePath}'.");
                return;
            }

            string base64Jpeg = Convert.ToBase64String(await System.IO.File.ReadAllBytesAsync(testImagePath));

            JObject body = VisionLlmProvider.BuildOpenAiRequest(modelName, VisionLlmProvider.DefaultPrompt, base64Jpeg, 512);

            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "http://127.0.0.1:11434/v1/chat/completions");
            request.Content = new StringContent(body.ToString(), Encoding.UTF8, "application/json");

            using HttpClient longHttp = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
            using HttpResponseMessage response = await longHttp.SendAsync(request, CancellationToken.None);
            string responseJson = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _output.WriteLine($"Skipped: Ollama returned {response.StatusCode} for model '{modelName}': {responseJson}");
                return;
            }

            string rawText = VisionLlmProvider.ExtractOpenAiText(responseJson);
            var parsed = VisionLlmProvider.ParseModelReply(rawText);

            _output.WriteLine($"Model: {modelName}");
            _output.WriteLine($"Raw reply: {rawText}");
            _output.WriteLine($"Parsed description: {parsed.Description}");
            _output.WriteLine($"Parsed objects: {parsed.Objects.Count}");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Skipped: live Ollama request failed ({ex.Message}).");
        }
    }

    //resolves relative to this source file rather than the test output directory, since TestImage.jpg
    //is only copied to UI.csproj's own output, not this test project's
    private static string GetTestImagePath([CallerFilePath] string sourceFile = "")
    {
        string dir = System.IO.Path.GetDirectoryName(sourceFile);
        return System.IO.Path.GetFullPath(System.IO.Path.Combine(dir, "..", "UI", "TestImage.jpg"));
    }
}
