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
    /// <summary>Starts an external program with template-substituted arguments.</summary>
    public class RunProgramAction : IActionChannel
    {
        public string Name => "RunProgram";

        public bool ShouldRun(ClsTriggerActionQueueItem AQI)
        {
            return AQI.cam.Action_RunProgram && AQI.Trigger;
        }

        public async Task<bool> RunAsync(ClsTriggerActionQueueItem AQI, string CurSrv)
        {
            bool ret = true;

            string run = "";
            string param = "";
            try
            {
                run = AITOOL.ReplaceParams(AQI.cam, AQI.Hist, AQI.CurImg, AQI.cam.Action_RunProgramString, Global.IPType.Path);
                param = AITOOL.ReplaceParams(AQI.cam, AQI.Hist, AQI.CurImg, AQI.cam.Action_RunProgramArgsString, Global.IPType.Path);
                Log($"Debug:   Starting external app - Camera={AQI.camname} run='{run}', param='{param}'", CurSrv, AQI.cam, AQI.CurImg);

                Process.Start(run, param);

                if (AppSettings.Settings.ActionDelayMS >= 100)  //dont show for tiny delays
                    Log($"Debug: ...Applying 'ActionDelayMS' delay of {AppSettings.Settings.ActionDelayMS}ms.");

                //await Task.Delay(AppSettings.Settings.ActionDelayMS); //very short wait between trigger events
            }
            catch (Exception ex)
            {

                ret = false;
                Log($"Error: while running program '{run}' with params '{param}', got: {ex.Msg()}", CurSrv, AQI.cam, AQI.CurImg);
            }
            await Task.CompletedTask;

            return ret;
        }
    }
}
