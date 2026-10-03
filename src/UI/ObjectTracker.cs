using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace AITool
{
    //A single object tracked across the several images Blue Iris sends for one motion event
    //(and keeps sending while motion continues).
    public class ObjectTrack
    {
        public int Id { get; set; }
        public string Label { get; set; } = "";
        public ObjectType ObjType { get; set; }
        public DateTime FirstSeen { get; set; }
        public DateTime LastSeen { get; set; }
        public Rectangle LastRect { get; set; }
        public int HitCount { get; set; } = 1;

        //Set once the loiter gate (ObjectTracker.ShouldTrigger) has fired for this track, so we
        //don't re-trigger on every later image of the same event. There is nothing to clear this
        //back to false - once the object is gone long enough the track itself expires and a
        //reappearing object starts a brand new (untriggered) track.
        public bool Triggered { get; set; } = false;
    }

    //One prediction matched (or newly assigned) to a track, as returned by ObjectTracker.Update().
    public class TrackMatch
    {
        public ClsPrediction Prediction { get; set; }
        public ObjectTrack Track { get; set; }

        //How long this track has been continuously present, as of this image.
        public TimeSpan Age => this.Track.LastSeen - this.Track.FirstSeen;
    }

    //Per-camera object tracker. Matches relevant predictions to tracks of the same label across
    //images so a camera can require an object to have been present for N seconds ("loitering")
    //before it triggers actions. Pure logic, no UI or static app state - unit-testable on its own.
    public class ObjectTracker
    {
        //How far a matched rectangle's center may have moved - as a multiple of the larger of the
        //two rectangles' diagonals - to still be considered the same object when IoU/Dice overlap
        //alone falls short of Camera.TrackMinMatchPercent. Covers small or fast-moving objects
        //whose boxes may not overlap much between images.
        private const double CenterMoveFallbackFactor = 0.75;

        private readonly object _lock = new object();
        private readonly List<ObjectTrack> _tracks = new List<ObjectTrack>();
        private int _nextId = 1;

        public List<TrackMatch> Update(Camera cam, DateTime imageTime, List<ClsPrediction> relevantPredictions)
        {
            List<TrackMatch> ret = new List<TrackMatch>();

            lock (this._lock)
            {
                //a track not seen for a while belongs to a different, later object
                this._tracks.RemoveAll(t => (imageTime - t.LastSeen).TotalSeconds > cam.TrackTimeoutSeconds);

                List<ClsPrediction> unmatchedPreds = new List<ClsPrediction>(relevantPredictions ?? new List<ClsPrediction>());
                List<ObjectTrack> unmatchedTracks = new List<ObjectTrack>(this._tracks);

                //1. match by rectangle overlap (Dice coefficient via Rectangle.IntersectPercent),
                //same label required, best-scoring pairs win first
                var overlapCandidates = new List<(ClsPrediction Pred, ObjectTrack Track, double Score)>();
                foreach (ClsPrediction pred in unmatchedPreds)
                {
                    Rectangle predRect = pred.GetRectangle();
                    foreach (ObjectTrack track in unmatchedTracks)
                    {
                        if (!track.Label.EqualsIgnoreCase(pred.Label))
                            continue;

                        double score = predRect.IntersectPercent(track.LastRect);
                        if (score >= cam.TrackMinMatchPercent)
                            overlapCandidates.Add((pred, track, score));
                    }
                }

                foreach (var c in overlapCandidates.OrderByDescending(x => x.Score))
                {
                    if (!unmatchedPreds.Contains(c.Pred) || !unmatchedTracks.Contains(c.Track))
                        continue; //one side already claimed by a better-scoring pair

                    ret.Add(this.ApplyMatch(c.Track, c.Pred, imageTime));
                    unmatchedPreds.Remove(c.Pred);
                    unmatchedTracks.Remove(c.Track);
                }

                //2. fallback for small/fast moving objects where boxes barely overlap: same label,
                //closest center, within a reasonable distance relative to the object's own size
                var centerCandidates = new List<(ClsPrediction Pred, ObjectTrack Track, double Distance)>();
                foreach (ClsPrediction pred in unmatchedPreds)
                {
                    Rectangle predRect = pred.GetRectangle();
                    Point predCenter = predRect.Center();
                    foreach (ObjectTrack track in unmatchedTracks)
                    {
                        if (!track.Label.EqualsIgnoreCase(pred.Label))
                            continue;

                        double dist = Distance(predCenter, track.LastRect.Center());
                        double maxDiag = Math.Max(Diagonal(predRect), Diagonal(track.LastRect));
                        if (dist <= maxDiag * CenterMoveFallbackFactor)
                            centerCandidates.Add((pred, track, dist));
                    }
                }

                foreach (var c in centerCandidates.OrderBy(x => x.Distance))
                {
                    if (!unmatchedPreds.Contains(c.Pred) || !unmatchedTracks.Contains(c.Track))
                        continue;

                    ret.Add(this.ApplyMatch(c.Track, c.Pred, imageTime));
                    unmatchedPreds.Remove(c.Pred);
                    unmatchedTracks.Remove(c.Track);
                }

                //3. anything left over is a brand new object
                foreach (ClsPrediction pred in unmatchedPreds)
                {
                    ObjectTrack track = new ObjectTrack
                    {
                        Id = this._nextId++,
                        Label = pred.Label,
                        ObjType = pred.ObjType,
                        FirstSeen = imageTime,
                        LastSeen = imageTime,
                        LastRect = pred.GetRectangle(),
                        HitCount = 1,
                    };
                    this._tracks.Add(track);
                    ret.Add(new TrackMatch { Prediction = pred, Track = track });
                }
            }

            return ret;
        }

        private TrackMatch ApplyMatch(ObjectTrack track, ClsPrediction pred, DateTime imageTime)
        {
            track.LastSeen = imageTime;
            track.LastRect = pred.GetRectangle();
            track.HitCount++;
            return new TrackMatch { Prediction = pred, Track = track };
        }

        private static double Distance(Point a, Point b)
        {
            double dx = a.X - b.X;
            double dy = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private static double Diagonal(Rectangle rect)
        {
            return Math.Sqrt((double)rect.Width * rect.Width + (double)rect.Height * rect.Height);
        }

        //The loiter gate: true if actions should proceed given the current tracks, false if the
        //camera requires loitering and nothing has been present long enough yet. Pure/static so it
        //is unit-testable on its own. Its one side effect is marking the track it approves as
        //Triggered, so it won't fire again on the next image of the same event until the track
        //expires - the camera's regular cooldown still applies on top of this.
        public static bool ShouldTrigger(Camera cam, List<TrackMatch> trackMatches, out ObjectTrack triggeringTrack, out TimeSpan age)
        {
            triggeringTrack = null;
            age = TimeSpan.Zero;

            if (cam.LoiterSecondsRequired <= 0)
                return true; //loitering requirement disabled - today's behavior

            ObjectTrack longest = null;
            TimeSpan longestAge = TimeSpan.Zero;
            ObjectTrack qualifying = null;
            TimeSpan qualifyingAge = TimeSpan.Zero;

            foreach (TrackMatch m in trackMatches ?? new List<TrackMatch>())
            {
                TimeSpan a = m.Age;

                if (a > longestAge)
                {
                    longestAge = a;
                    longest = m.Track;
                }

                if (m.Track.Triggered)
                    continue; //already triggered for this track - wait for it to expire

                if (a.TotalSeconds >= cam.LoiterSecondsRequired && a > qualifyingAge)
                {
                    qualifyingAge = a;
                    qualifying = m.Track;
                }
            }

            if (qualifying != null)
            {
                qualifying.Triggered = true;
                triggeringTrack = qualifying;
                age = qualifyingAge;
                return true;
            }

            //nothing qualifies yet - report the longest-present track so the caller can log how
            //close it is ("person track #3 present 6.2s of 20s required")
            triggeringTrack = longest;
            age = longestAge;
            return false;
        }
    }
}
