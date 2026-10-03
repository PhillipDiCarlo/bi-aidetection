using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Management;
using System.Media;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;

using Innovative.SolarCalculator;

using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

using NAudio.Wave;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

using static AITool.AITOOL;

namespace AITool
{
    public static partial class Global
    {

        public static string FindSoundFile(string InputFile)
        {
            //only return the sound file if we found it
            string ret = "";
            try
            {
                //If the file passed contains a full path, just verify it exists
                if (InputFile.Contains("\\"))
                {
                    if (File.Exists(InputFile))
                    {
                        ret = InputFile;
                    }
                }
                else  //Assume it is just a filename and look for it...
                {
                    //add extension if not found
                    if (!Path.HasExtension(InputFile))
                        InputFile = InputFile.Trim() + ".wav";

                    //generate a list of known media paths
                    List<string> paths = new List<string>();
                    paths.Add(AppDomain.CurrentDomain.BaseDirectory);
                    paths.Add(Path.GetDirectoryName(AppSettings.Settings.SettingsFileName));

                    if (AITOOL.BlueIrisInfo.IsNotNull() && AITOOL.BlueIrisInfo.AppPath.IsNotNull() && Directory.Exists(AITOOL.BlueIrisInfo.AppPath))
                        paths.Add(Path.Combine(AITOOL.BlueIrisInfo.AppPath, "sounds"));

                    paths.Add(Environment.ExpandEnvironmentVariables("%SYSTEMROOT%\\Media"));

                    if (Directory.Exists(Environment.ExpandEnvironmentVariables("%PROGRAMFILES%\\Microsoft Office\\root\\Office16\\Media")))
                        paths.Add(Environment.ExpandEnvironmentVariables("%PROGRAMFILES%\\Microsoft Office\\root\\Office16\\Media"));

                    if (Directory.Exists(Environment.ExpandEnvironmentVariables("%PROGRAMFILES%\\Microsoft Office\\root\\Office15\\Media")))
                        paths.Add(Environment.ExpandEnvironmentVariables("%PROGRAMFILES%\\Microsoft Office\\root\\Office15\\Media"));

                    //search each path for the sound file
                    foreach (var fldr in paths)
                    {
                        string file = Path.Combine(fldr, InputFile);
                        if (File.Exists(file))
                        {
                            ret = file;
                            break;
                        }
                    }

                }
            }
            catch (Exception ex)
            {
                Log($"Error: {ex.Msg()}");
            }

            return ret;
        }

        public static void PlayTick(SoundPlayer player)
        {
            if (AppSettings.Settings.Tick && player != null)
            {
                // Play the sound asynchronously
                Task.Run(() =>
                {
                    // Rewind the player and play the sound
                    player.PlaySync();
                });

            }
        }

        public static void PlayOOG(string filename)
        {
            //This cannot play .OGG files created by the Telegram.Bot engine?
            //Could not load stream 1483939711 due to error: Found OPUS bitstream.
            //'System.ArgumentException' in NAudio.Vorbis.dll
            //Could not initialize container!
            using NAudio.Vorbis.VorbisWaveReader vorbis = new NAudio.Vorbis.VorbisWaveReader(filename);
            using NAudio.Wave.WaveOut waveOut = new NAudio.Wave.WaveOut();
            waveOut.Init(vorbis);
            waveOut.Play();
            while (waveOut.PlaybackState == PlaybackState.Playing)
            {
                if (MasterCTS.IsNotNull() && MasterCTS.Token.IsCancellationRequested)
                    break;

                Task.Delay(100, MasterCTS.Token);
            }

        }

        public static void Mute()
        {

            //SendMessageW(hand, WM_APPCOMMAND, hand, (IntPtr)APPCOMMAND_VOLUME_MUTE);

            using (var enumerator = new NAudio.CoreAudioApi.MMDeviceEnumerator())
            {
                foreach (var device in enumerator.EnumerateAudioEndPoints(NAudio.CoreAudioApi.DataFlow.Render, NAudio.CoreAudioApi.DeviceState.Active))
                {
                    if (device.AudioEndpointVolume?.HardwareSupport.HasFlag(NAudio.CoreAudioApi.EEndpointHardwareSupport.Mute) == true)
                    {
                        Console.WriteLine(device.FriendlyName);
                        device.AudioEndpointVolume.Mute = true;
                    }
                }
            }
        }
        public static void UnMute()
        {
            using (var enumerator = new NAudio.CoreAudioApi.MMDeviceEnumerator())
            {
                foreach (var device in enumerator.EnumerateAudioEndPoints(NAudio.CoreAudioApi.DataFlow.Render, NAudio.CoreAudioApi.DeviceState.Active))
                {
                    if (device.AudioEndpointVolume?.HardwareSupport.HasFlag(NAudio.CoreAudioApi.EEndpointHardwareSupport.Mute) == true)
                    {
                        device.AudioEndpointVolume.Mute = false;
                    }
                }
            }
        }

        public static void VolDown()
        {
            //SendMessageW(hand, WM_APPCOMMAND, hand, (IntPtr)APPCOMMAND_VOLUME_DOWN);

            using (var enumerator = new NAudio.CoreAudioApi.MMDeviceEnumerator())
            {
                foreach (var device in enumerator.EnumerateAudioEndPoints(NAudio.CoreAudioApi.DataFlow.Render, NAudio.CoreAudioApi.DeviceState.Active))
                {
                    if (device.AudioEndpointVolume?.HardwareSupport.HasFlag(NAudio.CoreAudioApi.EEndpointHardwareSupport.Volume) == true)
                    {
                        device.AudioEndpointVolume.VolumeStepDown();
                    }
                }
            }

        }

        public static void VolUp()
        {
            //SendMessageW(hand, WM_APPCOMMAND, hand, (IntPtr)APPCOMMAND_VOLUME_UP);
            using (var enumerator = new NAudio.CoreAudioApi.MMDeviceEnumerator())
            {
                foreach (var device in enumerator.EnumerateAudioEndPoints(NAudio.CoreAudioApi.DataFlow.Render, NAudio.CoreAudioApi.DeviceState.Active))
                {
                    if (device.AudioEndpointVolume?.HardwareSupport.HasFlag(NAudio.CoreAudioApi.EEndpointHardwareSupport.Volume) == true)
                    {
                        device.AudioEndpointVolume.VolumeStepUp();
                    }
                }
            }

        }

        public static void VolSet(float Level)
        {
            //SendMessageW(hand, WM_APPCOMMAND, hand, (IntPtr)APPCOMMAND_VOLUME_UP);
            using (var enumerator = new NAudio.CoreAudioApi.MMDeviceEnumerator())
            {
                foreach (var device in enumerator.EnumerateAudioEndPoints(NAudio.CoreAudioApi.DataFlow.Render, NAudio.CoreAudioApi.DeviceState.Active))
                {
                    if (device.AudioEndpointVolume?.HardwareSupport.HasFlag(NAudio.CoreAudioApi.EEndpointHardwareSupport.Volume) == true)
                    {
                        //device.AudioEndpointVolume.VolumeRange.MaxDecibels
                        //device.AudioEndpointVolume.VolumeRange.MinDecibels
                        //The new master volume level. The level is expressed as a normalized value in the range from 0.0 to 1.0.
                        device.AudioEndpointVolume.MasterVolumeLevelScalar = Level / 100f;
                    }
                }
            }

        }
    }
}
