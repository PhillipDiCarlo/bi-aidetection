using System;
using System.Collections.Generic;

namespace AITool.WebDashboard
{
    //Plain DTOs returned by the /api/... endpoints.  Kept separate from History/Camera/ClsURLItem so the
    //JSON contract stays small/stable and doesn't leak internal fields (full file paths, ThreadSafe wrappers, etc).

    public class DashboardStatusModel
    {
        public string Version { get; set; } = "";
        public double UptimeSeconds { get; set; } = 0;
        public int ImageQueueLength { get; set; } = 0;
        public int ActionQueueLength { get; set; } = 0;
        public DateTime? LastDetectionTime { get; set; } = null;
        public List<DashboardCameraModel> Cameras { get; set; } = new List<DashboardCameraModel>();
    }

    public class DashboardCameraModel
    {
        public string Name { get; set; } = "";
        public bool Enabled { get; set; } = true;
        public bool Paused { get; set; } = false;
        public DateTime? ResumeTime { get; set; } = null;
        public DateTime? LastTriggerTime { get; set; } = null;
        public int StatsAlerts { get; set; } = 0;
        public int StatsFalseAlerts { get; set; } = 0;
        public int StatsIrrelevantAlerts { get; set; } = 0;
        public int StatsSkippedImages { get; set; } = 0;
    }

    public class DashboardServerModel
    {
        public string Name { get; set; } = "";
        public string Type { get; set; } = "";
        public bool Enabled { get; set; } = true;
        public bool Online { get; set; } = false;
        public bool InUse { get; set; } = false;
        public string LastResultMessage { get; set; } = "";
        public int AvgTimeMS { get; set; } = 0;
        public int ErrCount { get; set; } = 0;
        public bool ErrDisabled { get; set; } = false;
    }

    public class DashboardHistoryItemModel
    {
        public string Camera { get; set; } = "";
        public DateTime Date { get; set; } = DateTime.MinValue;
        public string Detections { get; set; } = "";
        public bool Success { get; set; } = false;
        public string ImageUrl { get; set; } = "";
        public string AnnotatedImageUrl { get; set; } = "";
    }

    /// <summary>
    /// Request body for POST /api/pause and /api/resume.
    /// Camera is a camera name, "all"/"All Cameras" (case-insensitive), or blank - all three mean "every camera".
    /// </summary>
    public class DashboardPauseRequest
    {
        public string Camera { get; set; } = "";
        public double? Minutes { get; set; } = null;
    }
}
