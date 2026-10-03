using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using AITool;
using Xunit;

namespace AITool.Tests;

public class FrigateEventParsingTests
{
    private const string NewEventJson = @"{
        ""type"": ""new"",
        ""before"": { ""id"": ""1696000000.123456-abc123"", ""camera"": ""front_door"", ""label"": ""person"", ""has_snapshot"": false },
        ""after"": { ""id"": ""1696000000.123456-abc123"", ""camera"": ""front_door"", ""label"": ""person"", ""has_snapshot"": true, ""top_score"": 0.91 }
    }";

    [Fact]
    public void TryParseEvent_ParsesTypeAndBeforeAfter()
    {
        bool ok = FrigateSource.TryParseEvent(NewEventJson, out FrigateEventMessage msg);

        Assert.True(ok);
        Assert.Equal("new", msg.Type);
        Assert.Equal("front_door", msg.After.Camera);
        Assert.Equal("person", msg.After.Label);
        Assert.True(msg.After.HasSnapshot);
        Assert.False(msg.Before.HasSnapshot);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{\"type\": \"new\"")]
    public void TryParseEvent_ReturnsFalseForInvalidJson(string json)
    {
        bool ok = FrigateSource.TryParseEvent(json, out FrigateEventMessage msg);

        Assert.False(ok);
    }

    [Fact]
    public void ShouldFetchSnapshot_TrueForNewEventWithSnapshot()
    {
        FrigateSource.TryParseEvent(NewEventJson, out FrigateEventMessage msg);

        Assert.True(FrigateSource.ShouldFetchSnapshot(msg));
    }

    [Fact]
    public void ShouldFetchSnapshot_FalseWhenNoSnapshotYet()
    {
        string json = @"{""type"":""new"",""after"":{""id"":""1"",""camera"":""cam"",""label"":""person"",""has_snapshot"":false}}";
        FrigateSource.TryParseEvent(json, out FrigateEventMessage msg);

        Assert.False(FrigateSource.ShouldFetchSnapshot(msg));
    }

    [Fact]
    public void ShouldFetchSnapshot_FalseForEndEvent()
    {
        string json = @"{""type"":""end"",""before"":{""id"":""1"",""camera"":""cam"",""label"":""person"",""has_snapshot"":true},""after"":{""id"":""1"",""camera"":""cam"",""label"":""person"",""has_snapshot"":true}}";
        FrigateSource.TryParseEvent(json, out FrigateEventMessage msg);

        //already would have been fetched on "new" or the update that first saw has_snapshot=true - "end" should not re-fetch
        Assert.False(FrigateSource.ShouldFetchSnapshot(msg));
    }

    [Fact]
    public void ShouldFetchSnapshot_TrueOnFirstUpdateThatGainsSnapshot()
    {
        //covers the case where "new" was published before Frigate had a snapshot ready yet
        string json = @"{""type"":""update"",""before"":{""id"":""1"",""camera"":""cam"",""label"":""person"",""has_snapshot"":false},""after"":{""id"":""1"",""camera"":""cam"",""label"":""person"",""has_snapshot"":true}}";
        FrigateSource.TryParseEvent(json, out FrigateEventMessage msg);

        Assert.True(FrigateSource.ShouldFetchSnapshot(msg));
    }

    [Fact]
    public void ShouldFetchSnapshot_FalseOnLaterUpdateThatAlreadyHadSnapshot()
    {
        //a long-tracked object keeps publishing "update" messages - we must not re-fetch/re-queue every time
        string json = @"{""type"":""update"",""before"":{""id"":""1"",""camera"":""cam"",""label"":""person"",""has_snapshot"":true},""after"":{""id"":""1"",""camera"":""cam"",""label"":""person"",""has_snapshot"":true}}";
        FrigateSource.TryParseEvent(json, out FrigateEventMessage msg);

        Assert.False(FrigateSource.ShouldFetchSnapshot(msg));
    }

    [Theory]
    [InlineData("", "person", true)]
    [InlineData("person", "person", true)]
    [InlineData("person,car", "Car", true)] //case insensitive
    [InlineData("person,car", "dog", false)]
    [InlineData("person", "", false)]
    public void PassesFilter_MatchesCsvListCaseInsensitive(string filter, string value, bool expected)
    {
        Assert.Equal(expected, FrigateSource.PassesFilter(value, filter));
    }
}

