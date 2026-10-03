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


        public static bool DeleteRegSetting(string Name)
        {

            //regkey is built from CompanyName\ProductName\MajorVersion.MinorVersion
            Version AN = Assembly.GetExecutingAssembly().GetName().Version;
            string Cname = System.Windows.Forms.Application.CompanyName;
            string Pname = System.Windows.Forms.Application.ProductName;
            string version = AN.Major + "." + AN.Minor;
            string SKey = "";
            bool ret = false;
            try
            {
                string RKey = $"Software\\{Cname}\\{Pname}\\{version}{SKey}";

                using RegistryKey reg = Registry.CurrentUser.OpenSubKey(RKey, false);

                if (reg != null)
                {
                    bool Found = false;
                    string[] Values = reg.GetValueNames();
                    foreach (string valu in Values)
                        if (string.Equals(valu, Name, StringComparison.OrdinalIgnoreCase))
                        {
                            Found = true;
                            break;
                        }
                    if (Found)
                    {
                        reg.DeleteValue(Name, false);
                        ret = true;
                    }
                }


            }
            catch (Exception)
            {
                //Log($"Error: {ex.Msg()}");
            }

            return ret;

        }
        public static bool DeleteRegSettings()
        {

            //regkey is built from CompanyName\ProductName\MajorVersion.MinorVersion
            Version AN = Assembly.GetExecutingAssembly().GetName().Version;
            string Cname = System.Windows.Forms.Application.CompanyName;
            string Pname = System.Windows.Forms.Application.ProductName;
            string version = AN.Major + "." + AN.Minor;
            bool ret = false;
            try
            {
                string RKey = $"Software\\{Cname}\\{Pname}\\{version}";

                Registry.CurrentUser.DeleteSubKeyTree(RKey, false);

                ret = true;

            }
            catch (Exception)
            {
                //Log($"Error: {ex.Msg()}");
            }

            return ret;

        }

        public static dynamic GetRegSetting(string Name, object DefaultValue = null, string SubKey = "")
        {

            //regkey is built from CompanyName\ProductName\MajorVersion.MinorVersion
            Version AN = Assembly.GetExecutingAssembly().GetName().Version;
            string Cname = System.Windows.Forms.Application.CompanyName;
            string Pname = System.Windows.Forms.Application.ProductName;
            string version = AN.Major + "." + AN.Minor;
            object RetVal = DefaultValue;
            string SKey = "";
            if (!string.IsNullOrWhiteSpace(SubKey))
                SKey = "\\" + SubKey.Trim();
            try
            {
                string RKey = $"Software\\{Cname}\\{Pname}\\{version}{SKey}";

                using RegistryKey reg = Registry.CurrentUser.OpenSubKey(RKey, false);
                if (reg != null)
                {
                    bool Found = false;
                    string[] Values = reg.GetValueNames();
                    foreach (string valu in Values)
                        if (string.Equals(valu, Name, StringComparison.OrdinalIgnoreCase))
                        {
                            Found = true;
                            RetVal = reg.GetValue(Name, DefaultValue);
                            break;
                        }
                    if (Found)
                    {
                        if (reg.GetValueKind(Name) == RegistryValueKind.MultiString)
                        {
                            if (DefaultValue is List<string>)
                                RetVal = ((string[])RetVal).ToList();
                            else if (DefaultValue is object[])
                                RetVal = (string[])RetVal;
                            else if (DefaultValue is string[])
                                RetVal = (string[])RetVal;
                        }
                        else if (RetVal is string && DefaultValue is Point)
                        {
                            //{X=965,Y=399}
                            int X = GetNumberInt(RetVal.ToString().GetWord("X=", ","));
                            int Y = GetNumberInt(RetVal.ToString().GetWord("Y=", "}"));
                            RetVal = new Point(X, Y);

                        }
                        else if (RetVal is string && DefaultValue is Size)
                        {
                            //{Width=931, Height=592}
                            int Wid = GetNumberInt(RetVal.ToString().GetWord("Width=", ","));
                            int Hei = GetNumberInt(RetVal.ToString().GetWord("Height=", "}"));
                            RetVal = new Size(Wid, Hei);
                        }
                        else if (RetVal is string)
                        {
                            if (RetVal.ToString().Length > 256 && IsBase64String(RetVal.ToString()))
                            {
                                string base64 = "";
                                try
                                {
                                    base64 = DeCompressFromBase64String(RetVal.ToString());
                                }
                                catch (Exception)
                                {
                                }

                                if (base64.IsEmpty())  //maybe its not really base 64 + gzip compressed?
                                    base64 = RetVal.ToString();

                                RetVal = Convert.ChangeType(base64, DefaultValue.GetType());
                            }
                            else
                            {
                                RetVal = Convert.ChangeType(RetVal, DefaultValue.GetType());
                            }
                        }
                        else if (DefaultValue != null)
                            RetVal = Convert.ChangeType(RetVal, DefaultValue.GetType());
                        //Else
                        //    RetVal = Convert.ChangeType(RetVal, DefaultValue.GetType)


                    }
                }


            }
            catch (Exception)
            {
                //Log($"Error: {ex.Msg()}");
            }

            return RetVal;

        }
        public static bool SaveRegSetting(string name, object value, string SubKey = "")
        {
            bool ret = false;
            //regkey is built from CompanyName\ProductName\MajorVersion.MinorVersion
            Version AN = Assembly.GetExecutingAssembly().GetName().Version;
            string Cname = System.Windows.Forms.Application.CompanyName;
            string Pname = System.Windows.Forms.Application.ProductName;
            string version = AN.Major + "." + AN.Minor;
            string SKey = "";
            if (!string.IsNullOrWhiteSpace(SubKey))
                SKey = "\\" + SubKey.Trim();
            try
            {
                string RKey = $"Software\\{Cname}\\{Pname}\\{version}{SKey}";
                using RegistryKey reg = Registry.CurrentUser.CreateSubKey(RKey, RegistryKeyPermissionCheck.ReadWriteSubTree);
                if (reg != null)
                {
                    if (value is List<string>)
                    {
                        List<string> strlist = (List<string>)value;
                        reg.SetValue(name, strlist.ToArray(), RegistryValueKind.MultiString);
                    }
                    else if (value is object[])
                    {
                        List<string> strlist = new List<string>();
                        object[] objects = (object[])value;
                        foreach (object obj in objects)
                            strlist.Add(obj.ToString());
                        reg.SetValue(name, strlist.ToArray(), RegistryValueKind.MultiString);
                    }
                    else if (value is string)
                    {
                        //large strings may cause, so compress and base 64 encode
                        //Insufficient system resources exist to complete the requested service.; [IOException]
                        string compressed = value.ToString();

                        if (compressed.Length > 2048)
                            compressed = CompressToBase64String(compressed);

                        reg.SetValue(name, compressed);

                    }
                    else
                    {
                        reg.SetValue(name, value);

                    }
                    ret = true;
                }


            }
            catch (Exception ex)
            {
                Log($"Error: ({name}={value.ToString().Length} bytes) {ex.Msg()}");
            }
            finally
            {

            }

            return ret;
        }

        public static void Startup(bool Enable)
        {
            try
            {

                using (RegistryKey RK = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true))
                {

                    string AppName = Path.GetFileNameWithoutExtension(Application.ExecutablePath);
                    string AppCmd = Application.ExecutablePath + " /min";
                    bool Enabled = false;
                    object CurVal = RK.GetValue(AppName, null);

                    if (CurVal == null || string.IsNullOrWhiteSpace(CurVal.ToString()))
                    {
                        Log("Application is NOT set to start with Windows: HKCU\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run");

                        Enabled = false;
                    }
                    else
                    {
                        if (string.Equals(CurVal.ToString(), AppCmd, StringComparison.OrdinalIgnoreCase))
                        {
                            Log("Application is already set to start with Windows: HKCU\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run");
                            Enabled = true;
                        }
                        else
                        {
                            Log($"Application is NOT set to start with Windows (bad path={CurVal}): HKCU\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run");
                        }

                    }

                    if (Enable && !Enabled)
                    {
                        Log("Enabling Application startup: " + AppCmd);
                        if (!Debugger.IsAttached)
                        {
                            RK.SetValue(AppName, AppCmd);
                        }
                    }
                    else if (!Enable && Enabled)
                    {
                        Log("Disabling Application startup.");
                        if (!Debugger.IsAttached)
                        {
                            RK.DeleteValue(AppName);
                        }
                    }

                }

            }
            catch (Exception ex)
            {
                Log(ex.Message);
            }
            finally
            {
            }
        }
    }
}
