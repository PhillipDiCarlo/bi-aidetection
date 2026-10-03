using System;
using System.Collections.Generic;
using AITool;
using Xunit;

namespace AITool.Tests;

public class ObjectTrackerTests
{
    private static ClsPrediction Pred(string label, int x, int y, int w, int h, ObjectType objType = ObjectType.Person)
    {
        return new ClsPrediction
        {
            Label = label,
            ObjType = objType,
            Result = ResultType.Relevant,
            XMin = x,
            YMin = y,
            XMax = x + w,
            YMax = y + h,
            ImageWidth = 1920,
            ImageHeight = 1080,
        };
    }

    private static Camera NewCam()
    {
        return new Camera(); //parameterless ctor - no disk access, just the field defaults (TrackTimeoutSeconds=15, TrackMinMatchPercent=30, LoiterSecondsRequired=0)
    }

    [Fact]
    public void Update_SameObjectAcrossFramesKeepsOneTrackAndAccumulatesAge()
    {
        var tracker = new ObjectTracker();
        var cam = NewCam();
        var t0 = new DateTime(2024, 1, 1, 12, 0, 0);

        var r1 = tracker.Update(cam, t0, new List<ClsPrediction> { Pred("person", 100, 100, 50, 100) });
        Assert.Single(r1);
        int trackId = r1[0].Track.Id;
        Assert.Equal(TimeSpan.Zero, r1[0].Age);

        var t1 = t0.AddSeconds(5);
        var r2 = tracker.Update(cam, t1, new List<ClsPrediction> { Pred("person", 103, 102, 50, 100) }); //slightly moved, same object

        Assert.Single(r2);
        Assert.Equal(trackId, r2[0].Track.Id);
        Assert.Equal(5, r2[0].Age.TotalSeconds, 3);
        Assert.Equal(2, r2[0].Track.HitCount);
    }

    [Fact]
    public void Update_DifferentLabelAtSameRectDoesNotMatchAndStartsItsOwnTrack()
    {
        var tracker = new ObjectTracker();
        var cam = NewCam();
        var t0 = new DateTime(2024, 1, 1, 12, 0, 0);

        var r1 = tracker.Update(cam, t0, new List<ClsPrediction> { Pred("person", 100, 100, 50, 100) });
        int personTrackId = r1[0].Track.Id;

        //same rectangle on the next image, but a different label - must not be treated as the same object
        var t1 = t0.AddSeconds(1);
        var r2 = tracker.Update(cam, t1, new List<ClsPrediction> { Pred("car", 100, 100, 50, 100, ObjectType.Vehicle) });

        Assert.Single(r2);
        Assert.NotEqual(personTrackId, r2[0].Track.Id);
        Assert.Equal(TimeSpan.Zero, r2[0].Age); //brand new track
    }

    [Fact]
    public void Update_TwoPeopleSideBySideGetTwoTracks()
    {
        var tracker = new ObjectTracker();
        var cam = NewCam();
        var t0 = new DateTime(2024, 1, 1, 12, 0, 0);

        var results = tracker.Update(cam, t0, new List<ClsPrediction>
        {
            Pred("person", 0, 0, 50, 100),
            Pred("person", 500, 0, 50, 100),
        });

        Assert.Equal(2, results.Count);
        Assert.NotEqual(results[0].Track.Id, results[1].Track.Id);
    }

    [Fact]
    public void Update_TrackExpiresAfterTimeoutAndReappearingObjectStartsNewTrack()
    {
        var tracker = new ObjectTracker();
        var cam = NewCam();
        cam.TrackTimeoutSeconds = 15;
        var t0 = new DateTime(2024, 1, 1, 12, 0, 0);

        var r1 = tracker.Update(cam, t0, new List<ClsPrediction> { Pred("person", 100, 100, 50, 100) });
        int firstTrackId = r1[0].Track.Id;

        var t1 = t0.AddSeconds(cam.TrackTimeoutSeconds + 1); //well past the timeout
        var r2 = tracker.Update(cam, t1, new List<ClsPrediction> { Pred("person", 100, 100, 50, 100) });

        Assert.NotEqual(firstTrackId, r2[0].Track.Id);
        Assert.Equal(TimeSpan.Zero, r2[0].Age); //brand new track, not the old one's accumulated age
    }

