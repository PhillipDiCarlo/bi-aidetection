using System.Collections.Generic;
using AITool.AIProviders;
using Xunit;
using static AITool.AIProviders.YoloPostProcessor;

namespace AITool.Tests;

public class YoloPostProcessorTests
{
    [Fact]
    public void Letterbox_RoundTripsABoxThroughAWideImage()
    {
        //1920x1080 source letterboxed into a 640x640 model input: scale is limited by width, so padding lands top/bottom
        LetterboxTransform t = ComputeLetterbox(origWidth: 1920, origHeight: 1080, targetSize: 640);

        Assert.Equal(640.0 / 1920.0, t.Scale, 6);
        Assert.Equal(0, t.PadX, 3);
        Assert.True(t.PadY > 0);

        //a box at (100,100)-(300,400) in the original image, projected into letterboxed space using the same transform
        double lx1 = 100 * t.Scale + t.PadX;
        double ly1 = 100 * t.Scale + t.PadY;
        double lx2 = 300 * t.Scale + t.PadX;
        double ly2 = 400 * t.Scale + t.PadY;

        var (ox1, oy1, ox2, oy2) = MapBoxToOriginal((float)lx1, (float)ly1, (float)lx2, (float)ly2, t, 1920, 1080);

        Assert.Equal(100, ox1, 1);
        Assert.Equal(100, oy1, 1);
        Assert.Equal(300, ox2, 1);
        Assert.Equal(400, oy2, 1);
    }

    [Fact]
    public void Letterbox_RoundTripsABoxThroughATallImage()
    {
        //portrait source: scale is limited by height, so padding lands left/right this time
        LetterboxTransform t = ComputeLetterbox(origWidth: 1080, origHeight: 1920, targetSize: 640);

        Assert.Equal(640.0 / 1920.0, t.Scale, 6);
        Assert.Equal(0, t.PadY, 3);
        Assert.True(t.PadX > 0);

        double lx1 = 200 * t.Scale + t.PadX;
        double ly1 = 50 * t.Scale + t.PadY;

        var (ox1, oy1, _, _) = MapBoxToOriginal((float)lx1, (float)ly1, (float)lx1, (float)ly1, t, 1080, 1920);

        Assert.Equal(200, ox1, 1);
        Assert.Equal(50, oy1, 1);
    }

    [Fact]
    public void MapBoxToOriginal_ClampsToImageBounds()
    {
        LetterboxTransform t = ComputeLetterbox(origWidth: 640, origHeight: 640, targetSize: 640);

        //a box that extends past the letterboxed canvas should clamp, not go negative or past the image edge
        var (ox1, oy1, ox2, oy2) = MapBoxToOriginal(-50, -50, 10000, 10000, t, 640, 640);

        Assert.Equal(0, ox1);
        Assert.Equal(0, oy1);
        Assert.Equal(640, ox2);
        Assert.Equal(640, oy2);
    }

    [Fact]
    public void Nms_SuppressesOverlappingSameClassBoxes()
    {
        var boxes = new List<YoloBox>
        {
            new YoloBox { X1 = 0, Y1 = 0, X2 = 100, Y2 = 100, Score = 0.9f, ClassId = 0 },
            new YoloBox { X1 = 5, Y1 = 5, X2 = 105, Y2 = 105, Score = 0.8f, ClassId = 0 }, //heavily overlaps the box above, same class
        };

        List<YoloBox> kept = Nms(boxes, 0.45f);

        Assert.Single(kept);
        Assert.Equal(0.9f, kept[0].Score);
    }

    [Fact]
    public void Nms_KeepsOverlappingBoxesOfDifferentClasses()
    {
        var boxes = new List<YoloBox>
        {
            new YoloBox { X1 = 0, Y1 = 0, X2 = 100, Y2 = 100, Score = 0.9f, ClassId = 0 }, //person
            new YoloBox { X1 = 0, Y1 = 0, X2 = 100, Y2 = 100, Score = 0.85f, ClassId = 2 }, //car, exact same box but a different class
        };

        List<YoloBox> kept = Nms(boxes, 0.45f);

        Assert.Equal(2, kept.Count);
    }