public class FrigateCameraMappingTests
{
    [Fact]
    public void MapCamera_PrefersBICamName()
    {
        Camera byName = new Camera { Name = "Driveway", BICamName = "", Prefix = "" };
        Camera byBICamName = new Camera { Name = "Other", BICamName = "front_door", Prefix = "" };
        List<Camera> cams = new List<Camera> { byName, byBICamName };

        Camera result = FrigateSource.MapCamera("front_door", cams);

        Assert.Same(byBICamName, result);
    }

    [Fact]
    public void MapCamera_FallsBackToNameWhenNoBICamNameMatches()
    {
        Camera byName = new Camera { Name = "front_door", BICamName = "", Prefix = "" };
        List<Camera> cams = new List<Camera> { byName };

        Camera result = FrigateSource.MapCamera("front_door", cams);

        Assert.Same(byName, result);
    }

    [Fact]
    public void MapCamera_FallsBackToPrefixWhenNoBICamNameOrNameMatches()
    {
        Camera byPrefix = new Camera { Name = "Cam1", BICamName = "", Prefix = "front_door" };
        List<Camera> cams = new List<Camera> { byPrefix };

        Camera result = FrigateSource.MapCamera("front_door", cams);

        Assert.Same(byPrefix, result);
    }

    [Fact]
    public void MapCamera_IsCaseInsensitive()
    {
        Camera cam = new Camera { Name = "Front_Door", BICamName = "", Prefix = "" };

        Camera result = FrigateSource.MapCamera("FRONT_DOOR", new List<Camera> { cam });

        Assert.Same(cam, result);
    }

    [Fact]
    public void MapCamera_ReturnsNullWhenNothingMatches()
    {
        Camera cam = new Camera { Name = "Backyard", BICamName = "", Prefix = "" };

        Camera result = FrigateSource.MapCamera("front_door", new List<Camera> { cam });

        Assert.Null(result);
    }

    [Fact]
    public void MapCamera_ReturnsNullForEmptyFrigateCameraName()
    {
        Camera cam = new Camera { Name = "Backyard" };

        Camera result = FrigateSource.MapCamera("", new List<Camera> { cam });

        Assert.Null(result);
    }

    [Fact]
    public void BuildFileName_UsesMatchedCameraPrefixAsLeadingToken()
    {
        Camera cam = new Camera { Name = "Front Door", BICamName = "front_door", Prefix = "FD" };
        DateTime ts = new DateTime(2026, 1, 2, 3, 4, 5, 678);

        string filename = FrigateSource.BuildFileName(cam, "front_door", "1696000000.123456-abc123", ts);

        Assert.Equal("FD.1696000000.123456-abc123.20260102_030405678.jpg", filename);
    }

    [Fact]
    public void BuildFileName_FallsBackToCameraNameWhenNoPrefix()
    {
        Camera cam = new Camera { Name = "FrontDoor", BICamName = "front_door", Prefix = "" };
        DateTime ts = new DateTime(2026, 1, 2, 3, 4, 5, 678);

        string filename = FrigateSource.BuildFileName(cam, "front_door", "1", ts);

        Assert.StartsWith("FrontDoor.1.", filename);
    }

    [Fact]
    public void BuildFileName_ReplacesDotsInEventIdSoCameraPrefixMatchingStaysUnambiguous()
    {
        //GetCamera()'s path-based prefix matching splits the filename on dots, so a dot anywhere else in the
        //token would be ambiguous - BuildFileName must not introduce one via the camera token itself.
        Camera cam = new Camera { Name = "Cam.With.Dots", BICamName = "", Prefix = "" };

        string filename = FrigateSource.BuildFileName(cam, "cam", "1", DateTime.Now);

        Assert.StartsWith("Cam_With_Dots.1.", filename);
    }
}

