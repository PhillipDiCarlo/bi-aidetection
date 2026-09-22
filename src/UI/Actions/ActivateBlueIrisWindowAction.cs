using System;
using System.Reflection;
using MQTTnet;
using SixLabors.ImageSharp;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Media;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Speech.Synthesis;
using System.Threading.Tasks;
using MQTTnet.Protocol;
using NPushover.RequestObjects;
using NPushover.ResponseObjects;
using Telegram.Bot.Exceptions;
using static AITool.AITOOL;

namespace AITool.Actions
{
    /// <summary>Brings the Blue Iris window to the front (only when BI runs on the same machine).</summary>
    public class ActivateBlueIrisWindowAction : IActionChannel
    {
        public string Name => "ActivateBlueIrisWindow";

        public bool ShouldRun(ClsTriggerActionQueueItem AQI)
        {
            return AQI.cam.Action_ActivateBlueIrisWindow && AQI.Trigger;
        }

        public async Task<bool> RunAsync(ClsTriggerActionQueueItem AQI, string CurSrv)
        {
            bool ret = true;

            Global.ShowProcessWindow("blueiris.exe", "Blue Iris", Global.ShowWindowEnum.SW_SHOWMAXIMIZED);
            await Task.CompletedTask;

            return ret;
        }
    }
}
