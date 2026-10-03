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

        public static string GetFrameworkVersion()
        {
            using (RegistryKey ndpKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full"))
            {
                if (ndpKey != null)
                {
                    int value = (int)(ndpKey.GetValue("Release") ?? 0);
                    string ver = ndpKey.GetValue("Version", "Unknown").ToString();
                    if (value >= 528040)
                        return new Version(4, 8, 0).ToString() + $" (v{ver}, Release {value.ToString()})";

                    if (value >= 461808)
                        return new Version(4, 7, 2).ToString() + $" (v{ver}, Release {value.ToString()})";

                    if (value >= 461308)
                        return new Version(4, 7, 1).ToString() + $" (v{ver}, Release {value.ToString()})";

                    if (value >= 460798)
                        return new Version(4, 7, 0).ToString() + $" (v{ver}, Release {value.ToString()})";

                    if (value >= 394802)
                        return new Version(4, 6, 2).ToString() + $" (v{ver}, Release {value.ToString()})";

                    if (value >= 394254)
                        return new Version(4, 6, 1).ToString() + $" (v{ver}, Release {value.ToString()})";

                    if (value >= 393295)
                        return new Version(4, 6, 0).ToString() + $" (v{ver}, Release {value.ToString()})";

                    if (value >= 379893)
                        return new Version(4, 5, 2).ToString() + $" (v{ver}, Release {value.ToString()})";

                    if (value >= 378675)
                        return new Version(4, 5, 1).ToString() + $" (v{ver}, Release {value.ToString()})";

                    if (value >= 378389)
                        return new Version(4, 5, 0).ToString() + $" (v{ver}, Release {value.ToString()})";

                    return $"Unknown release {value}";
                }

                throw new NotSupportedException(@"No registry key found under 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full' to determine running framework version");
            }
        }
        public static bool IsAdministrator()
        {
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        public static Process GetaProcess(string processname)
        {
            try
            {
                if (Path.HasExtension(processname))
                    processname = Path.GetFileNameWithoutExtension(processname);
                Process[] aProc = Process.GetProcessesByName(processname);
                if (aProc.Length > 0)
                    return aProc[0];
                else
                    return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static Process[] GetProcesses(string processname)
        {
            try
            {
                if (Path.HasExtension(processname))
                    processname = Path.GetFileNameWithoutExtension(processname);
                Process[] aProc = Process.GetProcessesByName(processname);
                if (aProc.Length > 0)
                    return aProc;
                else
                    return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public enum ShowWindowEnum:int
        {
            /// <summary>
            ///        Hides the window and activates another window.
            /// </summary>
            SW_HIDE = 0,
            // <summary>
            ///        Activates and displays a window. If the window is minimized or maximized, the system restores it to its original size and position. An application should specify this flag when displaying the window for the first time.
            /// </summary>
            SW_SHOWNORMAL = 1,

            /// <summary>
            ///        Activates the window and displays it as a minimized window.
            /// </summary>
            SW_SHOWMINIMIZED = 2,

            /// <summary>
            ///        Activates the window and displays it as a maximized window.
            /// </summary>
            SW_SHOWMAXIMIZED = 3,

            /// <summary>
            ///        Maximizes the specified window.
            /// </summary>
            SW_MAXIMIZE = 3,

            /// <summary>
            ///        Displays a window in its most recent size and position. This value is similar to <see cref="ShowWindowCommands.SW_SHOWNORMAL"/>, except the window is not activated.
            /// </summary>
            SW_SHOWNOACTIVATE = 4,

            /// <summary>
            ///        Activates the window and displays it in its current size and position.
            /// </summary>
            SW_SHOW = 5,

            /// <summary>
            ///        Minimizes the specified window and activates the next top-level window in the z-order.
            /// </summary>
            SW_MINIMIZE = 6,

            /// <summary>
            ///        Displays the window as a minimized window. This value is similar to <see cref="ShowWindowCommands.SW_SHOWMINIMIZED"/>, except the window is not activated.
            /// </summary>
            SW_SHOWMINNOACTIVE = 7,

            /// <summary>
            ///        Displays the window in its current size and position. This value is similar to <see cref="ShowWindowCommands.SW_SHOW"/>, except the window is not activated.
            /// </summary>
            SW_SHOWNA = 8,

            /// <summary>
            ///        Activates and displays the window. If the window is minimized or maximized, the system restores it to its original size and position. An application should specify this flag when restoring a minimized window.
            /// </summary>
            SW_RESTORE = 9,

            /// <summary>
            ///        Items 10, 11 and 11 existed in the VB definition but not the c# definition - so I am assuming this was a mistake and have added them here.
            ///         Please forgive me if this is wrong!  I don't think it should have any negative impact.
            ///         According to what I have read elsewhere: The SW_SHOWDEFAULT makes sure the window is restored prior to showing, then activating.
            ///         And the 11's try to coerce a window to minimized or maximized.
            /// </summary>
            SW_SHOWDEFAULT = 10,
            SW_FORCEMINIMIZE = 11,
            SW_MAX = 11
        }
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool SetForegroundWindow(IntPtr hWnd);
        [System.Runtime.InteropServices.DllImport("User32.dll")]
        private static extern bool IsIconic(IntPtr handle);

        [DllImport("User32.dll", SetLastError = true)]
        static extern void SwitchToThisWindow(IntPtr hWnd, bool fAltTab);
        [DllImport("user32.dll", EntryPoint = "FindWindow")]
        private extern static IntPtr FindWindow(string lpClassName, string lpWindowName);
        public static bool ShowProcessWindow(string processname, string WindowTitle, ShowWindowEnum WindowStyle)
        {
            bool ret = false;

            Process[] Procs = Global.GetProcesses(processname);
            if (Procs.IsNotEmpty())
            {
                foreach (var proc in Procs)
                {
                    try
                    {
                        if (!proc.HasExited)
                        {
                            bool RefreshFound = false;

                            if (proc.MainWindowHandle.IsNull())
                                proc.Refresh();

                            IntPtr hwnd = proc.MainWindowHandle;

                            if (hwnd.IsNull())
                            {
                                //get the window a different way
                                hwnd = FindWindow(null, WindowTitle);
                            }
                            else
                            {
                                RefreshFound = true;
                            }

                            if (hwnd.IsNotNull())
                            {
                                bool IsIconicResult = IsIconic(hwnd);

                                if (IsIconicResult)  //check if the window is minimized. If it is not, then you don't want to restore it, you only need to activate it.
                                    SwitchToThisWindow(hwnd, true);   //SendMessageW(p.MainWindowHandle, WM_SYSCOMMAND, SC_RESTORE, 0) 'restore the window from it's minimized state

                                //true or nonzero if the window was brought to the foreground,
                                //false or zero If the window was not
                                bool SetForegroundResult = SetForegroundWindow(hwnd);
                                //Return value
                                //If the window was previously visible, the return value is nonzero.
                                //If the window was previously hidden, the return value is zero.
                                bool ShowWindowResult = ShowWindow(hwnd, ((int)WindowStyle));
                                ret = true;
                                Log($"Debug: Set '{processname}' ({proc.Id}, {hwnd.ToString()}) to '{WindowStyle.ToString()}' - RefreshFound = '{RefreshFound}', IsIconicResult = '{IsIconicResult}', SetForgroundWindow result = '{SetForegroundResult}', ShowWindowResult='{ShowWindowResult}'");

                            }
                            else
                            {
                                Log($"Debug: Could not get MainWindowHandle for process '{processname}, {WindowTitle}' ({proc.Id}).  It may be running as a service without GUI?");
                            }

                        }
                        else
                        {
                            Log($"Debug: process not valid '{processname}'.");
                        }

                    }
                    catch (Exception ex)
                    {
                        Log($"Trace: Error working with process: {ex.Msg()}");
                    }

                }
            }
            else
            {
                Log($"Debug: Could not find running process? '{processname}'.");
            }

            return ret;
        }
        public static bool KillProcesses(string ProcessPath)
        {
            List<ClsProcess> prc = GetProcessesByPath(ProcessPath);
            return KillProcesses(prc);
        }

        public static bool KillProcesses(List<ClsProcess> prc)
        {

            int valid = 0;
            int ccnt = 0;
            if (prc != null && prc.Count > 0)
            {
                foreach (ClsProcess curprc in prc)
                {
                    ccnt++;
                    if (ProcessValid(curprc))
                    {
                        try
                        {
                            Log($"Debug: Killing {ccnt} of {prc.Count}: {curprc.FileName}");
                            curprc.process.Kill();
                            valid++;
                        }
                        catch (Exception ex)
                        {

                            Log($"Error: Could not kill process {curprc.FileName}");
                        }
                    }
                    else
                    {
                        Log($"Process no longer valid {curprc.FileName}");

                    }
                }
                if (prc.Count == valid)
                    return true;
                else
                    return false;
            }
            return true;
        }



        public static bool WaitForProcessToStart(Process prc, int TimeoutMS, string fullpath)
        {
            bool ret = false;
            Stopwatch sw = Stopwatch.StartNew();
            try
            {
                if (prc != null && !prc.HasExited)
                {
                    long LastMem = 0;
                    int cnt = 0;
                    //prc.WaitForInputIdle(TimeoutMS); //I think this only works with GUI app
                    prc.Refresh();
                    while (sw.ElapsedMilliseconds <= TimeoutMS && !prc.HasExited && LastMem != prc.PrivateMemorySize64)
                    {
                        cnt++;
                        LastMem = prc.PrivateMemorySize64;
                        System.Threading.Thread.Sleep(AppSettings.Settings.loop_delay_ms);
                        prc.Refresh();
                    }
                    sw.Stop();
                    ret = prc != null && !prc.HasExited;
                    if (ret)
                        Log($"Debug: Waited {sw.ElapsedMilliseconds}ms (cnt={cnt}) for {fullpath} to initialize.");
                    else
                        Log($"Error: Process failed to start in {sw.ElapsedMilliseconds}ms (cnt={cnt}) for {fullpath} to initialize.");

                }
            }
            catch (Exception ex)
            {
                Log($"Error: {ex.Msg()}");
            }
            return ret;

        }
        public static bool WaitForProcessToClose(Process prc, int TimeoutMS, string fullpath)
        {
            bool ret = false;
            Stopwatch sw = Stopwatch.StartNew();
            try
            {
                if (prc != null && !prc.HasExited)
                {
                    int cnt = 0;
                    //prc.WaitForInputIdle(TimeoutMS); //I think this only works with GUI app
                    prc.Refresh();
                    while (sw.ElapsedMilliseconds <= TimeoutMS && !prc.HasExited)
                    {
                        cnt++;
                        System.Threading.Thread.Sleep(AppSettings.Settings.loop_delay_ms);
                        prc.Refresh();
                    }
                    sw.Stop();
                    ret = prc == null || prc.HasExited;
                    int exitcode = 0;
                    if (prc.IsNotNull())
                        exitcode = prc.ExitCode;

                    if (ret)
                        Log($"Debug: Process closed after {sw.ElapsedMilliseconds}ms (cnt={cnt}) for {fullpath} to close with exit code '{exitcode}'");
                    else
                        Log($"Debug: Process was still open after {sw.ElapsedMilliseconds}ms (cnt={cnt}) for {fullpath}");

                }
                else
                {
                    ret = true;  //exited or didnt exist
                }
            }
            catch (Exception ex)
            {
                Log($"Error: {ex.Msg()}");
            }
            return ret;

        }

        public static bool ProcessValid(List<ClsProcess> prc)
        {

            int valid = 0;
            if (prc != null && prc.Count > 0)
            {
                foreach (ClsProcess curprc in prc)
                {
                    if (ProcessValid(curprc))
                        valid++;
                    else
                        break;
                }
                if (prc.Count == valid)
                    return true;
            }
            return false;
        }
        public static bool ProcessValid(ClsProcess prc)
        {

            if (prc != null && prc.process != null)
            {
                try
                {
                    if (!prc.process.HasExited)
                    {
                        //if (!string.IsNullOrEmpty(prc.CommandLine) || !string.IsNullOrEmpty(prc.process.StartInfo.Arguments))
                        //{
                        return true;
                        //}
                    }
                }
                catch { }
            }
            return false;
        }

        public static List<ClsProcess> GetProcessesByPath(string processname, bool ExcludeCurrentProcess = false)
        {
            List<ClsProcess> Ret = new List<ClsProcess>();
            try
            {
                string pname = Path.GetFileNameWithoutExtension(processname);

                Process[] aProc = Process.GetProcessesByName(pname);

                Process cprc = null;
                if (ExcludeCurrentProcess)
                    cprc = Process.GetCurrentProcess();

                ProcessDetail PD = null;

                if (aProc.Length > 0)

                {
                    foreach (Process curproc in aProc)
                    {
                        if (ExcludeCurrentProcess && cprc.Id == curproc.Id)
                            continue;

                        //accessing 64 bit process from 32 bit app may not allow to get process properties, only name
                        //Stopwatch SW = Stopwatch.StartNew();
                        ClsProcess CurPrc = new ClsProcess();

                        try
                        {
                            //if (IsAdministrator())
                            //{
                            //    Process.EnterDebugMode();
                            //}
                            PD = new ProcessDetail(curproc.Id);
                            CurPrc.FileName = PD.Win32ProcessImagePath;
                            //Todo: This is not working for some reason, even with admin rights
                            //if (IsAdministrator())
                            //{
                            //    Ret.CommandLine = PD.CommandLine;  //.Replace((char)34,"");
                            //}
                        }
                        catch
                        {
                            CurPrc = null;
                        }

                        //asdf
                        //if (string.IsNullOrEmpty(Ret.CommandLine))
                        //{
                        //    //Having trouble obtaining the command line?
                        //    //Log($"Cannot get command line for '{curproc.ProcessName}', must be running as administrator.");
                        //}

                        if (Ret != null && CurPrc != null && !string.IsNullOrEmpty(CurPrc.FileName) && string.Equals(CurPrc.FileName, processname, StringComparison.OrdinalIgnoreCase))
                        {
                            CurPrc.process = curproc;
                            Ret.Add(CurPrc);
                        }
                        else
                        {
                            CurPrc = null;
                        }
                    }

                }
            }
            catch
            {
                Ret = new List<ClsProcess>();
            }

            return Ret;
        }

        public static ClsProcess GetaProcessByPath(string processname)
        {
            ClsProcess Ret = null;
            try
            {
                string pname = Path.GetFileNameWithoutExtension(processname);

                Process[] aProc = Process.GetProcessesByName(pname);

                ProcessDetail PD = null;

                if (aProc.Length > 0)

                {
                    foreach (Process curproc in aProc)
                    {
                        //accessing 64 bit process from 32 bit app may not allow to get process properties, only name
                        //Stopwatch SW = Stopwatch.StartNew();
                        Ret = new ClsProcess();

                        try
                        {
                            //if (IsAdministrator())
                            //{
                            //    Process.EnterDebugMode();
                            //}
                            PD = new ProcessDetail(curproc.Id);
                            Ret.FileName = PD.Win32ProcessImagePath;
                            //Todo: This is not working for some reason, even with admin rights
                            //if (IsAdministrator())
                            //{
                            //    Ret.CommandLine = PD.CommandLine;  //.Replace((char)34,"");
                            //}
                        }
                        catch
                        {
                            Ret = null;
                        }

                        //asdf
                        //if (string.IsNullOrEmpty(Ret.CommandLine))
                        //{
                        //    //Having trouble obtaining the command line?
                        //    //Log($"Cannot get command line for '{curproc.ProcessName}', must be running as administrator.");
                        //}

                        if (Ret != null && !string.IsNullOrEmpty(Ret.FileName) && Ret.FileName.ToLower() == processname.ToLower())
                        {
                            Ret.process = curproc;
                            break;
                        }
                        else
                        {
                            Ret = null;
                        }
                    }

                }
            }
            catch
            {
                Ret = null;
            }

            return Ret;
        }

        public static bool IsProcessRunning(string ProcessName)
        {
            bool Ret = false;

            try
            {
                Process Proc = GetaProcess(ProcessName);

                if (Proc == null)
                    Log($"Process is not running: '{ProcessName}'.");
                else
                {
                    Log($"Process IS running: '{ProcessName}'.");
                    Ret = true;
                }
            }
            catch (Exception ex)
            {
                Log("Error: " + ex.Msg());
            }

            return Ret;
        }



        public static string GetWMIPropertyFromProcess(Int32 PID, string PropName)
        {

            // THIS IS SLOW AS FUCK

            // .net PROCESS object cannot seem to get the command line from processes that we did not start
            // resort to using WMI
            //https://docs.microsoft.com/en-us/windows/win32/cimwin32prov/win32-process
            //
            //CommandLine, ExecutablePath, ProcessId
            string Ret = "";
            try
            {
                Stopwatch SW = Stopwatch.StartNew();

                //method 2 - 400ms faster:

                string wmiQuery = $"select ProcessId, CommandLine, ExecutablePath from Win32_Process where ProcessId='{Convert.ToUInt32(PID)}'";
                using (ManagementObjectSearcher search = new ManagementObjectSearcher(wmiQuery))
                {

                    // By definition, the query returns at most 1 match, because the process 
                    // is looked up by ID (which is unique by definition).
                    using (var matchEnum = search.Get().GetEnumerator())
                    {
                        if (matchEnum.MoveNext()) // Move to the 1st item.
                        {
                            object obj = matchEnum.Current[PropName];

                            if (obj == null)
                                obj = "";

                            Ret = obj.ToString();

                        }
                    }

                    //ManagementObjectCollection processList = search.Get();
                    //foreach (ManagementObject process in processList)
                    //{
                    //    //Log("{0,6} - {1} - {2}", process["ProcessId"], process["Name"], process["CommandLine"]);
                    //    object obj = process[PropName];

                    //    if (obj == null)
                    //        obj = "";

                    //    Ret = obj.ToString();

                    //    break;

                    //}
                }

                //Method 1 - 450 ms slowest
                //ManagementClass mgmtClass = new ManagementClass("Win32_Process");
                //foreach (ManagementObject process in mgmtClass.GetInstances())
                //{
                //    // Get pid
                //    UInt32 CurPID = (System.UInt32)process["ProcessId"];

                //    if (CurPID == Convert.ToUInt32(PID))
                //    {
                //        object obj = process[PropName];

                //        if (obj == null)
                //            obj = "";

                //        Ret = obj.ToString();

                //        break;
                //    }

                //}

                SW.Stop();
                Log($"Got '{PropName}' for PID '{PID}' in '{SW.ElapsedMilliseconds}'ms: {Ret}");

            }
            catch (Exception ex)
            {

                Log("Error: Could not get command line: " + ex.Msg());
            }
            return Ret;
        }

        //I had to make this class since win32 apps cant access 64 bit command line and module info
        public class ClsProcess:IEquatable<ClsProcess>
        {
            public Process process = null;
            public string FileName = "";
            public string CommandLine = "";
            public ClsProcess()
            {
                this.process = new Process();
            }

            public override bool Equals(object obj)
            {
                return Equals(obj as ClsProcess);
            }

            public bool Equals(ClsProcess other)
            {
                return other != null &&
                       string.Equals(FileName, other.FileName, StringComparison.OrdinalIgnoreCase) &&
                       string.Equals(CommandLine, other.CommandLine, StringComparison.OrdinalIgnoreCase);
            }

            public override string ToString()
            {
                return $"{Path.GetFileName(this.FileName)} {this.CommandLine}";
            }

            public static bool operator ==(ClsProcess left, ClsProcess right)
            {
                return EqualityComparer<ClsProcess>.Default.Equals(left, right);
            }

            public static bool operator !=(ClsProcess left, ClsProcess right)
            {
                return !(left == right);
            }
        }
    }

    internal static class Utility
    {
        public static string[] SplitArgs(string unsplitArgumentLine)
        {
            if (unsplitArgumentLine == null)
                return new string[0];

            int numberOfArgs;
            IntPtr ptrToSplitArgs;
            string[] splitArgs;

            ptrToSplitArgs = NativeMethods.CommandLineToArgvW(unsplitArgumentLine, out numberOfArgs);

            // CommandLineToArgvW returns NULL upon failure.
            if (ptrToSplitArgs == IntPtr.Zero)
                throw new ArgumentException("Unable to split argument.", new Win32Exception());

            // Make sure the memory ptrToSplitArgs to is freed, even upon failure.
            try
            {
                splitArgs = new string[numberOfArgs];

                // ptrToSplitArgs is an array of pointers to null terminated Unicode strings.
                // Copy each of these strings into our split argument array.
                for (int i = 0; i < numberOfArgs; i++)
                    splitArgs[i] = Marshal.PtrToStringUni(
                        Marshal.ReadIntPtr(ptrToSplitArgs, i * IntPtr.Size));

                return splitArgs;
            }
            finally
            {
                // Free memory obtained by CommandLineToArgW.
                NativeMethods.LocalFree(ptrToSplitArgs);
            }
        }

        public static T ReadUnmanagedStructFromProcess<T>(IntPtr processHandle,
                                                          IntPtr addressInProcess)
        {
            int bytesRead;
            int bytesToRead = Marshal.SizeOf(typeof(T));
            IntPtr buffer = Marshal.AllocHGlobal(bytesToRead);
            if (!NativeMethods.ReadProcessMemory(processHandle, addressInProcess, buffer, bytesToRead,
                    out bytesRead))
                throw new Win32Exception();
            T result = (T)Marshal.PtrToStructure(buffer, typeof(T));
            Marshal.FreeHGlobal(buffer);
            return result;
        }

        public static string ReadStringUniFromProcess(IntPtr processHandle,
                                                      IntPtr addressInProcess,
                                                      int NumChars)
        {
            int bytesRead;
            IntPtr outBuffer = Marshal.AllocHGlobal(NumChars * 2);

            bool bresult = NativeMethods.ReadProcessMemory(processHandle,
                                                           addressInProcess,
                                                           outBuffer,
                                                           NumChars * 2,
                                                           out bytesRead);
            if (!bresult)
                throw new Win32Exception();

            string result = Marshal.PtrToStringUni(outBuffer, bytesRead / 2);
            Marshal.FreeHGlobal(outBuffer);
            return result;
        }

        public static int UnmanagedStructSize<T>()
        {
            return Marshal.SizeOf(typeof(T));
        }
    }
    public static class LowLevelTypes
    {

        #region Constants and Enums
        // Represents the image format of a DLL or executable.
        public enum ImageFormat
        {
            NATIVE,
            MANAGED,
            UNKNOWN
        }

        // Flags used for opening a file handle (e.g. in a call to CreateFile), that determine the
        // requested permission level.
        [Flags]
        public enum FileAccessFlags:uint
        {
            GENERIC_WRITE = 0x40000000,
            GENERIC_READ = 0x80000000
        }

        // Value used for CreateFile to determine how to behave in the presence (or absence) of a
        // file with the requested name.  Used only for CreateFile.
        public enum FileCreationDisposition:uint
        {
            CREATE_NEW = 1,
            CREATE_ALWAYS = 2,
            OPEN_EXISTING = 3,
            OPEN_ALWAYS = 4,
            TRUNCATE_EXISTING = 5
        }

        // Flags that determine what level of sharing this application requests on the target file.
        // Used only for CreateFile.
        [Flags]
        public enum FileShareFlags:uint
        {
            EXCLUSIVE_ACCESS = 0x0,
            SHARE_READ = 0x1,
            SHARE_WRITE = 0x2,
            SHARE_DELETE = 0x4
        }

        // Flags that control caching and other behavior of the underlying file object.  Used only for
        // CreateFile.
        [Flags]
        public enum FileFlagsAndAttributes:uint
        {
            NORMAL = 0x80,
            OPEN_REPARSE_POINT = 0x200000,
            SEQUENTIAL_SCAN = 0x8000000,
            RANDOM_ACCESS = 0x10000000,
            NO_BUFFERING = 0x20000000,
            OVERLAPPED = 0x40000000
        }

        // The target architecture of a given executable image.  The various values correspond to the
        // magic numbers defined by the PE Executable Image File Format.
        // http://www.microsoft.com/whdc/system/platform/firmware/PECOFF.mspx
        public enum MachineType:ushort
        {
            UNKNOWN = 0x0,
            X64 = 0x8664,
            X86 = 0x14c,
            IA64 = 0x200
        }

        // A flag indicating the format of the path string that Windows returns from a call to
        // QueryFullProcessImageName().
        public enum ProcessQueryImageNameMode:uint
        {
            WIN32_FORMAT = 0,
            NATIVE_SYSTEM_FORMAT = 1
        }

        // Flags indicating the level of permission requested when opening a handle to an external
        // process.  Used by OpenProcess().
        [Flags]
        public enum ProcessAccessFlags:uint
        {
            NONE = 0x0,
            ALL = 0x001F0FFF,
            VM_OPERATION = 0x00000008,
            VM_READ = 0x00000010,
            QUERY_INFORMATION = 0x00000400,
            QUERY_LIMITED_INFORMATION = 0x00001000
        }

        // Defines return value codes used by various Win32 System APIs.
        public enum NTSTATUS:int
        {
            SUCCESS = 0,
        }

        // Determines the amount of information requested (and hence the type of structure returned)
        // by a call to NtQueryInformationProcess.
        public enum PROCESSINFOCLASS:int
        {
            PROCESS_BASIC_INFORMATION = 0
        };

        [Flags]
        public enum SHGFI:uint
        {
            Icon = 0x000000100,
            DisplayName = 0x000000200,
            TypeName = 0x000000400,
            Attributes = 0x000000800,
            IconLocation = 0x000001000,
            ExeType = 0x000002000,
            SysIconIndex = 0x000004000,
            LinkOverlay = 0x000008000,
            Selected = 0x000010000,
            Attr_Specified = 0x000020000,
            LargeIcon = 0x000000000,
            SmallIcon = 0x000000001,
            OpenIcon = 0x000000002,
            ShellIconSize = 0x000000004,
            PIDL = 0x000000008,
            UseFileAttributes = 0x000000010,
            AddOverlays = 0x000000020,
            OverlayIndex = 0x000000040,
        }
        #endregion

        #region Structures
        // In general, for all structures below which contains a pointer (represented here by IntPtr),
        // the pointers refer to memory in the address space of the process from which the original
        // structure was read.  While this seems obvious, it means we cannot provide an elegant
        // interface to the various fields in the structure due to the de-reference requiring a
        // handle to the target process.  Instead, that functionality needs to be provided at a
        // higher level.
        //
        // Additionally, since we usually explicitly define the fields that we're interested in along
        // with their respective offsets, we frequently specify the exact size of the native structure.

        // Win32 UNICODE_STRING structure.
        [StructLayout(LayoutKind.Sequential)]
        public struct UNICODE_STRING
        {
            // The length in bytes of the string pointed to by buffer, not including the null-terminator.
            private ushort length;
            // The total allocated size in memory pointed to by buffer.
            private ushort maximumLength;
            // A pointer to the buffer containing the string data.
            private IntPtr buffer;

            public ushort Length { get { return this.length; } }
            public ushort MaximumLength { get { return this.maximumLength; } }
            public IntPtr Buffer { get { return this.buffer; } }
        }

        // Win32 RTL_USER_PROCESS_PARAMETERS structure.
        [StructLayout(LayoutKind.Explicit, Size = 72)]
        public struct RTL_USER_PROCESS_PARAMETERS
        {
            [FieldOffset(56)]
            private UNICODE_STRING imagePathName;
            [FieldOffset(64)]
            private UNICODE_STRING commandLine;

            public UNICODE_STRING ImagePathName { get { return this.imagePathName; } }
            public UNICODE_STRING CommandLine { get { return this.commandLine; } }
        };

        // Win32 PEB structure.  Represents the process environment block of a process.
        [StructLayout(LayoutKind.Explicit, Size = 472)]
        public struct PEB
        {
            [FieldOffset(2), MarshalAs(UnmanagedType.U1)]
            private bool isBeingDebugged;
            [FieldOffset(12)]
            private IntPtr ldr;
            [FieldOffset(16)]
            private IntPtr processParameters;
            [FieldOffset(468)]
            private uint sessionId;

            public bool IsBeingDebugged { get { return this.isBeingDebugged; } }
            public IntPtr Ldr { get { return this.ldr; } }
            public IntPtr ProcessParameters { get { return this.processParameters; } }
            public uint SessionId { get { return this.sessionId; } }
        };

        // Win32 PROCESS_BASIC_INFORMATION.  Contains a pointer to the PEB, and various other
        // information about a process.
        [StructLayout(LayoutKind.Explicit, Size = 24)]
        public struct PROCESS_BASIC_INFORMATION
        {
            [FieldOffset(4)]
            private IntPtr pebBaseAddress;
            [FieldOffset(16)]
            private UIntPtr uniqueProcessId;

            public IntPtr PebBaseAddress { get { return this.pebBaseAddress; } }
            public UIntPtr UniqueProcessId { get { return this.uniqueProcessId; } }
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct SHFILEINFO
        {
            // C# doesn't support overriding the default constructor of value types, so we need to use
            // a dummy constructor.
            public SHFILEINFO(bool dummy)
            {
                this.hIcon = IntPtr.Zero;
                this.iIcon = 0;
                this.dwAttributes = 0;
                this.szDisplayName = "";
                this.szTypeName = "";
            }
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        };
        #endregion
    }
    public static class NativeMethods
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ReadProcessMemory(IntPtr hProcess,
                                                    IntPtr lpBaseAddress,
                                                    IntPtr lpBuffer,
                                                    int dwSize,
                                                    out int lpNumberOfBytesRead);

        [DllImport("ntdll.dll", SetLastError = true)]
        public static extern LowLevelTypes.NTSTATUS NtQueryInformationProcess(
            IntPtr hProcess,
            LowLevelTypes.PROCESSINFOCLASS pic,
            ref LowLevelTypes.PROCESS_BASIC_INFORMATION pbi,
            int cb,
            out int pSize);

        [DllImport("shell32.dll", SetLastError = true)]
        public static extern IntPtr CommandLineToArgvW(
            [MarshalAs(UnmanagedType.LPWStr)] string lpCmdLine,
            out int pNumArgs);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr LocalFree(IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr OpenProcess(
            LowLevelTypes.ProcessAccessFlags dwDesiredAccess,
            [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle,
            int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true, CallingConvention = CallingConvention.StdCall,
            CharSet = CharSet.Unicode)]
        public static extern uint QueryFullProcessImageName(
            IntPtr hProcess,
            [MarshalAs(UnmanagedType.U4)] LowLevelTypes.ProcessQueryImageNameMode flags,
            [Out] StringBuilder lpImageName, ref int size);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern SafeFileHandle CreateFile(string lpFileName,
                                                       LowLevelTypes.FileAccessFlags dwDesiredAccess,
                                                       LowLevelTypes.FileShareFlags dwShareMode,
                                                       IntPtr lpSecurityAttributes,
                                                       LowLevelTypes.FileCreationDisposition dwDisp,
                                                       LowLevelTypes.FileFlagsAndAttributes dwFlags,
                                                       IntPtr hTemplateFile);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr SHGetFileInfo(string pszPath,
                                                  uint dwFileAttributes,
                                                  ref LowLevelTypes.SHFILEINFO psfi,
                                                  uint cbFileInfo,
                                                  uint uFlags);
    }
    internal class ProcessDetail:IDisposable
    {


        public ProcessDetail(int pid)
        {
            // Initialize everything to null in case something fails.
            this.processId = pid;
            this.processHandleFlags = LowLevelTypes.ProcessAccessFlags.NONE;
            this.cachedProcessBasicInfo = null;
            this.machineTypeIsLoaded = false;
            this.machineType = LowLevelTypes.MachineType.UNKNOWN;
            this.cachedPeb = null;
            this.cachedProcessParams = null;
            this.cachedCommandLine = null;
            this.processHandle = IntPtr.Zero;

            this.OpenAndCacheProcessHandle();
        }

        // Returns the machine type (x86, x64, etc) of this process.  Uses lazy evaluation and caches
        // the result.
        public LowLevelTypes.MachineType MachineType
        {
            get
            {
                if (this.machineTypeIsLoaded)
                    return this.machineType;
                if (!this.CanQueryProcessInformation)
                    return LowLevelTypes.MachineType.UNKNOWN;

                this.CacheMachineType();
                return this.machineType;
            }
        }

        public string NativeProcessImagePath
        {
            get
            {
                if (this.nativeProcessImagePath == null)
                {
                    this.nativeProcessImagePath = this.QueryProcessImageName(
                        LowLevelTypes.ProcessQueryImageNameMode.NATIVE_SYSTEM_FORMAT);
                }
                return this.nativeProcessImagePath;
            }
        }

        public string Win32ProcessImagePath
        {
            get
            {
                if (this.win32ProcessImagePath == null)
                {
                    this.win32ProcessImagePath = this.QueryProcessImageName(
                        LowLevelTypes.ProcessQueryImageNameMode.WIN32_FORMAT);
                }
                return this.win32ProcessImagePath;
            }
        }

        //public Icon SmallIcon
        //{
        //    get
        //    {
        //        LowLevelTypes.SHFILEINFO info = new LowLevelTypes.SHFILEINFO(true);
        //        LowLevelTypes.SHGFI flags = LowLevelTypes.SHGFI.Icon
        //                                             | LowLevelTypes.SHGFI.SmallIcon
        //                                             | LowLevelTypes.SHGFI.OpenIcon
        //                                             | LowLevelTypes.SHGFI.UseFileAttributes;
        //        int cbFileInfo = Marshal.SizeOf(info);
        //        NativeMethods.SHGetFileInfo(Win32ProcessImagePath,
        //                                             256,
        //                                             ref info,
        //                                             (uint)cbFileInfo,
        //                                             (uint)flags);
        //        return Icon.FromHandle(info.hIcon);
        //    }
        //}

        // Returns the command line that this process was launched with.  Uses lazy evaluation and
        // caches the result.  Reads the command line from the PEB of the running process.
        public string CommandLine
        {
            get
            {
                if (!this.CanReadPeb)
                    return ""; //throw new InvalidOperationException();
                this.CacheProcessInformation();
                this.CachePeb();
                this.CacheProcessParams();
                this.CacheCommandLine();
                return this.cachedCommandLine;
            }
        }

        // Determines if we have permission to read the process's PEB.
        public bool CanReadPeb
        {
            get
            {
                LowLevelTypes.ProcessAccessFlags required_flags =
                    LowLevelTypes.ProcessAccessFlags.VM_READ
                  | LowLevelTypes.ProcessAccessFlags.QUERY_INFORMATION;

                // In order to read the PEB, we must have *both* of these flags.
                if ((this.processHandleFlags & required_flags) != required_flags)
                    return false;

                // If we're on a 64-bit OS, in a 32-bit process, and the target process is not 32-bit,
                // we can't read its PEB.
                if (Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess
                    && (this.MachineType != LowLevelTypes.MachineType.X86))
                    return false;

                return true;
            }
        }

        // If we can't read the process's PEB, we may still be able to get other kinds of information
        // from the process.  This flag determines if we can get lesser information.
        private bool CanQueryProcessInformation
        {
            get
            {
                LowLevelTypes.ProcessAccessFlags required_flags =
                    LowLevelTypes.ProcessAccessFlags.QUERY_LIMITED_INFORMATION
                  | LowLevelTypes.ProcessAccessFlags.QUERY_INFORMATION;

                // In order to query the process, we need *either* of these flags.
                return (this.processHandleFlags & required_flags) != LowLevelTypes.ProcessAccessFlags.NONE;
            }
        }

        private string QueryProcessImageName(LowLevelTypes.ProcessQueryImageNameMode mode)
        {
            StringBuilder moduleBuffer = new StringBuilder(1024);
            int size = moduleBuffer.Capacity;
            NativeMethods.QueryFullProcessImageName(
                this.processHandle,
                mode,
                moduleBuffer,
                ref size);
            if (mode == LowLevelTypes.ProcessQueryImageNameMode.NATIVE_SYSTEM_FORMAT)
                moduleBuffer.Insert(0, "\\\\?\\GLOBALROOT");
            return moduleBuffer.ToString();
        }

        // Loads the top-level structure of the process's information block and caches it.
        private void CacheProcessInformation()
        {
            System.Diagnostics.Debug.Assert(this.CanReadPeb);

            // Fetch the process info and set the fields.
            LowLevelTypes.PROCESS_BASIC_INFORMATION temp = new LowLevelTypes.PROCESS_BASIC_INFORMATION();
            int size;
            LowLevelTypes.NTSTATUS status = NativeMethods.NtQueryInformationProcess(
                this.processHandle,
                LowLevelTypes.PROCESSINFOCLASS.PROCESS_BASIC_INFORMATION,
                ref temp,
                Utility.UnmanagedStructSize<LowLevelTypes.PROCESS_BASIC_INFORMATION>(),
                out size);

            if (status != LowLevelTypes.NTSTATUS.SUCCESS)
            {
                //-1073741820 = STATUS_INFO_LENGTH_MISMATCH  - Probably a 64 bit process accessing 32 bit process memory related error
                throw new Win32Exception(Convert.ToInt32(status));
            }

            this.cachedProcessBasicInfo = temp;
        }

        // Follows a pointer from the PROCESS_BASIC_INFORMATION structure in the target process's
        // address space to read the PEB.
        private void CachePeb()
        {
            System.Diagnostics.Debug.Assert(this.CanReadPeb);

            if (this.cachedPeb == null)
            {
                this.cachedPeb = Utility.ReadUnmanagedStructFromProcess<LowLevelTypes.PEB>(
                    this.processHandle,
                    this.cachedProcessBasicInfo.Value.PebBaseAddress);
            }
        }

        // Follows a pointer from the PEB structure in the target process's address space to read the
        // RTL_USER_PROCESS_PARAMETERS structure.
        private void CacheProcessParams()
        {
            System.Diagnostics.Debug.Assert(this.CanReadPeb);

            if (this.cachedProcessParams == null)
            {
                this.cachedProcessParams =
                    Utility.ReadUnmanagedStructFromProcess<LowLevelTypes.RTL_USER_PROCESS_PARAMETERS>(
                        this.processHandle, this.cachedPeb.Value.ProcessParameters);
            }
        }

        private void CacheCommandLine()
        {
            System.Diagnostics.Debug.Assert(this.CanReadPeb);

            if (this.cachedCommandLine == null)
            {
                this.cachedCommandLine = Utility.ReadStringUniFromProcess(
                    this.processHandle,
                    this.cachedProcessParams.Value.CommandLine.Buffer,
                    this.cachedProcessParams.Value.CommandLine.Length / 2);
            }
        }

        private void CacheMachineType()
        {
            System.Diagnostics.Debug.Assert(this.CanQueryProcessInformation);

            // If our extension is running in a 32-bit process (which it is), then attempts to access
            // files in C:\windows\system (and a few other files) will redirect to C:\Windows\SysWOW64
            // and we will mistakenly think that the image file is a 32-bit image.  The way around this
            // is to use a native system format path, of the form:
            //    \\?\GLOBALROOT\Device\HarddiskVolume0\Windows\System\foo.dat
            // NativeProcessImagePath gives us the full process image path in the desired format.
            string path = this.NativeProcessImagePath;

            // Open the PE File as a binary file, and parse just enough information to determine the
            // machine type.
            //http://www.microsoft.com/whdc/system/platform/firmware/PECOFF.mspx
            using (SafeFileHandle safeHandle = NativeMethods.CreateFile(
                       path,
                       LowLevelTypes.FileAccessFlags.GENERIC_READ,
                       LowLevelTypes.FileShareFlags.SHARE_READ,
                       IntPtr.Zero,
                       LowLevelTypes.FileCreationDisposition.OPEN_EXISTING,
                       LowLevelTypes.FileFlagsAndAttributes.NORMAL,
                       IntPtr.Zero))
            {
                FileStream fs = new FileStream(safeHandle, FileAccess.Read);
                using (BinaryReader br = new BinaryReader(fs))
                {
                    fs.Seek(0x3c, SeekOrigin.Begin);
                    Int32 peOffset = br.ReadInt32();
                    fs.Seek(peOffset, SeekOrigin.Begin);
                    UInt32 peHead = br.ReadUInt32();
                    if (peHead != 0x00004550) // "PE\0\0", little-endian
                        throw new Exception("Can't find PE header");
                    this.machineType = (LowLevelTypes.MachineType)br.ReadUInt16();
                    this.machineTypeIsLoaded = true;
                }
            }
        }

        private void OpenAndCacheProcessHandle()
        {
            // Try to open a handle to the process with the highest level of privilege, but if we can't
            // do that then fallback to requesting access with a lower privilege level.
            this.processHandleFlags = LowLevelTypes.ProcessAccessFlags.QUERY_INFORMATION
                               | LowLevelTypes.ProcessAccessFlags.VM_READ;
            this.processHandle = NativeMethods.OpenProcess(this.processHandleFlags, false, this.processId);
            if (this.processHandle == IntPtr.Zero)
            {
                this.processHandleFlags = LowLevelTypes.ProcessAccessFlags.QUERY_LIMITED_INFORMATION;
                this.processHandle = NativeMethods.OpenProcess(this.processHandleFlags, false, this.processId);
                if (this.processHandle == IntPtr.Zero)
                {
                    this.processHandleFlags = LowLevelTypes.ProcessAccessFlags.NONE;
                    throw new Win32Exception();
                }
            }
        }

        // An open handle to the process, along with the set of access flags that the handle was
        // open with.
        private int processId;
        private IntPtr processHandle;
        private LowLevelTypes.ProcessAccessFlags processHandleFlags;
        private string nativeProcessImagePath;
        private string win32ProcessImagePath;

        // The machine type is read by parsing the PE image file of the running process, so we cache
        // its value since the operation expensive.
        private bool machineTypeIsLoaded;
        private LowLevelTypes.MachineType machineType;

        // The following fields exist ultimately so that we can access the command line.  However,
        // each field must be read separately through a pointer into another process's address
        // space so the access is expensive, hence we cache the values.
        private Nullable<LowLevelTypes.PROCESS_BASIC_INFORMATION> cachedProcessBasicInfo;
        private Nullable<LowLevelTypes.PEB> cachedPeb;
        private Nullable<LowLevelTypes.RTL_USER_PROCESS_PARAMETERS> cachedProcessParams;
        private string cachedCommandLine;

        ~ProcessDetail()
        {
            this.Dispose();
        }

        public void Dispose()
        {
            if (this.processHandle != IntPtr.Zero)
                NativeMethods.CloseHandle(this.processHandle);
            this.processHandle = IntPtr.Zero;
        }
    }



}