    [Fact]
    public void Update_MovingObjectMatchedViaCenterDistanceFallback()
    {
        var tracker = new ObjectTracker();
        var cam = NewCam();
        var t0 = new DateTime(2024, 1, 1, 12, 0, 0);

        var r1 = tracker.Update(cam, t0, new List<ClsPrediction> { Pred("person", 100, 100, 40, 40) });
        int trackId = r1[0].Track.Id;

        //shifted 30px right - overlap (Dice) drops to 25%, below the 30% default TrackMinMatchPercent,
        //but the center only moved 30px against a ~56.6px diagonal, well within the fallback factor
        var t1 = t0.AddSeconds(1);
        var r2 = tracker.Update(cam, t1, new List<ClsPrediction> { Pred("person", 130, 100, 40, 40) });

        Assert.Single(r2);
        Assert.Equal(trackId, r2[0].Track.Id);
    }

    [Fact]
    public void ShouldTrigger_DisabledWhenLoiterSecondsRequiredIsZero()
    {
        var cam = NewCam();
        cam.LoiterSecondsRequired = 0;

        bool ok = ObjectTracker.ShouldTrigger(cam, new List<TrackMatch>(), out ObjectTrack track, out TimeSpan age);

        Assert.True(ok);
        Assert.Null(track);
        Assert.Equal(TimeSpan.Zero, age);
    }

    [Fact]
    public void ShouldTrigger_FalseBeforeRequiredSecondsAndTrueAtOrAfter()
    {
        var cam = NewCam();
        cam.LoiterSecondsRequired = 20;
        var t0 = new DateTime(2024, 1, 1, 12, 0, 0);

        var track = new ObjectTrack { Id = 3, Label = "person", FirstSeen = t0, LastSeen = t0.AddSeconds(6.2) };
        var match = new TrackMatch { Track = track };

        bool notYet = ObjectTracker.ShouldTrigger(cam, new List<TrackMatch> { match }, out ObjectTrack reportedTrack, out TimeSpan age);
        Assert.False(notYet);
        Assert.Same(track, reportedTrack);
        Assert.Equal(6.2, age.TotalSeconds, 3);
        Assert.False(track.Triggered);

        track.LastSeen = t0.AddSeconds(20); //exactly the requirement
        bool nowOk = ObjectTracker.ShouldTrigger(cam, new List<TrackMatch> { match }, out ObjectTrack triggeredTrack, out TimeSpan triggeredAge);
        Assert.True(nowOk);
        Assert.Same(track, triggeredTrack);
        Assert.Equal(20, triggeredAge.TotalSeconds, 3);
        Assert.True(track.Triggered);
    }

    [Fact]
    public void ShouldTrigger_DoesNotReTriggerSameTrackOnceItHasFired()
    {
        var cam = NewCam();
        cam.LoiterSecondsRequired = 20;
        var t0 = new DateTime(2024, 1, 1, 12, 0, 0);

        var track = new ObjectTrack { Id = 1, Label = "person", FirstSeen = t0, LastSeen = t0.AddSeconds(20) };
        var match = new TrackMatch { Track = track };

        Assert.True(ObjectTracker.ShouldTrigger(cam, new List<TrackMatch> { match }, out _, out _));
        Assert.True(track.Triggered);

        //still present, well past the requirement - but already triggered, so no re-trigger
        track.LastSeen = t0.AddSeconds(25);
        bool triggeredAgain = ObjectTracker.ShouldTrigger(cam, new List<TrackMatch> { match }, out ObjectTrack reported, out _);

        Assert.False(triggeredAgain);
        Assert.Same(track, reported); //still reported for logging ("present Xs of Ys required"), just not re-triggered
    }
}