public class FrigateDedupeTests
{
    private static FrigateSource MakeSource(List<Camera> cameras, Action<string, Camera> enqueue, Func<string, byte[]> download)
    {
        FrigateSource src = new FrigateSource();
        src.GetCameras = () => cameras;
        src.GetCameraFilter = () => "";
        src.GetLabelFilter = () => "";
        src.EnqueueAction = enqueue;
        return src;
    }

    [Fact]
    public async Task ProcessEventJsonAsync_SkipsCameraThatFailsFilter()
    {
        Camera cam = new Camera { Name = "front_door" };
        int enqueueCount = 0;

        FrigateSource src = MakeSource(new List<Camera> { cam }, (p, c) => enqueueCount++, null);
        src.GetCameraFilter = () => "back_yard"; //front_door is not in the allow-list

        string json = @"{""type"":""new"",""after"":{""id"":""1"",""camera"":""front_door"",""label"":""person"",""has_snapshot"":true}}";

        bool result = await src.ProcessEventJsonAsync(json);

        Assert.False(result);
        Assert.Equal(0, enqueueCount);
    }

    [Fact]
    public async Task ProcessEventJsonAsync_SkipsLabelThatFailsFilter()
    {
        Camera cam = new Camera { Name = "front_door" };
        int enqueueCount = 0;

        FrigateSource src = MakeSource(new List<Camera> { cam }, (p, c) => enqueueCount++, null);
        src.GetLabelFilter = () => "car";

        string json = @"{""type"":""new"",""after"":{""id"":""1"",""camera"":""front_door"",""label"":""person"",""has_snapshot"":true}}";

        bool result = await src.ProcessEventJsonAsync(json);

        Assert.False(result);
        Assert.Equal(0, enqueueCount);
    }

    [Fact]
    public async Task ProcessEventJsonAsync_SkipsUnmatchedCamera()
    {
        Camera cam = new Camera { Name = "back_yard" };
        int enqueueCount = 0;

        FrigateSource src = MakeSource(new List<Camera> { cam }, (p, c) => enqueueCount++, null);

        string json = @"{""type"":""new"",""after"":{""id"":""1"",""camera"":""front_door"",""label"":""person"",""has_snapshot"":true}}";

        bool result = await src.ProcessEventJsonAsync(json);

        Assert.False(result);
        Assert.Equal(0, enqueueCount);
    }

    [Fact]
    public async Task ProcessEventJsonAsync_DedupesSameEventIdAcrossMultipleMessages()
    {
        Camera cam = new Camera { Name = "front_door" };
        int downloadCount = 0;

        FrigateSource src = MakeSource(new List<Camera> { cam }, (p, c) => { }, null);
        string tempFolder = Path.Combine(Path.GetTempPath(), "frigate_dedupe_test_" + Guid.NewGuid());
        src.GetSnapshotFolderSetting = () => tempFolder;
        src.Http = new System.Net.Http.HttpClient(new StubHandler(() =>
        {
            downloadCount++;
            return new byte[] { 1, 2, 3 };
        }));
        src.GetFrigateUrl = () => "http://127.0.0.1:1";

        string json = @"{""type"":""new"",""after"":{""id"":""dup-1"",""camera"":""front_door"",""label"":""person"",""has_snapshot"":true}}";

        bool first = await src.ProcessEventJsonAsync(json);
        bool second = await src.ProcessEventJsonAsync(json); //same event id again - should be a no-op

        Assert.True(first);
        Assert.False(second);
        Assert.Equal(1, downloadCount);

        try { Directory.Delete(tempFolder, true); } catch { }
    }

    //minimal HttpMessageHandler stub so the dedupe test doesn't need a real HTTP listener
    private class StubHandler : System.Net.Http.HttpMessageHandler
    {
        private readonly Func<byte[]> GetBytes;
        public StubHandler(Func<byte[]> getBytes) { this.GetBytes = getBytes; }

        protected override Task<System.Net.Http.HttpResponseMessage> SendAsync(System.Net.Http.HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
        {
            System.Net.Http.HttpResponseMessage resp = new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new System.Net.Http.ByteArrayContent(this.GetBytes())
            };
            return Task.FromResult(resp);
        }
    }
}
