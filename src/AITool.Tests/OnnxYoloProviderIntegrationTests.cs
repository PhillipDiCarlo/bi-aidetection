using System;
using System.IO;
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
/// which only exercises the pure math. There's no official Ultralytics ONNX download, so this points
/// AppSettings.Settings.OnnxDefaultModel{Url,Path} at a community-hosted export of the stock (80-class
/// COCO) yolov8n model and a temp dir, and lets OnnxYoloProvider.DetectAsync download it itself on first
/// use - the same auto-download path a fresh install exercises, rather than duplicating the download
/// logic here. If that download fails (offline machine, host unreachable, CI with no network), the tests
/// no-op instead of failing - there's no xunit "Skip" mechanism wired into this project.
/// </summary>
public class OnnxYoloProviderIntegrationTests
{
    private readonly ITestOutputHelper output;

    public OnnxYoloProviderIntegrationTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    private static readonly string ModelPath = Path.Combine(Path.GetTempPath(), "AITool.Tests.OnnxYolo", "yolov8n.onnx");

    //Points the default-model settings at the temp-dir path used by these tests, so OnnxYoloProvider's
    //auto-download kicks in for a model path that equals AppSettings.Settings.OnnxDefaultModelPath.
    private static void ConfigureDefaultModelSettings()
    {
        AppSettings.Settings.OnnxDefaultModelUrl = "https://huggingface.co/kshitijjjjjjjjjjjjjjjj/yolov8n-coco-onnx/resolve/main/yolov8n.onnx";
        AppSettings.Settings.OnnxDefaultModelPath = ModelPath;
    }

    //Auto-download failure (no network) isn't a code bug - treat it as skipped, same as the old EnsureModelAsync() did.
    private static bool IsDownloadFailure(ClsAIServerResponse result)
    {
        return !result.Success && result.Error != null && result.Error.Contains("auto-download the default model but failed", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DetectAsync_FindsAPersonInTestImage()
    {
        ConfigureDefaultModelSettings();

        ClsURLItem url = new ClsURLItem(ModelPath, 1, URLTypeEnum.Local_ONNX);
        Camera cam = new Camera("default"); //avoids ClsRelevantObjectManager's Reset()/Update() recursion, which needs a populated camera list to terminate for any other name
        using ClsImageQueueItem img = new ClsImageQueueItem(GetTestImagePath("TestImage.jpg"), 0);

        ClsAIServerResponse result = await new OnnxYoloProvider().DetectAsync(img, url, cam, CancellationToken.None);

        if (IsDownloadFailure(result))
            return;

        LogPredictions(result);
        Assert.True(result.Success, result.Error);
        Assert.Contains(result.Predictions, p => p.Label.Equals("Person", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task DetectAsync_FindsAVehicleInTestVehicleImage()
    {
        ConfigureDefaultModelSettings();

        ClsURLItem url = new ClsURLItem(ModelPath, 1, URLTypeEnum.Local_ONNX);
        Camera cam = new Camera("default");
        using ClsImageQueueItem img = new ClsImageQueueItem(GetTestImagePath("TestVehicleImage.jpg"), 0);

        ClsAIServerResponse result = await new OnnxYoloProvider().DetectAsync(img, url, cam, CancellationToken.None);

        if (IsDownloadFailure(result))
            return;

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
