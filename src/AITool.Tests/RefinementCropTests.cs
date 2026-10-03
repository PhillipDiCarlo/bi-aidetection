using System.Drawing;
using AITool;
using Xunit;

namespace AITool.Tests;

/// <summary>
/// Covers 2.9 (crop before refinement): the crop rectangle math (padding + clamping), mapping a crop-relative
/// rectangle back to full-image coordinates, the per-type RefinementCrop default on ClsURLItem, and an
/// end-to-end-ish check of how a cropped refinement prediction gets remapped. The actual HTTP dispatch in
/// AITOOL.DetectObjects() is too entangled with static app state/queues to unit test directly, so these tests
/// exercise the extracted, static pieces it is built from.
/// </summary>
public class RefinementCropTests
{
    [Fact]
    public void GetRefinementCropRectangle_AddsPaddingAroundObject()
    {
        var objectRect = new Rectangle(100, 100, 100, 50); // 100x50 object

        Rectangle crop = AITOOL.GetRefinementCropRectangle(objectRect, 1000, 1000, 20);

        // 20% of 100 width = 20px, 20% of 50 height = 10px padding on each side
        Assert.Equal(Rectangle.FromLTRB(80, 90, 220, 160), crop);
    }

    [Fact]
    public void GetRefinementCropRectangle_ClampsToImageBounds()
    {
        // object sits right in the corner - padding would push the crop outside the image
        var objectRect = new Rectangle(0, 0, 50, 50);

        Rectangle crop = AITOOL.GetRefinementCropRectangle(objectRect, 200, 200, 50);

        Assert.True(crop.Left >= 0);
        Assert.True(crop.Top >= 0);
        Assert.True(crop.Right <= 200);
        Assert.True(crop.Bottom <= 200);
        Assert.Equal(0, crop.Left);
        Assert.Equal(0, crop.Top);
    }

    [Fact]
    public void GetRefinementCropRectangle_ClampsBottomRightCorner()
    {
        var objectRect = new Rectangle(150, 150, 50, 50); // right up against the 200x200 edge

        Rectangle crop = AITOOL.GetRefinementCropRectangle(objectRect, 200, 200, 50);

        Assert.Equal(200, crop.Right);
        Assert.Equal(200, crop.Bottom);
    }

    [Fact]
    public void GetRefinementCropRectangle_NeverReturnsZeroSizeCrop()
    {
        // a zero-size object rect (shouldn't normally happen) must still produce a usable crop
        var objectRect = new Rectangle(10, 10, 0, 0);

        Rectangle crop = AITOOL.GetRefinementCropRectangle(objectRect, 100, 100, 15);

        Assert.True(crop.Width > 0);
        Assert.True(crop.Height > 0);
    }

    [Fact]
    public void MapCropRelativeRectToFullImage_OffsetsByCropOrigin()
    {
        var cropArea = new Rectangle(80, 90, 140, 70); // crop's top-left is at (80,90) in the full image
        var cropRelativeRect = new Rectangle(10, 5, 30, 20); // a sub-object found at (10,5) inside the crop

        Rectangle full = AITOOL.MapCropRelativeRectToFullImage(cropArea, cropRelativeRect);

        Assert.Equal(new Rectangle(90, 95, 30, 20), full);
    }

    [Fact]
    public void MapCropRelativeRectToFullImage_ZeroOffsetIsIdentity()
    {
        var cropArea = new Rectangle(0, 0, 500, 500);
        var cropRelativeRect = new Rectangle(20, 20, 40, 40);

        Rectangle full = AITOOL.MapCropRelativeRectToFullImage(cropArea, cropRelativeRect);

        Assert.Equal(cropRelativeRect, full);
    }

    private static ClsPrediction MakePrediction(string label, Rectangle rect, int imageWidth, int imageHeight)
    {
        ClsPrediction pred = new ClsPrediction
        {
            Label = label,
            XMin = rect.Left,
            YMin = rect.Top,
            XMax = rect.Right,
            YMax = rect.Bottom,
            ImageWidth = imageWidth,
            ImageHeight = imageHeight,
        };
        // object initializers run after the constructor, so PercentOfImage/RectWidth/etc need to be (re)computed
        // from the values just set - exactly as the real AI-server-specific constructors do internally
        pred.UpdatePercent();
        return pred;
    }

    // ClsImageQueueItem.UpdateImageInfo() only reads Width/Height/image_path off the instance - it never touches
    // disk - so a fake, file-less instance with those set directly stands in for "the full image" in these tests.
    private static ClsImageQueueItem MakeFakeFullImage(int width, int height)
    {
        ClsImageQueueItem img = new ClsImageQueueItem(@"C:\fake\full-image.jpg", 0, true);
        img.Width = width;
        img.Height = height;
        return img;
    }

