using System;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using AITool.AIProviders;
using Xunit;
using Xunit.Abstractions;
using static AITool.AITOOL;

namespace AITool.Tests;

/// <summary>
/// Runs the real ONNX Runtime pipeline end to end against a real model - unlike YoloPostProcessorTests,
/// which only exercises the pure math. There's no official Ultralytics ONNX download, so this pulls a
/// community-hosted export of the stock (80-class COCO) yolov8n model into the temp dir on first run.
/// If that download fails (offline machine, host unreachable, CI with no network), the tests no-op
/// instead of failing - there's no xunit "Skip" mechanism wired into this project.
/// </summary>
public class OnnxYoloProviderIntegrationTests
{
    private readonly ITestOutputHelper output;

    public OnnxYoloProviderIntegrationTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    private const string ModelUrl = "https://huggingface.co/kshitijjjjjjjjjjjjjjjj/yolov8n-coco-onnx/resolve/main/yolov8n.onnx";
    private static readonly string ModelPath = Path.Combine(Path.GetTempPath(), "AITool.Tests.OnnxYolo", "yolov8n.onnx");

    private static async Task<bool> EnsureModelAsync()
    {
        if (File.Exists(ModelPath) && new FileInfo(ModelPath).Length > 1_000_000)
            return true;

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ModelPath)!);
            using HttpClient client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            byte[] bytes = await client.GetByteArrayAsync(ModelUrl);
            await File.WriteAllBytesAsync(ModelPath, bytes);
            return true;
        }
        catch
        {
            return false;
        }
    }

    [Fact]
    public async Task DetectAsync_FindsAPersonInTestImage()
    {
        if (!await EnsureModelAsync())
            return; //no model available - treat as skipped rather than failed

        ClsURLItem url = new ClsURLItem(ModelPath, 1, URLTypeEnum.Local_ONNX);
        Camera cam = new Camera("default"); //avoids ClsRelevantObjectManager's Reset()/Update() recursion, which needs a populated camera list to terminate for any other name
        using ClsImageQueueItem img = new ClsImageQueueItem(GetTestImagePath("TestImage.jpg"), 0);

        ClsAIServerResponse result = await new OnnxYoloProvider().DetectAsync(img, url, cam, CancellationToken.None);

        LogPredictions(result);
        Assert.True(result.Success, result.Error);
        Assert.Contains(result.Predictions, p => p.Label.Equals("Person", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task DetectAsync_FindsAVehicleInTestVehicleImage()
    {
        if (!await EnsureModelAsync())
            return;

        ClsURLItem url = new ClsURLItem(ModelPath, 1, URLTypeEnum.Local_ONNX);
        Camera cam = new Camera("default");
        using ClsImageQueueItem img = new ClsImageQueueItem(GetTestImagePath("TestVehicleImage.jpg"), 0);

        ClsAIServerResponse result = await new OnnxYoloProvider().DetectAsync(img, url, cam, CancellationToken.None);

        LogPredictions(result);
        Assert.True(result.Success, result.Error);

        string[] vehicleLabels = { "car", "truck", "bus", "motorcycle" };
        Assert.Contains(result.Predictions, p => Array.Exists(vehicleLabels, v => v.Equals(p.Label, StringComparison.OrdinalIgnoreCase)));
    }

    private static string GetTestImagePath(string fileName, [CallerFilePath] string testFilePath = "")
    {
        string testDir = Path.GetDirectoryName(testFilePath)!;
        return Path.GetFullPath(Path.Combine(testDir, "..", "UI", fileName));
    }

    private void LogPredictions(ClsAIServerResponse result)
    {
        foreach (ClsPrediction p in result.Predictions)
            output.WriteLine($"{p.Label} conf={p.Confidence:F1} box=({p.XMin:F0},{p.YMin:F0})-({p.XMax:F0},{p.YMax:F0})");
    }
}
