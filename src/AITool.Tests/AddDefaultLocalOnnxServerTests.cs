using System.Collections.Generic;
using Xunit;

namespace AITool.Tests;

/// <summary>
/// Covers AITOOL.AddDefaultLocalOnnxServerIfEmpty() - the "fresh install gets a working local detector,
/// an existing configuration is left alone" logic factored out of AITOOL.UpdateAIURLList() for testing.
/// </summary>
public class AddDefaultLocalOnnxServerTests
{
    [Fact]
    public void EmptyList_AddsDefaultLocalOnnxServer()
    {
        List<ClsURLItem> AIURLList = new List<ClsURLItem>();

        bool added = AITOOL.AddDefaultLocalOnnxServerIfEmpty(AIURLList, @"C:\fake\_Settings\models\yolov8n.onnx");

        Assert.True(added);
        ClsURLItem url = Assert.Single(AIURLList);
        Assert.Equal(URLTypeEnum.Local_ONNX, url.Type);
        Assert.Equal(@"C:\fake\_Settings\models\yolov8n.onnx", url.url);
        Assert.Equal("Local YOLO (built-in)", url.Name);
        Assert.True(url.Enabled);
    }

    [Fact]
    public void NonEmptyList_IsLeftUntouched()
    {
        ClsURLItem existing = new ClsURLItem("http://127.0.0.1:80/v1/vision/detection", 1, URLTypeEnum.DeepStack);
        List<ClsURLItem> AIURLList = new List<ClsURLItem> { existing };

        bool added = AITOOL.AddDefaultLocalOnnxServerIfEmpty(AIURLList, @"C:\fake\_Settings\models\yolov8n.onnx");

        Assert.False(added);
        ClsURLItem url = Assert.Single(AIURLList);
        Assert.Same(existing, url);
    }

    [Fact]
    public void EmptyDefaultModelPath_DoesNotAddServer()
    {
        List<ClsURLItem> AIURLList = new List<ClsURLItem>();

        bool added = AITOOL.AddDefaultLocalOnnxServerIfEmpty(AIURLList, "");

        Assert.False(added);
        Assert.Empty(AIURLList);
    }
}