    [Fact]
    public void IoU_IdenticalBoxesIsOne()
    {
        var a = new YoloBox { X1 = 0, Y1 = 0, X2 = 10, Y2 = 10 };
        Assert.Equal(1f, IoU(a, a), 5);
    }

    [Fact]
    public void IoU_DisjointBoxesIsZero()
    {
        var a = new YoloBox { X1 = 0, Y1 = 0, X2 = 10, Y2 = 10 };
        var b = new YoloBox { X1 = 100, Y1 = 100, X2 = 110, Y2 = 110 };
        Assert.Equal(0f, IoU(a, b));
    }

    [Fact]
    public void DecodeOutput_ChannelsFirstLayout_YieldsExpectedBoxesAndFiltersLowConfidence()
    {
        //synthetic YOLOv8/v11-style output: [1, 4+nc, N] = [1, 84, 3] (80 COCO classes, 3 candidate boxes), channels-first
        const int numClasses = 80;
        const int numBoxes = 3;
        const int channels = 4 + numClasses;
        float[] data = new float[channels * numBoxes];

        void SetChannel(int channelIdx, int boxIdx, float value) => data[channelIdx * numBoxes + boxIdx] = value;

        //box 0: person (class 0), centered at (100,100), 50x50, confidence 0.9 - should survive
        SetChannel(0, 0, 100); SetChannel(1, 0, 100); SetChannel(2, 0, 50); SetChannel(3, 0, 50);
        SetChannel(4 + 0, 0, 0.9f);

        //box 1: car (class 2), centered at (300,300), 60x80, confidence 0.8 - should survive
        SetChannel(0, 1, 300); SetChannel(1, 1, 300); SetChannel(2, 1, 60); SetChannel(3, 1, 80);
        SetChannel(4 + 2, 1, 0.8f);

        //box 2: some class 5, confidence 0.1 - below the 0.25 threshold, should be dropped
        SetChannel(0, 2, 50); SetChannel(1, 2, 50); SetChannel(2, 2, 20); SetChannel(3, 2, 20);
        SetChannel(4 + 5, 2, 0.1f);

        List<YoloBox> boxes = DecodeOutput(data, new[] { 1, channels, numBoxes }, numClasses, confThreshold: 0.25f);

        Assert.Equal(2, boxes.Count);

        YoloBox person = boxes.Find(b => b.ClassId == 0);
        Assert.Equal(0.9f, person.Score, 3);
        Assert.Equal(75, person.X1, 1);
        Assert.Equal(75, person.Y1, 1);
        Assert.Equal(125, person.X2, 1);
        Assert.Equal(125, person.Y2, 1);

        YoloBox car = boxes.Find(b => b.ClassId == 2);
        Assert.Equal(0.8f, car.Score, 3);
        Assert.Equal(270, car.X1, 1);
        Assert.Equal(260, car.Y1, 1);
        Assert.Equal(330, car.X2, 1);
        Assert.Equal(340, car.Y2, 1);

        Assert.DoesNotContain(boxes, b => b.ClassId == 5);
    }

    [Fact]
    public void DecodeOutput_AnchorMajorLayout_MatchesChannelsFirstResult()
    {
        //same single box as above but laid out as [1, N, 4+nc] (anchor-major, no transpose needed)
        const int numClasses = 4;
        const int channels = 4 + numClasses;
        float[] data = { 100, 100, 50, 50, 0, 0.75f, 0, 0 }; //cx,cy,w,h, then 4 class scores - class 1 wins

        List<YoloBox> boxes = DecodeOutput(data, new[] { 1, 1, channels }, numClasses, confThreshold: 0.25f);

        Assert.Single(boxes);
        Assert.Equal(1, boxes[0].ClassId);
        Assert.Equal(0.75f, boxes[0].Score, 3);
        Assert.Equal(75, boxes[0].X1, 1);
        Assert.Equal(125, boxes[0].X2, 1);
    }

    [Fact]
    public void ResolveInputSize_FallsBackToDefaultForDynamicDimension()
    {
        Assert.Equal(640, ResolveInputSize(-1));
        Assert.Equal(640, ResolveInputSize(0));
        Assert.Equal(512, ResolveInputSize(512));
    }
}
