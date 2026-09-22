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
    /// <summary>Plays wav files and/or speaks text (see the examples in the code) with a per-camera cooldown.</summary>
    public class SoundAction : IActionChannel
    {
        public string Name => "Sound";

        public bool ShouldRun(ClsTriggerActionQueueItem AQI)
        {
            return AQI.cam.Action_PlaySounds && AQI.Trigger;
        }

        public async Task<bool> RunAsync(ClsTriggerActionQueueItem AQI, string CurSrv)
        {
            bool ret = true;

            double soundcooltime = (DateTime.Now - AQI.cam.last_sound_time).TotalSeconds;

            if (soundcooltime >= AQI.cam.sound_cooldown_time_seconds)
            {
                try
                {

                    //Examples:
                    //Simple sound play:
                    //    C:\BlueIris\sounds\are-you-kidding.wav
                    //    are-you-kidding.wav   <-- No need to specify path if in AITOOL, BlueIris folder or Windows Media folder
                    //    are-you-kidding
                    //    C:\Windows\Media\Ring10.wav
                    //    Ring10.wav
                    //Conditional:
                    //    cat ; catsound.wav
                    //    cat,dog,sheep ; animalsound.wav
                    //    bear ; fuuuuck.wav
                    //Talk:
                    //    Talk:There is a [Label] outside
                    //    person ; talk:There is a mother f'in person in the driveway
                    //Combine any with pipe symbols
                    //    Talk:There is a [Label] outside | object1, object2 ; soundfile.wav | object1, object2 ; anotherfile.wav | * ; defaultsound.wav
                    string snds = AITOOL.ReplaceParams(AQI.cam, AQI.Hist, AQI.CurImg, AQI.cam.Action_Sounds, Global.IPType.Path);

                    bool wasplayed = false;

                    List<string> prms = snds.SplitStr("|");
                    foreach (string prm in prms)
                    {
                        if (prm.Contains(";"))
                        {
                            //prm0 - object1, object2
                            //prm1 - soundfile.wav
                            List<string> splt = prm.SplitStr(";");

                            ClsRelevantObjectManager rom = new ClsRelevantObjectManager(splt[0], "Sound", AQI.cam);

                            if (!AQI.Hist.IsNull() && rom.IsRelevant(AQI.Hist.Predictions(), false, out bool IgnoreImageMask, out bool IgnoreDynamicMask) == ResultType.Relevant)
                            {

                                if (splt[1].StartsWith("talk:", StringComparison.OrdinalIgnoreCase))
                                {
                                    string speech = splt[1].GetWord("talk:", "");

                                    if (speech.IsNotNull())
                                    {
                                        //if you would like to change the default voice used, you have to change it in the OLD control panel, not the new Windows 10/11 version?
                                        //Start menu > type 'Control Panel' > Easy of use > Speech Recognition > Advanced speech options > Text to speech tab.

                                        using var synth = new SpeechSynthesizer();

                                        synth.SetOutputToDefaultAudioDevice();
                                        Log($"Debug:   Talking using system default Windows voice '{synth.Voice.Name}': '{speech}'...", CurSrv, AQI.cam, AQI.CurImg);
                                        synth.Speak(speech);
                                        wasplayed = true;
                                    }

                                }
                                else
                                {
                                    string soundfile = Global.FindSoundFile(splt[1].Trim());
                                    if (soundfile.IsNotNull())
                                    {

                                        Log($"Debug:   Playing sound: {soundfile}...", CurSrv, AQI.cam, AQI.CurImg);
                                        using SoundPlayer sp = new SoundPlayer(soundfile);
                                        sp.PlaySync();
                                        wasplayed = true;
                                    }
                                    else
                                    {
                                        Log($"Error: Sound file not found: {soundfile}");
                                    }

                                }
                            }
                        }
                        else if (prm.StartsWith("talk:", StringComparison.OrdinalIgnoreCase))
                        {
                            string speech = prm.GetWord("talk:", "");

                            if (speech.IsNotNull())
                            {
                                using var synth = new SpeechSynthesizer();

                                synth.SetOutputToDefaultAudioDevice();
                                Log($"Debug:   Talking using system default Windows voice '{synth.Voice.Name}': '{speech}'...", CurSrv, AQI.cam, AQI.CurImg);
                                synth.Speak(speech);
                                wasplayed = true;
                            }
                        }
                        else   //assume it is JUST a sound file
                        {
                            string soundfile = Global.FindSoundFile(prm);
                            if (soundfile.IsNotNull())
                            {

                                Log($"Debug:   Playing sound: {soundfile}...", CurSrv, AQI.cam, AQI.CurImg);
                                using SoundPlayer sp = new SoundPlayer(soundfile);
                                sp.PlaySync();
                                wasplayed = true;
                            }
                            else
                            {
                                Log($"Error: Sound file not found: {prm}");
                            }
                        }

                        if (wasplayed && prms.Count > 1)
                            await Task.Delay(50); //very short wait between sound events

                    }

                    if (wasplayed)
                    {

                        AQI.cam.last_sound_time = DateTime.Now;

                        if (AppSettings.Settings.ActionDelayMS >= 100)  //dont show for tiny delays
                            Log($"Debug:  ...Applying 'ActionDelayMS' delay of {AppSettings.Settings.ActionDelayMS}ms.");

                        //lets not wait after each sound
                        //await Task.Delay(AppSettings.Settings.ActionDelayMS); //very short wait between trigger events

                    }
                    else
                    {
                        Log($"Debug: No object matched sound to play.", CurSrv, AQI.cam, AQI.CurImg);
                    }

                }
                catch (Exception ex)
                {

                    ret = false;
                    Log($"Error: while calling sound '{AQI.cam.Action_Sounds}', got: {ex.Msg()}", CurSrv, AQI.cam, AQI.CurImg);
                }

            }
            else
            {
                Log($"   Camera {AQI.camname} is still in SOUND cooldown. Sound was not played. ({soundcooltime} of {AQI.cam.sound_cooldown_time_seconds} seconds - See Cameras 'sound_cooldown_time_seconds' in settings file)", CurSrv, AQI.cam, AQI.CurImg);

            }

            return ret;
        }
    }
}
