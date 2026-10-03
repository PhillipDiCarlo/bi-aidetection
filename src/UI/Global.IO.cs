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

        public static async Task<bool> SafeFileDeleteAsync(string Filename, string Source, long MaxWaitMS = 30000, bool Quiet = false)
        {
            //run the function in another thread
            return await Task.Run(() => SafeFileDelete(Filename, Source, MaxWaitMS, Quiet));
        }
        public static bool SafeFileDelete(string Filename, string Source, long MaxWaitMS = 30000, bool Quiet = false)
        {
            bool ret = false;

            if (!File.Exists(Filename))
                return true;

            WaitFileAccessResult result = Global.WaitForFileAccess(Filename, FileAccess.ReadWrite, FileShare.None, MaxWaitMS, AppSettings.Settings.loop_delay_ms, true, 0);
            if (result.Success)
            {
                try
                {
                    File.Delete(Filename);
                    ret = true;
                }
                catch (Exception ex)
                {
                    if (!Quiet)
                        Log($"Error: Could not delete file after {result.TimeMS} ms. Source='{Source}': {ex.Msg()}");
                }
            }
            else
            {
                if (!Quiet)
                    Log($"Error: Could not delete file after {result.TimeMS} ms. Source='{Source}': {Filename}");
            }

            return ret;

        }

        public static string GetTempFolder(bool Clear = false)
        {
            string ret = Path.GetTempPath();

            try
            {
                ret = Path.Combine(Path.GetTempPath(), "_AITOOL");

                if (Clear && Directory.Exists(ret))
                    try { Directory.Delete(ret, true); } catch { }

                if (!Directory.Exists(ret))
                    Directory.CreateDirectory(ret);
            }
            catch (Exception ex)
            {
                Log("Error: Could not get temp folder? " + ex.Msg());
            }

            return ret;
        }

        public static string SafeLoadTextFile(string Filename)
        {

            string ret = "";

            //dont wait very long for access, only grab text if the file is not being used (for stderr.txt, etc)
            WaitFileAccessResult result = WaitForFileAccess(Filename, FileAccess.Read, FileShare.Read, 100, AppSettings.Settings.loop_delay_ms, true, 4, MaxErrRetryCnt: 4);

            try
            {
                if (result.Success)
                {
                    // Open a FileStream object using the passed in safe file handle.
                    using FileStream fileStream = new FileStream(result.Handle, FileAccess.Read);
                    using StreamReader sr = new StreamReader(fileStream, Encoding.UTF8);
                    ret = sr.ReadToEnd();
                }
            }
            catch { }
            finally
            {
                if (result.Handle != null)
                {
                    if (!result.Handle.IsClosed)
                        result.Handle.Close();

                    result.Handle.Dispose();
                }
            }

            return ret;

        }

        public static async Task<bool> DirectoryExistsAsync(string directory, int TimeoutMS = 20000)
        {
            //run the function in another thread
            CancellationTokenSource cts = new CancellationTokenSource(TimeoutMS);
            try
            {
                Stopwatch sw = Stopwatch.StartNew();
                bool result = await Task.Run(() => Directory.Exists(directory), cts.Token);
                Log($"Trace: Directory exists for '{directory}' took {sw.ElapsedMilliseconds}ms");
                return result;
            }
            catch (Exception ex)
            {

                return false;
            }
        }

        public static string MappedDriveToUNCPath(string path)
        {
            if (!path.StartsWith(@"\\"))
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey("Network\\" + path[0]))
                {
                    if (key != null)
                    {
                        return key.GetValue("RemotePath").ToString() + path.Remove(0, 2).ToString();
                    }
                }
            }
            return path;
        }

        public class MappedDrive
        {
            public string DriveLetter = "";
            public string Path = "";
        }

        public static async Task<string> GetBestRemotePathAsync(string RemoteLocalPath, string RemoteMachineNameOrIP)
        {
            //We are taking a path like C:\BlueIris\clips\blah read from a remote computer's registry
            //and trying to convert it to an accessible path on the current computer.
            string ret = RemoteLocalPath;
            string lastremotepathpart = RemoteLocalPath.SplitStr(@"\").Last();
            string ip = "";
            string hostname = "";
            Stopwatch sw = Stopwatch.StartNew();

            if (!RemoteLocalPath.StartsWith(@"\\"))
            {
                //Make sure we have the IP address
                IPAddress ipa = await GetIPAddressFromHostnameAsync(RemoteMachineNameOrIP);
                ip = Global.IP2Str(ipa, IPType.Path);
                hostname = await GetHostNameAsync(RemoteMachineNameOrIP);

                //first look for a mapped drive letter:
                string letter = RemoteLocalPath.Substring(0, 1);
                List<MappedDrive> mapped = new List<MappedDrive>();
                using RegistryKey key = Registry.CurrentUser.OpenSubKey("Network");
                if (key != null)
                {
                    List<string> drives = key.GetSubKeyNames().ToList();
                    foreach (string drv in drives)
                    {
                        using RegistryKey drvkey = key.OpenSubKey(drv);
                        if (drvkey != null)
                        {
                            string remotepath = drvkey.GetValue("RemotePath", "").ToString();
                            if (!string.IsNullOrEmpty(remotepath) && remotepath.StartsWith(@"\\"))
                            {
                                string mappedserver = remotepath.GetWord(@"\\", @"\");

                                if (mappedserver.EqualsIgnoreCase(ip) ||
                                    mappedserver.EqualsIgnoreCase(hostname) ||
                                    mappedserver.EqualsIgnoreCase(hostname.GetWord("", ".")))  //lop off the SERVER.DOMAIN
                                {
                                    MappedDrive md = new MappedDrive();
                                    md.DriveLetter = drv;
                                    md.Path = remotepath;
                                    mapped.Add(md);
                                }
                            }
                        }
                    }
                }

                //first pass, try to get any matches where some of the UNC path matches...
                //there would have to be a shared part of the path
                //            C:\clips\myclippath
                //\\server\share\clips\myclippath
                foreach (MappedDrive md in mapped)
                {
                    string sharedpath = GetSharedPath(md.Path, RemoteLocalPath, false).TrimEnd(@"\".ToCharArray());
                    if (!string.IsNullOrEmpty(sharedpath))
                    {
                        Log($"Debug: Found shared path in {sw.ElapsedMilliseconds}ms on '{RemoteMachineNameOrIP}' for path '{RemoteLocalPath}': {sharedpath}");
                        return sharedpath;
                    }
                }

                List<string> spltpth = RemoteLocalPath.SplitStr("\\");

                //search for last two parts of the path UNDER each of the shares
                string lastpath = spltpth[spltpth.Count - 1];
                string nexttolast = "";

                if (spltpth.Count - 2 > 0)
                {
                    nexttolast = spltpth[spltpth.Count - 2];
                    string searchpath = $"{nexttolast}\\{lastpath}";
                    foreach (MappedDrive md in mapped)
                    {
                        string checkpath = Path.Combine(md.Path, searchpath);
                        if (await Global.DirectoryExistsAsync(checkpath))
                        {
                            Log($"Debug: Found remote path in {sw.ElapsedMilliseconds}ms on '{RemoteMachineNameOrIP}' for path '{RemoteLocalPath}': {checkpath}");
                            return checkpath;
                        }
                    }
                }


                //            C:\clips\myclippath
                //\\server\share\clips\myclippath

                //C:\BlueIrisStorage\Alerts
                //\\server\BlueIrisStorage

                foreach (MappedDrive md in mapped)
                {
                    string checkpath = Path.Combine(md.Path, lastpath);
                    if (await Global.DirectoryExistsAsync(checkpath))
                    {
                        Log($"Debug: Found remote path in {sw.ElapsedMilliseconds}ms on '{RemoteMachineNameOrIP}' for path '{RemoteLocalPath}': {checkpath}");
                        return checkpath;
                    }
                }


                ret = $"\\\\{Global.IP2Str(RemoteMachineNameOrIP, IPType.Path)}\\{RemoteLocalPath.Replace(":", "$")}";
                //resort to using admin shares (have to be enabled through group policy in newer versions of windows)
                Log($"Debug: Found ADMIN share in {sw.ElapsedMilliseconds}ms '{RemoteMachineNameOrIP}' for path '{RemoteLocalPath}': {ret}");

            }
            return ret;
        }

        public static string GetSharedPath(string BasePath, string SrcPath, bool OnlyPartial)
        {
            // Dim NetRootPth As String = "\\server\admlibrary\Software\AutoDesk\Test_CAD_State_Kit"
            // Dim SrcPth As String     = "                              C:\Test\Test_CAD_State_Kit\C3D 2021\Utilities"
            // Output=                     \\server\admlibrary\Software\AutoDesk\Test_CAD_State_Kit\C3D 2021\Utilities
            string Ret = "";
            try
            {
                // Dim com As String = FindCommonPath(pths)
                string[] nps = BasePath.Trim().Split('\\');
                string SharedPth = "";
                bool Fnd = false;
                foreach (string pp in nps)
                {
                    if (!OnlyPartial)
                    {
                        if (pp == "")
                            SharedPth = SharedPth + @"\";
                        else
                            SharedPth = SharedPth + pp + @"\";
                    }

                    string[] rps = SrcPath.Trim().Split('\\');
                    foreach (string rp in rps)
                    {
                        if (Fnd)
                        {
                            if (rp == "")
                                SharedPth = SharedPth + @"\";
                            else
                                SharedPth = SharedPth + rp + @"\";
                        }
                        else if (pp != "" && string.Equals(pp, rp, StringComparison.OrdinalIgnoreCase))
                        {
                            Fnd = true;
                            if (OnlyPartial)
                                SharedPth = SharedPth + rp + @"\";
                        }
                    }
                    if (Fnd)
                        break;
                }
                if (Fnd)
                    Ret = SharedPth;
            }
            catch (Exception ex)
            {
                Ret = "";
                Log("Error: " + ex.Message);
            }
            return Ret;
        }

        public static bool DirectoryCopy(string sourceDirName, string destDirName, bool copySubDirs, bool nolog)
        {

            bool ret = true;
            try
            {
                // Get the subdirectories for the specified directory.
                DirectoryInfo dir = new DirectoryInfo(sourceDirName);

                if (!dir.Exists)
                {
                    ret = false;
                    if (!nolog) Log($"Error: Source folder does not exist? {sourceDirName}");
                    return ret;
                }

                DirectoryInfo[] dirs = dir.GetDirectories();

                // If the destination directory doesn't exist, create it.       
                Directory.CreateDirectory(destDirName);

                // Get the files in the directory and copy them to the new location.
                FileInfo[] files = dir.GetFiles();
                foreach (FileInfo file in files)
                {
                    string tempPath = Path.Combine(destDirName, file.Name);
                    try
                    {
                        file.CopyTo(tempPath, false);
                    }
                    catch (Exception ex)
                    {
                        ret = false;
                        if (!nolog) Log($"Error: {tempPath} - {ex.Message}");
                    }
                }

                // If copying subdirectories, copy them and their contents to new location.
                if (copySubDirs)
                {
                    foreach (DirectoryInfo subdir in dirs)
                    {
                        string tempPath = Path.Combine(destDirName, subdir.Name);
                        if (!DirectoryCopy(subdir.FullName, tempPath, copySubDirs, nolog))
                            ret = false;
                    }
                }

            }
            catch (Exception ex)
            {
                ret = false;
                if (!nolog) Log($"Error: {ex.Message}");
            }

            return ret;
        }


        public static void MoveFiles(string FromFolder, string ToFolder, string FileSpec, bool OnlyIfNewer, bool OnlyCopy = false)
        {
            //Let us pass a filename so we can be lazy
            if (Path.HasExtension(FromFolder))
                FromFolder = Path.GetDirectoryName(FromFolder);

            if (Path.HasExtension(ToFolder))
                ToFolder = Path.GetDirectoryName(ToFolder);

            List<FileInfo> files = GetFiles(FromFolder, FileSpec, SearchOption.TopDirectoryOnly);

            int cnt = 0;

            if (files.Count > 0)
            {
                if (!Directory.Exists(ToFolder))
                    Directory.CreateDirectory(ToFolder);

            }

            foreach (FileInfo fi in files)
            {
                string newfile = Path.Combine(ToFolder, fi.Name);
                try
                {
                    bool move = true;
                    FileInfo nfi = new FileInfo(newfile);
                    if (nfi.Exists)
                    {
                        if (fi.LastWriteTime < nfi.LastWriteTime)
                        {
                            //just delete the older file rather than moving it
                            move = false;
                            if (!OnlyCopy)
                                fi.Delete();
                        }
                    }

                    if (move)
                    {
                        if (!OnlyCopy)
                            fi.MoveTo(newfile);
                        else
                            fi.CopyTo(newfile, true);

                    }

                    cnt++;
                }
                catch (Exception ex)
                {

                    Log($"Error: Could not move {fi.FullName} to {newfile}: {ex.Msg()}");
                }
            }

            Log($"Debug: Moved {cnt} '{FileSpec}' files from {FromFolder} to {ToFolder}.");

        }

        //[DllImport("ntdll.dll")]
        //public static extern int RtlNtStatusToDosError(int status);

        /// <summary>
        /// Flags used by <see cref="WinError.FormatMessage"/> method.
        /// </summary>
        [Flags]
        public enum FormatMessageFlags:uint
        {
            /// <summary>
            /// The function allocates a buffer large enough to hold the formatted message, and places a pointer to the
            /// allocated buffer at the address specified by <c>lpBuffer</c>. The <c>lpBuffer</c> parameter is a pointer
            /// to an <c>LPTSTR</c>. The <c>nSize</c> parameter specifies the minimum number of <c>TCHARs</c> to allocate
            /// for an output message buffer. The caller should use the <c>LocalFree</c> function to free the buffer when
            /// it is no longer needed.
            /// If the length of the formatted message exceeds 128K bytes, then <c>FormatMessage</c> will fail and a
            /// subsequent call to <c>GetLastError</c> will return <c>ERROR_MORE_DATA</c>.
            /// This value is not available for use when compiling Windows Store apps.
            /// </summary>
            FORMAT_MESSAGE_ALLOCATE_BUFFER = 0x00000100,

            /// <summary>
            /// Insert sequences in the message definition are to be ignored and passed through to the output buffer
            /// unchanged. This flag is useful for fetching a message for later formatting. If this flag is set, the
            /// <c>Arguments</c> parameter is ignored.
            /// </summary>
            FORMAT_MESSAGE_IGNORE_INSERTS = 0x00000200,

            /// <summary>
            /// The <c>lpSource</c> parameter is a pointer to a null-terminated string that contains a message definition.
            /// The message definition may contain insert sequences, just as the message text in a message table resource
            /// may. This flag cannot be used with <see cref="FORMAT_MESSAGE_FROM_HMODULE"/> or
            /// <see cref="FORMAT_MESSAGE_FROM_SYSTEM"/>.
            /// </summary>
            FORMAT_MESSAGE_FROM_STRING = 0x00000400,

            /// <summary>
            /// The <c>lpSource</c> parameter is a module handle containing the message-table resource(s) to search. If
            /// this <c>lpSource</c> handle is <c>null</c>, the current process's application image file will be searched.
            /// This flag cannot be used with <see cref="FORMAT_MESSAGE_FROM_STRING"/>.
            /// If the module has no message table resource, the function fails with <c>ERROR_RESOURCE_TYPE_NOT_FOUND</c>.
            /// </summary>
            FORMAT_MESSAGE_FROM_HMODULE = 0x00000800,

            /// <summary>
            /// The function should search the system message-table resource(s) for the requested message. If this flag is
            /// specified with <see cref="FORMAT_MESSAGE_FROM_HMODULE"/>, the function searches the system message table
            /// if the message is not found in the module specified by <c>lpSource</c>. This flag cannot be used with
            /// <see cref="FORMAT_MESSAGE_FROM_STRING"/>.
            /// If this flag is specified, an application can pass the result of the <c>GetLastError</c> function to
            /// retrieve the message text for a system-defined error.
            /// </summary>
            FORMAT_MESSAGE_FROM_SYSTEM = 0x00001000,

            /// <summary>
            /// The Arguments parameter is not a <c>va_list</c> structure, but is a pointer to an array of values that
            /// represent the arguments. This flag cannot be used with 64-bit integer values. If you are using a 64-bit
            /// integer, you must use the <c>va_list</c> structure.
            /// </summary>
            FORMAT_MESSAGE_ARGUMENT_ARRAY = 0x00002000
        }
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern uint FormatMessage(
                FormatMessageFlags dwFlags,
                IntPtr lpSource,
                uint dwMessageId,
                uint dwLanguageId,
                StringBuilder lpBuffer,
                uint nSize,
                IntPtr arguments);
        //[DllImport("Kernel32.dll", SetLastError = true)]
        //static extern uint FormatMessage(uint dwFlags, IntPtr lpSource, uint dwMessageId, uint dwLanguageId, ref IntPtr lpBuffer, uint nSize, IntPtr pArguments);
        //[DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        //static extern uint FormatMessage(uint dwFlags, IntPtr lpSource, uint dwMessageId, uint dwLanguageId, StringBuilder lpBuffer, uint nSize, IntPtr Arguments);
        public static string FormatMessageFromHRESULT(int errorcode)
        {
            const int nCapacity = 1024; // max error length
                                        //const uint FORMAT_MSG_FROM_SYS = 0x01000;

            //const uint FORMAT_MESSAGE_ALLOCATE_BUFFER = 0x00000100;
            //const uint FORMAT_MESSAGE_IGNORE_INSERTS = 0x00000200;
            //const uint FORMAT_MESSAGE_FROM_SYSTEM = 0x00001000;
            //const uint FORMAT_MESSAGE_ARGUMENT_ARRAY = 0x00002000;
            //const uint FORMAT_MESSAGE_FROM_HMODULE = 0x00000800;
            //const uint FORMAT_MESSAGE_FROM_STRING = 0x00000400;
            //const int HresultWin32Prefix = unchecked((int)0x80070000);
            StringBuilder defSb = new StringBuilder(nCapacity);

            const FormatMessageFlags Flags = FormatMessageFlags.FORMAT_MESSAGE_FROM_SYSTEM
                | FormatMessageFlags.FORMAT_MESSAGE_IGNORE_INSERTS
                | FormatMessageFlags.FORMAT_MESSAGE_ARGUMENT_ARRAY;

            var buffer = new StringBuilder(nCapacity);
            uint result = FormatMessage(Flags, IntPtr.Zero, (uint)errorcode, 0, buffer, nCapacity, IntPtr.Zero);
            string ret = "";
            if (result != 0)
            {
                ret = buffer.ToString().Trim();
            }
            return ret;
            //uint dwChars = FormatMessage(
            //    FORMAT_MESSAGE_ALLOCATE_BUFFER | FORMAT_MESSAGE_FROM_SYSTEM | FORMAT_MESSAGE_IGNORE_INSERTS | FORMAT_MESSAGE_FROM_HMODULE,
            //    IntPtr.Zero,
            //    (uint)hresult,
            //    0, // Default language
            //    ref lpMsgBuf,
            //    0,
            //    IntPtr.Zero);
            //must specify the FORMAT_MESSAGE_ARGUMENT_ARRAY flag when pass an array
            //uint length = FormatMessage(FORMAT_MESSAGE_ALLOCATE_BUFFER | FORMAT_MESSAGE_FROM_SYSTEM, IntPtr.Zero, (uint)code, 0, defSb, nCapacity, IntPtr.Zero);



            //string sRet = "(unknown)";
            //if (dwChars > 0)
            //{
            //    sRet = Marshal.PtrToStringAnsi(lpMsgBuf).TrimEnd(' ', '.', '\r', '\n');
            //}

            //string sDefMsg = defSb.ToString().TrimEnd(' ', '.', '\r', '\n');
            ////nothing left to do:
            //return sDefMsg;
        }

        //[DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        //private extern static SafeFileHandle CreateFile(
        //    string lpFileName,
        //    FileSystemRights dwDesiredAccess,
        //    FileShare dwShareMode,
        //    IntPtr securityAttrs,
        //    FileMode dwCreationDisposition,
        //    FileOptions dwFlagsAndAttributes,
        //    IntPtr hTemplateFile);


        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private extern static SafeFileHandle CreateFile(
            string lpFileName,
            [MarshalAs(UnmanagedType.U4)] FileAccess dwDesiredAccess,
            [MarshalAs(UnmanagedType.U4)] FileShare dwShareMode,
            IntPtr lpSecurityAttributes,
            [MarshalAs(UnmanagedType.U4)] FileMode dwCreationDisposition,
            [MarshalAs(UnmanagedType.U4)] FileAttributes dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        private const int ERROR_ACCESS_DENIED = 5;
        private const int ERROR_SHARING_VIOLATION = 32;
        private const int ERROR_LOCK_VIOLATION = 33;
        public static async Task<WaitFileAccessResult> WaitForFileAccessAsync(string filename, FileAccess rights = FileAccess.Read, FileShare share = FileShare.Read, long WaitMS = 30000, int RetryDelayMS = 0)
        {
            //run the function in another thread
            return await Task.Run(() => WaitForFileAccess(filename, rights, share, WaitMS, RetryDelayMS));
        }

        public class WaitFileAccessResult
        {
            public bool Success = false;
            public long TimeMS = 0;
            public int ErrRetryCnt = 0;
            public string ResultString = "";
            public SafeFileHandle Handle = default(SafeFileHandle);

        }

        public static WaitFileAccessResult WaitForFileAccess(string filename,
                                                              FileAccess rights = FileAccess.Read,
                                                              FileShare share = FileShare.None,
                                                              long MaxWaitMS = 30000,
                                                              int RetryDelayMS = 0,
                                                              bool ReturnHandle = false,
                                                              long MinFileSize = 1,
                                                              int MaxErrRetryCnt = 2000)
        {

            //using var Trace = new Trace();  //This c# 8.0 using feature will auto dispose when the function is done.

            WaitFileAccessResult ret = new WaitFileAccessResult();
            string LastFailReason = "";
            Stopwatch SW = Stopwatch.StartNew();

            if (RetryDelayMS == 0)
                RetryDelayMS = AppSettings.Settings.loop_delay_ms;

            try
            {
                //lets give it an initial tiny wait
                //await Task.Delay(RetryDelayMS);

                FileInfo FI = new FileInfo(filename);

                bool NeedsToWrite = rights == FileAccess.ReadWrite || rights == FileAccess.Write;

                if (FI.Exists)
                {

                    if (NeedsToWrite && FI.IsReadOnly)
                    {
                        ret.Success = false;
                        ret.ResultString = "(ReadOnly)";
                        return ret;
                    }

                    long LastLength = FI.Length;  //if the file is growing, try to wait for it

                    while ((ret.ErrRetryCnt <= MaxErrRetryCnt) && (SW.ElapsedMilliseconds <= MaxWaitMS))
                    {
                        if (FI.Length >= MinFileSize && LastLength == FI.Length)
                        {

                            //SafeFileHandle fileHandle = CreateFile(fileName,FileSystemRights.Modify, FileShare.Write,IntPtr.Zero, FileMode.OpenOrCreate,FileOptions.None, IntPtr.Zero);
                            ret.Handle = CreateFile(filename, rights, share, IntPtr.Zero, FileMode.Open, FileAttributes.Normal, IntPtr.Zero);

                            if (ret.Handle.IsInvalid)
                            {
                                int LastErr = Marshal.GetLastWin32Error();

                                if (LastErr == ERROR_SHARING_VIOLATION)
                                    LastFailReason = "(SharingViolation)";
                                else if (LastErr == ERROR_LOCK_VIOLATION)
                                    LastFailReason = "(LockViolation)";



                                if (LastErr != ERROR_SHARING_VIOLATION && LastErr != ERROR_LOCK_VIOLATION)
                                {
                                    LastFailReason = $"(Unexpected-{LastErr})";
                                    //unexpected error, break out
                                    Log($"Error: Unexpected Win32Error waiting for access to {filename}: {LastErr}: {new Win32Exception(LastErr)}");
                                    break;
                                }

                            }
                            else
                            {
                                //passed the first test
                                if (NeedsToWrite)
                                {
                                    //make sure we can read a byte and write the same byte back to verify access
                                    if (IsFileWritable(filename, ref ret.Handle, ref FI, out LastFailReason))
                                    {
                                        ret.Success = true;
                                        break;
                                    }
                                }
                                else
                                {
                                    ret.Success = true;
                                    break;
                                }
                            }

                            if (!ret.Handle.IsClosed)
                            {
                                ret.Handle.Close();
                                ret.Handle.Dispose();
                            }

                        }

                        LastLength = FI.Length;

                        ret.ErrRetryCnt += 1;

                        Thread.Sleep(RetryDelayMS);

                        FI.Refresh();
                    }
                    SW.Stop();

                    ret.TimeMS = SW.ElapsedMilliseconds;

                    if (!ret.Success)
                    {
                        ret.ResultString = $"Debug: LastFail={LastFailReason}, lock time: {ret.TimeMS}ms (max={MaxWaitMS}), {ret.ErrRetryCnt} retries (max={MaxErrRetryCnt}) with a {RetryDelayMS}ms retry delay: {Path.GetFileName(filename)}";
                        if (ret.ErrRetryCnt > 2 || ret.TimeMS > 1000)
                        {
                            Log(ret.ResultString);
                        }
                    }

                }
                else
                {
                    ret.ResultString = $"Error: File not found: " + filename;
                    Log(ret.ResultString);
                }

            }
            catch (Exception ex)
            {
                ret.TimeMS = SW.ElapsedMilliseconds;
                ret.ResultString = $"Error: {filename}: {ex.Msg()}";
                Log(ret.ResultString);
            }
            finally
            {
                if (!ReturnHandle && ret.Handle != null && !ret.Handle.IsClosed)
                {
                    ret.Handle.Close();
                    ret.Handle.Dispose();
                }
            }


            return ret;

        }

        public static bool IsFileWritable(string filename, ref SafeFileHandle handle, ref FileInfo FI, out string FailReason)
        {
            bool Ret = false;
            string err = "";
            FailReason = "";

            byte[] TestReadByte;
            try
            {
                if (FI == null)
                    FI = new FileInfo(filename);

                if (FI.IsReadOnly)
                {
                    FailReason = "(ReadOnly)";
                    return false;
                }

                bool Updated = false;

                using (FileStream fs = new FileStream(handle, FileAccess.ReadWrite))
                {

                    using (BinaryReader br = new BinaryReader(fs))
                    {

                        if (br.BaseStream.CanRead)
                        {
                            //read 1 byte
                            TestReadByte = br.ReadBytes(1);
                            fs.Position = 0;
                            using (BinaryWriter bw = new BinaryWriter(fs))
                            {

                                if (bw.BaseStream.CanWrite)
                                {
                                    //write same byte back - get exception if file is in use
                                    bw.Write(TestReadByte);
                                    bw.Flush();
                                    Updated = true;
                                }
                                else
                                {
                                    FailReason = "(CantWrite)";
                                }
                            }
                        }
                        else
                        {
                            FailReason = "(CantRead)";
                        }
                    }
                }

                if (Updated)
                {
                    //reset dates on the file back to what they were before the test write
                    FileInfo DFI = new FileInfo(filename);
                    if (DFI.CreationTime != FI.CreationTime)
                        DFI.CreationTime = FI.CreationTime;  //reset date the same as the old file
                    if (DFI.LastWriteTime != FI.LastWriteTime)
                        DFI.LastWriteTime = FI.LastWriteTime;
                }

                if (String.IsNullOrWhiteSpace(FailReason))
                    Ret = true;

            }
            catch (Exception ex)
            {
                err = "(" + ex.Message + ")";
            }
            FailReason = err;
            return Ret;
        }



        public static bool IsDirWritable(string FolderName, bool CreateIfNotExist)
        {
            bool Ret = false;
            try
            {
                if (FolderName.IsEmpty())
                    return Ret;

                // if actually passed a filename, get folder
                string pth = FolderName;
                //if (PathEndsWithFilename(FolderName))
                //{
                //    pth = Path.GetDirectoryName(pth);
                //}

                if (!Directory.Exists(pth))
                {
                    if (CreateIfNotExist)
                    {
                        Directory.CreateDirectory(pth);
                        Ret = true;
                        return Ret;
                    }
                    else
                    {
                        return Ret;
                    }
                }

                string Filename = Path.Combine(pth, Path.GetRandomFileName());
                using (var fs = File.Create(Filename, 1, FileOptions.DeleteOnClose))
                {
                }
                // make sure temp file really did get deleted...
                if (File.Exists(Filename))
                {
                    Log("Trace: Failed to remove temp file? " + Filename);
                    File.Delete(Filename);
                }

                Ret = true;
            }
            catch
            {
            }

            return Ret;
        }


        public static DateTime GetTimeFromFileName(string FileName)
        {
            DateTime OutDate = DateTime.MinValue;

            try
            {
                if (string.IsNullOrWhiteSpace(FileName))
                {
                    return OutDate;
                }
                string Fil = Path.GetFileNameWithoutExtension(FileName);
                string StrDate = "";
                MatchCollection Matches = RegEx_ValidDate.Matches(Fil);
                if (Matches != null && Matches.Count > 0)
                {
                    StrDate = Matches[0].Value;
                    StrDate = StrDate.Replace("_", ":");
                    //pos 10=T
                    StrDate = StrDate.Remove(10, 1).Insert(10, "T");
                    if (!GetDateStrict(StrDate, ref OutDate))
                    {
                        Log("Error: There was a problem parsing '" + FileName + "' for a date.");
                        OutDate = DateTime.MinValue;
                    }
                }

            }
            catch (Exception ex)
            {
                Log("Error: " + ex.Msg());
            }

            return OutDate;

        }

        public static List<System.IO.FileInfo> GetFiles(string CurDirectory, string FileName = "*", System.IO.SearchOption SearchOptions = System.IO.SearchOption.AllDirectories, DateTime? MinLastWriteTime = null, DateTime? MaxLastWriteTime = null, int MaxFiles = 99999)
        {

            List<System.IO.FileInfo> files = new List<System.IO.FileInfo>();
            try
            {
                //If Directory.Exists(CurDirectory) Then
                List<string> Folders = CurDirectory.SplitStr(";|");
                List<string> Names = FileName.SplitStr(";|");
                bool HasDate = MinLastWriteTime.HasValue;
                foreach (string fld in Folders)
                {
                    string fldr = fld;

                    //so we can be lazy and pass filenames...
                    if (Path.HasExtension(fldr))
                        fldr = Path.GetDirectoryName(fldr);

                    if (Directory.Exists(fldr))
                    {
                        DirectoryInfo DirInfo = new DirectoryInfo(fldr);
                        foreach (string nam in Names)
                        {
                            //M.DbgLog($"Getting '{nam}' files from folder '{fldr}'...");
                            try
                            {
                                foreach (FileInfo fi in DirInfo.EnumerateFiles(nam, SearchOptions))
                                {
                                    if (HasDate)
                                    {
                                        if (MaxLastWriteTime.HasValue)
                                        {
                                            if (fi.LastWriteTime >= MinLastWriteTime.Value && fi.LastWriteTime <= MaxLastWriteTime)
                                            {
                                                if (files.Count < MaxFiles)
                                                    files.Add(fi);
                                                else
                                                    break;
                                            }

                                        }
                                        else
                                        {
                                            if (fi.LastWriteTime == MinLastWriteTime.Value)
                                            {
                                                if (files.Count < MaxFiles)
                                                    files.Add(fi);
                                                else
                                                    break;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        if (files.Count < MaxFiles)
                                            files.Add(fi);
                                        else
                                            break;
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Log("Error: While getting file list, received error: " + ex.Msg());
                            }
                        }

                    }
                    else
                    {
                        Log($"Debug: Directory doesn't exist: '{fldr}' - ({FileName})");
                    }
                }
                //files.AddRange(IO.Directory.GetFiles(CurDirectory, nam, SearchOptions).Select(Function(p) New IO.FileInfo(p)).ToList)
                //M.DbgLog("Found " & files.Count & " " & FileName & " files in " & CurDirectory)
                //Else
                //M.DbgLog("Error: Folder does not exist: " & CurDirectory)
                //End If

            }
            catch (Exception ex)
            {
                Log("Error: While getting file list, received error: " + ex.Msg());
            }
            finally
            {
            }

            return files;

        }
    }
}