    [Fact]
    public void SetFullImageRectangle_MapsLocalizedSubObjectBackToFullImageCoordinates()
    {
        // a vehicle at (100,100)-(300,250) in the full image gets padded/cropped, and a plate is found
        // near the bottom-right of that crop
        var objectRect = new Rectangle(100, 100, 200, 150);
        Rectangle cropArea = AITOOL.GetRefinementCropRectangle(objectRect, 1000, 1000, 15);

        ClsPrediction plate = MakePrediction("Plate: ABC123", new Rectangle(150, 120, 40, 15), cropArea.Width, cropArea.Height);

        Rectangle fullRect = AITOOL.MapCropRelativeRectToFullImage(cropArea, plate.GetRectangle());
        plate.SetFullImageRectangle(fullRect, MakeFakeFullImage(1000, 1000));

        Assert.Equal(cropArea.X + 150, plate.XMin);
        Assert.Equal(cropArea.Y + 120, plate.YMin);
        Assert.Equal(1000, plate.ImageWidth);
        Assert.Equal(1000, plate.ImageHeight);
    }

    [Fact]
    public void SetFullImageRectangle_WholeCropPredictionMapsToOriginalObjectRectangle()
    {
        // simulates the vision-LLM "Scene" prediction: it covers almost the entire crop, so instead of offsetting
        // it we map it directly onto the object it was cropped for (this is what lets its Detail merge into that
        // object instead of becoming a stray "whole yard" description)
        var objectRect = new Rectangle(400, 300, 120, 260); // a person
        Rectangle cropArea = AITOOL.GetRefinementCropRectangle(objectRect, 1920, 1080, 15);

        ClsPrediction scene = MakePrediction("Scene", new Rectangle(5, 5, cropArea.Width - 10, cropArea.Height - 45), cropArea.Width, cropArea.Height);

        // this is the same signal the dispatch code uses to decide whether a crop prediction describes the whole
        // crop rather than a localized sub-object
        Assert.True(scene.PercentOfImage >= AITOOL.RefinementWholeCropCoveragePercent);

        scene.SetFullImageRectangle(objectRect, MakeFakeFullImage(1920, 1080));

        Assert.Equal(objectRect, scene.GetRectangle());
        Assert.Equal(1920, scene.ImageWidth);
        Assert.Equal(1080, scene.ImageHeight);
    }

    [Fact]
    public void IsRefinementMatch_MatchesByObjectTypeKeyword()
    {
        ClsPrediction person = MakePrediction("Person", new Rectangle(0, 0, 10, 10), 100, 100);
        person.Result = ResultType.Relevant;
        person.ObjType = ObjectType.Person;

        Assert.True(AITOOL.IsRefinementMatch(person, "Person, People, Face"));
        Assert.False(AITOOL.IsRefinementMatch(person, "vehicle"));
    }

    [Fact]
    public void IsRefinementMatch_WildcardMatchesAnyRelevantPrediction()
    {
        ClsPrediction vehicle = MakePrediction("Car", new Rectangle(0, 0, 10, 10), 100, 100);
        vehicle.Result = ResultType.Relevant;
        vehicle.ObjType = ObjectType.Vehicle;

        Assert.True(AITOOL.IsRefinementMatch(vehicle, "*"));
    }

    [Fact]
    public void IsRefinementMatch_IgnoresNonRelevantPredictions()
    {
        ClsPrediction person = MakePrediction("Person", new Rectangle(0, 0, 10, 10), 100, 100);
        person.Result = ResultType.DynamicMasked;
        person.ObjType = ObjectType.Person;

        Assert.False(AITOOL.IsRefinementMatch(person, "*"));
    }

    [Theory]
    [InlineData(URLTypeEnum.OpenAI_Vision, true)]
    [InlineData(URLTypeEnum.Anthropic_Vision, true)]
    [InlineData(URLTypeEnum.CodeProject_AI_Plate, true)]
    [InlineData(URLTypeEnum.DeepStack, false)]
    [InlineData(URLTypeEnum.DeepStack_Scene, false)]
    [InlineData(URLTypeEnum.CodeProject_AI_Faces, false)]
    [InlineData(URLTypeEnum.SightHound_Vehicle, false)]
    public void ClsURLItem_RefinementCropDefaultsPerType(URLTypeEnum type, bool expectedDefault)
    {
        ClsURLItem url = new ClsURLItem("", 1, type);

        Assert.Equal(expectedDefault, url.RefinementCrop);
        Assert.Equal(15, url.RefinementCropPaddingPercent);
    }

    [Fact]
    public void ClsURLItem_RefinementCropIsResolvedOnceForAnOlderSettingsFileMissingTheSetting()
    {
        // simulates a ClsURLItem deserialized from a settings.json written before 2.9 existed - RefinementCrop
        // is missing from the JSON, so it stays at its null ("never set") default until Update() resolves it
        ClsURLItem url = new ClsURLItem();
        url.url = "http://127.0.0.1:11434/v1/chat/completions";
        url.Type = URLTypeEnum.OpenAI_Vision;

        Assert.Null(url.RefinementCrop);

        url.Update(false);

        Assert.True(url.RefinementCrop);
    }

    [Fact]
    public void ClsURLItem_RefinementCropUserChoiceIsNeverOverwrittenOnceSet()
    {
        ClsURLItem url = new ClsURLItem("http://127.0.0.1:11434/v1/chat/completions", 1, URLTypeEnum.OpenAI_Vision);
        Assert.True(url.RefinementCrop); // per-type default applied

        url.RefinementCrop = false; // user opts back out
        url.Update(false); // runs many times over the life of the app (eg UpdateAIURLList())

        Assert.False(url.RefinementCrop);
    }
}
