# Loitering (object tracking across frames)

Blue Iris sends several JPEGs per motion event, and keeps sending while motion continues. AI Tool can use
that to require a relevant object to have been continuously present for at least N seconds before it
triggers actions - e.g. a person standing at the door for 20 seconds, not one just walking past.

This is per-camera object tracking: each camera keeps its own set of "tracks" (one per object currently in
view), matching each new relevant prediction to an existing track of the same label by rectangle overlap,
falling back to center-distance for small or fast-moving objects whose boxes don't overlap much between
images. A track that hasn't been matched in a while expires; a reappearing object then starts a fresh
track at age zero.

## Setting

Cameras > [camera] > **Prediction Tolerances** (the same dialog as the confidence/size/duplicate-match
settings) > **Loitering**:

* **Trigger only if present for (seconds)** - `LoiterSecondsRequired`. `0` (the default) disables the
  feature entirely - this is today's behavior, actions fire on the first relevant detection as always.
  Any value above `0` requires a relevant object's track to have aged at least that many seconds before
  actions are triggered.

Two related, lower-level settings exist on `Camera` for tuning the tracker itself (no dedicated UI yet -
edit the camera's JSON in settings if you need to change them from their defaults):

* `TrackTimeoutSeconds` (default `15`) - how long a track may go unmatched before it's considered gone.
  Once a camera's gap between images exceeds this, a reappearing object is treated as new (age resets).
* `TrackMinMatchPercent` (default `30`) - minimum rectangle match percent (Dice coefficient, see
  `Rectangle.IntersectPercent` in `RectangleExtensions.cs`) for a new prediction to be considered the same
  object as an existing track, before falling back to the center-distance check.

## What happens while waiting to loiter long enough

Each image is still run through AI detection normally. If a relevant object's track hasn't reached the
required age yet, AI Tool does **not** trigger actions (MQTT/webhook/URL/etc.) for that image. It is
logged at Debug level (e.g. `person track #3 present 6.2s of 20s required`) and recorded in History as a
skipped alert (so it shows up like other cooldown-skipped images, including the History tab's "skipped"
filter/coloring) rather than as a full alert or a false/irrelevant alert.

Once a track reaches the required age, actions trigger once for that track; the track is marked so it
won't trigger again on subsequent images of the same event until it expires (the camera's normal trigger
cooldown also still applies on top of this).

## Template variables

Two new variables are available wherever other detection variables are (Actions > Variables):

* `[TrackSeconds]` - how long (in seconds) the longest-present relevant track from this detection has been
  around.
* `[TrackId]` - that track's internal id (useful mostly for debugging/logging; ids are only unique within
  a single camera's tracker for the lifetime of the app).

Both are `0` when loitering/tracking hasn't matched anything (e.g. the very first image of a new object).

## Out of scope

Line-crossing ("trigger only when the object crosses a drawn line") is not implemented - it needs a
line-drawing UI on top of the camera's mask image, which is a larger follow-up. The per-track rectangle
history kept by `ObjectTracker` would support it reasonably directly once that UI exists.
