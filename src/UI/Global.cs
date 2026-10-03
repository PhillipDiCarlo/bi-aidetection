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
    // =============================================================
    // ALL FUNCTIONS HERE ARE GENERIC/SHARED AND NOT UNIQUE TO AITOOL
    // NO direct UI interaction
    // =============================================================

        public static IProgress<ClsMessage> progress = null;

        /// <summary>
        ///     ''' Gets a value indicating whether the application is a windows service.
        ///     ''' </summary>
        ///     ''' <value>
        ///     ''' <c>true</c> if this instance is service; otherwise, <c>false</c>.

        ///     ''' </value>
        public static bool IsService
        {
            get
            {
                // Determining whether or not the host application is a service is
                // an expensive operation (it uses reflection), so we cache the
                // result of the first call to this method so that we don't have to
                // recalculate it every call.

                // If we have not already determined whether or not the application
                // is running as a service...
                if (!_isService.HasValue)
                {

                    // Get details of the host assembly.
                    System.Reflection.Assembly entryAssembly = System.Reflection.Assembly.GetEntryAssembly();

                    // Get the method that was called to enter the host assembly.
                    System.Reflection.MethodInfo entryPoint = entryAssembly.EntryPoint;

                    // If the base type of the host assembly inherits from the
                    // "ServiceBase" class, it must be a windows service. We store
                    // the result ready for the next caller of this method.
                    _isService = (entryPoint.ReflectedType.BaseType.FullName == "System.ServiceProcess.ServiceBase");
                }

                // Return the cached result.
                return System.Convert.ToBoolean(_isService);
            }
        }

        private static Nullable<bool> _isService = default(Boolean?);
        public static void ResponsiveSleep(int SleepMS)
        {
            Stopwatch sw = Stopwatch.StartNew();
            do
            {
                Thread.Sleep(50);
                //I've seen threading errors happen with doevents calls, so wrap in try/catch...
                try
                { Application.DoEvents(); }  //Cover your eyes, nothing to see here.  DoEvents isnt that bad.  Really.  Ok, bye.
                catch { }

            } while (sw.ElapsedMilliseconds <= SleepMS);
        }

    }
}
