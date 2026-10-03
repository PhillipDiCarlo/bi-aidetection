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
        [DllImport("Shlwapi.dll", CharSet = CharSet.Auto)]
        public static extern long StrFormatByteSize(long fileSize, [MarshalAs(UnmanagedType.LPTStr)] StringBuilder buffer, int bufferSize);

        public static string FormatBytes(long filesize)
        {
            StringBuilder sb = new StringBuilder();
            StrFormatByteSize(filesize, sb, sb.Capacity);
            return sb.ToString();
        }
        public static bool IsLatLongValid(string lat, string lng)
        {
            return IsLatLongValid(lat.ToDouble(), lng.ToDouble());
        }
        public static bool IsLatLongValid(double lat, double lng)
        {
            bool ret = false;
            if (lat == 0 || lat == 39.809734 || lat < -90 || lat > 90 || lng == 0 || lng == -98.555620 || lng < -180 || lng > 180)
                ret = false;
            else
                ret = true;
            return ret;
        }
        private static DateTime lastlatwarn = DateTime.Now;
        [DebuggerStepThrough]
        public static bool IsTimeBetween(DateTime time, string span)
        {
            if (span.IsEmpty())
                return true;  //if span is not set, assume its always true

            bool ret = false;

            try
            {
                // convert datetime to a TimeSpan
                TimeSpan now = time.TimeOfDay;

                //first split up multiple ranges:
                List<string> spans = span.SplitStr(",");
                foreach (var spn in spans)
                {

                    //support simple format hour1;hour2;hour3  12;1;2;3;4;5;6
                    if (spn.Contains(";"))
                    {
                        List<string> semsplt = spn.SplitStr(";");
                        foreach (string hr in semsplt)
                        {
                            //The value of the Hour property is always expressed using a 24-hour clock.
                            if (hr == time.Hour.ToString())
                            {
                                ret = true;
                                break;
                            }
                        }

                        if (ret)
                            break;
                    }
                    else
                    {
                        List<string> splt = spn.SplitStr("-", RemoveEmpty: false);

                        TimeSpan BeginSpan = now;
                        TimeSpan EndSpan = now;

                        //sunset-sunrise
                        //sunrise-sunset
                        //sunset_5-sunrise+5

                        if (splt[0].EqualsIgnoreCase("sunset") || splt[0].EqualsIgnoreCase("sunrise") || splt[0].Has("dusk") || splt[0].Has("dawn"))
                        {
                            if (!IsLatLongValid(AppSettings.Settings.LocalLatitude, AppSettings.Settings.LocalLongitude))
                            {
                                if ((DateTime.Now - lastlatwarn).TotalMinutes >= 30)
                                {
                                    Log($"Warn: The 'LocalLatitude' and 'LocalLongitude' settings in AITOOL.Settings.JSON file is incorrect and needs to be set to your local area.  Latitude is currently '{AppSettings.Settings.LocalLatitude}' and Longitude is '{AppSettings.Settings.LocalLongitude}'.  If you set those settings in a locally running copy of BlueIris > Settings > Schedule tab, they will be AUTOMATICALLY used.");
                                    lastlatwarn = DateTime.Now;
                                }
                            }
                            else
                            {
                                TimeZoneInfo localZone = TimeZoneInfo.Local;
                                SolarTimes solarTimes = new SolarTimes(time, AppSettings.Settings.LocalLatitude, AppSettings.Settings.LocalLongitude);

                                if (splt[0].Has("dusk"))
                                {
                                    BeginSpan = TimeZoneInfo.ConvertTimeFromUtc(solarTimes.DuskCivil.ToUniversalTime(), localZone).TimeOfDay;
                                }
                                else if (splt[0].EqualsIgnoreCase("sunset"))
                                {
                                    BeginSpan = TimeZoneInfo.ConvertTimeFromUtc(solarTimes.Sunset.ToUniversalTime(), localZone).TimeOfDay;
                                }
                                else if (splt[0].EqualsIgnoreCase("sunrise"))
                                {
                                    BeginSpan = TimeZoneInfo.ConvertTimeFromUtc(solarTimes.Sunrise.ToUniversalTime(), localZone).TimeOfDay;
                                }
                                else if (splt[0].Has("dawn"))
                                {
                                    BeginSpan = TimeZoneInfo.ConvertTimeFromUtc(solarTimes.DawnCivil.ToUniversalTime(), localZone).TimeOfDay;
                                }

                                if (splt[1].Has("dusk"))
                                {
                                    EndSpan = TimeZoneInfo.ConvertTimeFromUtc(solarTimes.DuskCivil.ToUniversalTime(), localZone).TimeOfDay;
                                }
                                else if (splt[1].EqualsIgnoreCase("sunset"))
                                {
                                    EndSpan = TimeZoneInfo.ConvertTimeFromUtc(solarTimes.Sunset.ToUniversalTime(), localZone).TimeOfDay;
                                }
                                else if (splt[1].EqualsIgnoreCase("sunrise"))
                                {
                                    EndSpan = TimeZoneInfo.ConvertTimeFromUtc(solarTimes.Sunrise.ToUniversalTime(), localZone).TimeOfDay;
                                }
                                else if (splt[1].Has("dawn"))
                                {
                                    EndSpan = TimeZoneInfo.ConvertTimeFromUtc(solarTimes.DawnCivil.ToUniversalTime(), localZone).TimeOfDay;
                                }

                            }

                        }
                        else
                        {
                            BeginSpan = TimeSpan.Parse(splt[0]);
                            EndSpan = TimeSpan.Parse(splt[1]);

                        }

                        // see if start comes before end
                        if (BeginSpan < EndSpan)
                        {
                            ret = BeginSpan <= now && now <= EndSpan;
                            //if (ret)
                            //    Console.WriteLine($"Time ({now.TotalHours.Round()}) IS       BETWEEN [{span}] BeginSpan ({BeginSpan.TotalHours.Round()}) is LESS THAN EndSpan ({EndSpan.TotalHours.Round()})");
                            //else
                            //    Console.WriteLine($"Time ({now.TotalHours.Round()}) IS *NOT* BETWEEN [{span}] BeginSpan ({BeginSpan.TotalHours.Round()}) is LESS THAN EndSpan ({EndSpan.TotalHours.Round()})");

                        }
                        else
                        {
                            // start is after end, so do the inverse comparison
                            ret = !(EndSpan < now && now < BeginSpan);
                            //if (ret)
                            //    Console.WriteLine($"Time ({now.TotalHours.Round()}) IS       BETWEEN [{span}] BeginSpan ({BeginSpan.TotalHours.Round()}) is GREATER THAN EndSpan ({EndSpan.TotalHours.Round()})");
                            //else
                            //    Console.WriteLine($"Time ({now.TotalHours.Round()}) IS *NOT* BETWEEN [{span}] BeginSpan ({BeginSpan.TotalHours.Round()}) is GREATER THAN EndSpan ({EndSpan.TotalHours.Round()})");

                        }

                        if (ret)
                            break;
                    }
                }

            }
            catch (Exception ex)
            {

                Log($"Error: Range Invalid: '{span}'. Lat='{AppSettings.Settings.LocalLatitude}', Long='{AppSettings.Settings.LocalLongitude}': " + ex.Msg());
            }

            return ret;
        }

        public static bool IsRegexPatternValid(string pattern)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(pattern) && pattern.Length > 2)
                {
                    System.Text.RegularExpressions.Regex test = new System.Text.RegularExpressions.Regex(pattern);
                    return true;
                }
            }
            catch { }
            return false;
        }

        public static bool OnlyHexInString(string test)
        {
            if (test.IsEmpty())
                return false;
            // For C-style hex notation (0xFF) you can use @"\A\b(0[xX])?[0-9a-fA-F]+\b\Z"
            return System.Text.RegularExpressions.Regex.IsMatch(test, @"\A\b[0-9a-fA-F]+\b\Z");
        }

        public static Color ConvertStringToColor(string InString, string InAlphaString = "")
        {
            Color Ret = Color.FromKnownColor(KnownColor.White);
            try
            {
                if (InString.IsNotEmpty())
                {
                    int Alpha = 255;
                    int Red = Ret.R;
                    int Green = Ret.G;
                    int Blue = Ret.B;
                    string[] splt;
                    if (InString.Contains(","))
                    {
                        splt = InString.Trim().Split(',');
                        if (splt.Count() == 3)
                        {
                            Red = splt[0].ToInt();
                            Green = splt[1].ToInt();
                            Blue = splt[2].ToInt();
                            Ret = Color.FromArgb(Red, Green, Blue);
                        }
                        else if (splt.Count() == 4)
                        {
                            Alpha = splt[0].ToInt();
                            Red = splt[1].ToInt();
                            Green = splt[2].ToInt();
                            Blue = splt[3].ToInt();
                            Ret = Color.FromArgb(Alpha, Red, Green, Blue);
                        }
                        else
                            Log("Error: Problem converting color, not 3 RGB numbers or 4 ARGB: '" + InString + ",");

                    }
                    else
                    {
                        if (InString.StartsWith("#") || OnlyHexInString(InString.Trim()))
                        {
                            int argb = Int32.Parse(InString.Replace("#", "").Trim(), NumberStyles.HexNumber);
                            Ret = Color.FromArgb(argb);
                        }
                        else
                        {
                            Ret = Color.FromName(InString.Trim());
                        }
                    }

                    if (InAlphaString.IsNotEmpty())
                    {
                        Ret = Color.FromArgb(InAlphaString.ToInt(), Ret);
                    }

                }
            }
            catch (Exception ex)
            {
                Log($"Error: InString='{InString}': {ex.Message}");
            }
            return Ret;
        }
        public static Double GetNumberDbl(object Obj)
        {
            //gets a number from anywhere within a string
            double Ret = 0;
            if (!Obj.IsNull())
            {
                if (Obj is string)
                {
                    string o = System.Convert.ToString(Obj).Trim();
                    double outdbl = 0;

                    //Take into account that some countries may use 123,45 vs 123.45
                    if (double.TryParse(o, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out outdbl))
                        Ret = outdbl;
                    else //try to extract the number from a larger string
                    {
                        try
                        {
                            //this can grab anything even "The number is 69,9 dude"
                            string outstrnum = Regex.Match(o.Replace(",", "."), @"[-+]?(?:\b[0-9]+(?:\.[0-9]*)?|\.[0-9]+\b)(?:[eE][-+]?[0-9]+\b)?").Value;
                            if (double.TryParse(outstrnum, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out outdbl))
                                Ret = outdbl;
                            else
                            {
                                Log($"Error: Could not parse to double? '{o}'");
                            }
                        }
                        catch { }

                    }


                    //if (OnlyNums != o)
                    //{
                    //    //debug
                    //    int brkpt = 0;
                    //}

                }
                else if (Obj is int)
                    Ret = Convert.ToDouble(Obj);
                else if (Obj is double)
                    Ret = ((double)Obj);
                else if (Obj is float)
                    Ret = (float)Obj;
            }
            return Ret;

        }
        public static int GetNumberInt(object Obj)
        {
            //gets a number from anywhere within a string
            int Ret = 0;
            if (Obj != null)
            {
                if (Obj is string && !string.IsNullOrWhiteSpace((string)Obj))
                {
                    string o = System.Convert.ToString(Obj).Trim();
                    double outdbl = 0;
                    string OnlyNums = "";

                    try
                    {
                        //this can grab anything even "The number is 69"
                        OnlyNums = Regex.Match(o, @"[-+]?(?:\b[0-9]+(?:\.[0-9]*)?|\.[0-9]+\b)(?:[eE][-+]?[0-9]+\b)?").Value;
                    }
                    catch { }

                    if (OnlyNums != o)
                    {
                        //debug
                        int brkpt = 0;
                    }

                    if (double.TryParse(OnlyNums, out outdbl))
                        Ret = Convert.ToInt32(Math.Round(outdbl));
                }
                else if (Obj is int)
                    Ret = (int)Obj;
                else if (Obj is double)
                    Ret = ((double)Obj).ToInt();
                else if (Obj is float)
                    Ret = Convert.ToInt32(Math.Round((float)Obj));
            }
            return Ret;

        }

        public static string CompressToBase64String(string strdata)
        {
            return Convert.ToBase64String(CompressGzip(Encoding.UTF8.GetBytes(strdata)));
        }
        public static string DeCompressFromBase64String(string strdata)
        {
            return Encoding.UTF8.GetString(DecompressGZ(Convert.FromBase64String(strdata)));
        }

        public static bool IsBase64String(string value)
        {
            if (value == null || value.Length == 0 || value.Length % 4 != 0
                || value.Contains(' ') || value.Contains('\t') || value.Contains('\r') || value.Contains('\n'))
                return false;
            var index = value.Length - 1;
            if (value[index] == '=')
                index--;
            if (value[index] == '=')
                index--;
            for (var i = 0; i <= index; i++)
                if (IsInvalid(value[i]))
                    return false;
            return true;
        }
        // Make it private as there is the name makes no sense for an outside caller
        private static bool IsInvalid(char value)
        {
            var intValue = (Int32)value;
            if (intValue >= 48 && intValue <= 57)
                return false;
            if (intValue >= 65 && intValue <= 90)
                return false;
            if (intValue >= 97 && intValue <= 122)
                return false;
            return intValue != 43 && intValue != 47;
        }
        public static byte[] CompressGzip(byte[] data)
        {
            try
            {
                using (MemoryStream output = new MemoryStream())
                {
                    using (GZipStream gzip = new GZipStream(output, CompressionMode.Compress, false))
                    {
                        gzip.Write(data, 0, data.Length);
                        gzip.Close();
                        return output.ToArray();
                    }
                }
            }
            catch (Exception ex)
            {
                Log("Error: " + ex.Msg());
            }

            return null;

        }

        public static byte[] DecompressGZ(byte[] data)
        {
            try
            {
                Stopwatch sw1 = new Stopwatch();
                sw1.Start();
                using (MemoryStream input = new MemoryStream())
                {
                    input.Write(data, 0, data.Length);
                    input.Position = 0;
                    using (GZipStream gzip = new GZipStream(input, CompressionMode.Decompress, false))
                    {
                        using (MemoryStream output = new MemoryStream())
                        {
                            byte[] buff = new byte[4097];
                            int read = -1;
                            read = gzip.Read(buff, 0, buff.Length);
                            while (read > 0)
                            {
                                output.Write(buff, 0, read);
                                read = gzip.Read(buff, 0, buff.Length);
                            }
                            gzip.Close();
                            sw1.Stop();
                            //Debug.Print(" (gz: " + sw1.ElapsedMilliseconds + "ms)");
                            return output.ToArray();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log("Error: " + ex.Msg());
            }

            return null;

        }

        public static string ReplaceCaseInsensitive(string input, string search, string replacement)
        {
            string result = Regex.Replace(
                input,
                Regex.Escape(search),
                replacement.Replace("$", "$$"),
                RegexOptions.IgnoreCase
            );
            return result;
        }


        public static String WildCardToRegular(String value)
        {
            return "^" + Regex.Escape(value).Replace("\\?", ".").Replace("\\*", ".*") + "$";
        }
        public static Regex RegEx_ValidDate = new Regex("(19|20)[0-9]{2}-(0[1-9]|1[012])-(0[1-9]|[12][0-9]|3[01])_[0-9][0-9]_[0-9][0-9]_[0-9][0-9]", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static bool IsInList(List<string> FindStrList, string SearchList, string Separators = ",;|", bool TrueIfEmpty = true)
        {
            if (TrueIfEmpty && string.IsNullOrWhiteSpace(SearchList))
                return true;  //If there is no searchlist, always return true

            return IsInList(FindStrList, SearchList.SplitStr(Separators, true, true, true));
        }
        [DebuggerStepThrough]
        public static bool IsInList(string FindStr, List<string> SearchList, string Separators = ",;|", bool TrueIfEmpty = true)
        {
            if (TrueIfEmpty && SearchList.Count == 0)
                return true;  //If there is no searchlist, always return true

            return IsInList(FindStr.SplitStr(Separators, true, true, true), SearchList);
        }
        [DebuggerStepThrough]
        public static bool IsInList(string FindStr, string SearchList, string Separators = ",;|", bool TrueIfEmpty = true)
        {
            if (TrueIfEmpty && string.IsNullOrWhiteSpace(SearchList))
                return true;  //If there is no searchlist, always return true


            return IsInList(FindStr.SplitStr(Separators, true, true, true), SearchList.SplitStr(Separators, true, true, true));
        }
        [DebuggerStepThrough]
        public static bool IsInList(List<string> FindStrsList, List<string> SearchList)
        {
            foreach (string findstr in FindStrsList)
            {
                foreach (string searchstr in SearchList)
                {
                    if (findstr.Equals(searchstr, StringComparison.OrdinalIgnoreCase) || searchstr == "*")
                        return true;
                }
            }
            return false;
        }

        [DebuggerStepThrough]
        public static string ConvertToBase64(this Stream stream)
        {
            byte[] bytes;
            using (var memoryStream = new MemoryStream())
            {
                stream.CopyTo(memoryStream);
                bytes = memoryStream.ToArray();
            }

            string base64 = Convert.ToBase64String(bytes);
            return base64;
        }



        public static IEnumerable<Exception> GetAllExceptions(this Exception exception)
        {
            yield return exception;

            if (exception is AggregateException aggrEx)
            {
                foreach (Exception innerEx in aggrEx.InnerExceptions.SelectMany(e => e.GetAllExceptions()))
                {
                    yield return innerEx;
                }
            }
            else if (exception.InnerException != null)
            {
                foreach (Exception innerEx in exception.InnerException.GetAllExceptions())
                {
                    yield return innerEx;
                }
            }
        }
        public static DateTime RetrieveLinkerTimestamp()
        {
            DateTime dt = new DateTime(1970, 1, 1, 0, 0, 0);

            try
            {
                string filePath = System.Reflection.Assembly.GetCallingAssembly().Location;
                const int c_PeHeaderOffset = 60;
                const int c_LinkerTimestampOffset = 8;
                byte[] b = new byte[2048];
                using (System.IO.Stream s = new System.IO.FileStream(filePath, System.IO.FileMode.Open, System.IO.FileAccess.Read))
                {

                    try
                    {
                        //s = New System.IO.FileStream(filePath, System.IO.FileMode.Open, System.IO.FileAccess.Read)
                        s.Read(b, 0, 2048);
                    }
                    finally
                    {
                        if (s != null)
                        {
                            s.Close();
                        }
                    }

                    int i = System.BitConverter.ToInt32(b, c_PeHeaderOffset);
                    int secondsSince1970 = System.BitConverter.ToInt32(b, i + c_LinkerTimestampOffset);
                    dt = dt.AddSeconds(secondsSince1970);
                    dt = dt.AddHours(System.TimeZone.CurrentTimeZone.GetUtcOffset(dt).Hours);
                }

            }
            catch (System.Exception ex)
            {
                Log("Error: " + ex.Msg());
            }

            return dt;

        }

        public class ClsDateFormat
        {
            public string Fmt = "";
            public long Cnt = 0;
            public override string ToString()
            {
                return $"Cnt='{this.Cnt}', Fmt='{this.Fmt}'";
            }
        }

        public static long DateFormatHitCnt = 0;

        public static List<ClsDateFormat> DateFormatList = new List<ClsDateFormat>();

        public static void CreateFormatList()
        {
            //28-Feb-2015 17:21:56.155
            //11/8/2012 09:28:33:941
            //15-May-2018 18:19:20.173
            //15-May-2018 18:05:28.457
            //dd-MMMM-yyyy HH:mm:ss.fff
            //7/15/2015 13:10:46:788
            //8/7/2017 13:00:15:330
            //6/14/2016 15:03:01:360
            //2018-04-10T14:32:26
            //yyyy-MM-ddTHH:mm:ss

            //most popular first
            DateFormatList.Add(new ClsDateFormat { Fmt = "dd-MMM-yyyy HH:mm:ss.fff" });
            DateFormatList.Add(new ClsDateFormat { Fmt = "yyyy-MM-dd HH:mm:ss.fff" });
            DateFormatList.Add(new ClsDateFormat { Fmt = "M/d/yyyy HH:mm:ss:fff" });
            DateFormatList.Add(new ClsDateFormat { Fmt = "dd-MMMM-yyyy HH:mm:ss.fff" });
            DateFormatList.Add(new ClsDateFormat { Fmt = "yyyy-MM-ddTHH:mm:ss" });

            DateFormatList.Add(new ClsDateFormat { Fmt = "M/d/yyyy H:mm:ss:fff" });
            DateFormatList.Add(new ClsDateFormat { Fmt = "M/dd/yyyy hh:mm:ss:fff" });
            DateFormatList.Add(new ClsDateFormat { Fmt = "M/dd/yyyy HH:mm:ss:fff" });
            DateFormatList.Add(new ClsDateFormat { Fmt = "M/dd/yyyy H:mm:ss:fff" });
            DateFormatList.Add(new ClsDateFormat { Fmt = "M/dd/yyyy hh:mm:ss:fff" });
            DateFormatList.Add(new ClsDateFormat { Fmt = "M/dd/yyyy HH:mm:ss:fff" });
            DateFormatList.Add(new ClsDateFormat { Fmt = "M/dd/yyyy H:mm:ss:fff" });
            DateFormatList.Add(new ClsDateFormat { Fmt = "MM/dd/yyyy hh:mm:ss:fff" });
            DateFormatList.Add(new ClsDateFormat { Fmt = "MM/dd/yyyy HH:mm:ss:fff" });
            DateFormatList.Add(new ClsDateFormat { Fmt = "MM/dd/yyyy H:mm:ss:fff" });
            DateFormatList.Add(new ClsDateFormat { Fmt = "d-MMMM-yyyy HH:mm:ss.fff" });
            DateFormatList.Add(new ClsDateFormat { Fmt = "d-MMM-yyyy HH:mm:ss.fff" });

        }
        public static bool GetDateStrict(string InpDate, ref DateTime OutDate, string format = "")
        {
            bool Ret = false;
            try
            {
                if (!string.IsNullOrEmpty(format))
                {
                    Ret = DateTime.TryParseExact(InpDate, format, null, System.Globalization.DateTimeStyles.None, out OutDate); //New CultureInfo("en-US")
                    if (Ret)
                        return Ret;
                }

                if (DateFormatList.Count == 0)
                {
                    CreateFormatList();
                }
                for (int i = 0; i < DateFormatList.Count; i++)
                {
                    ClsDateFormat df = DateFormatList[i];
                    Ret = DateTime.TryParseExact(InpDate, df.Fmt, null, System.Globalization.DateTimeStyles.None, out OutDate); //New CultureInfo("en-US")
                    if (Ret)
                    {
                        //First double check that date is in normal-ish range..
                        if (OutDate > new DateTime(2000, 1, 1) && OutDate <= DateTime.Now.AddHours(12))
                        {
                            df.Cnt = df.Cnt + 1;
                            DateFormatHitCnt = DateFormatHitCnt + 1;
                            //If DateFormatHitCnt < 15 OrElse (CurCnt > 1 AndAlso (DateFormatHitCnt Mod 25 = 0)) Then
                            //    'Sort the list by most frequent found cnt
                            //    DateFormatList = DateFormatList.OrderByDescending(Function(d) d.Cnt).ToList
                            //End If                        
                            break;
                        }
                        else
                        {
                            Ret = false;
                        }
                    }

                }

                if (!Ret)
                {
                    //last ditch
                    Ret = DateTime.TryParse(InpDate, out OutDate);
                    if (Ret)
                    {
                        if (OutDate > new DateTime(2010, 1, 1) && OutDate < new DateTime(2050, 1, 1))
                        {
                        }
                        else
                        {
                            Ret = false;
                            OutDate = DateTime.MinValue;
                        }
                    }
                }


            }
            catch (Exception)
            {
                Ret = false;
            }
            return Ret;
        }





        public static string GetXValue(XElement XE, string Name, string AttributeName = "")
        {
            string Ret = "";
            try
            {
                if (XE.HasElements)
                {
                    if (XE.Element(Name) != null)
                    {
                        if (string.IsNullOrEmpty(AttributeName))
                        {
                            Ret = XE.Element(Name).Value;
                        }
                        else
                        {
                            if (XE.Element(Name).HasAttributes)
                            {
                                if (XE.Element(Name).Attribute(AttributeName) != null)
                                {
                                    Ret = XE.Element(Name).Attribute(AttributeName).Value;
                                }
                                else
                                {
                                    //M.DbgLog($"Warning: Attribute not found '{AttributeName}' for Element '{Name}'.");
                                }
                            }
                            else
                            {
                                //M.DbgLog($"Warning: Attribute not found '{AttributeName}' for Element '{Name}'.");
                            }
                        }
                    }
                    else
                    {
                        //M.DbgLog($"Warning: Elements not found '{Name}'.");
                    }
                }
                else
                {
                    //M.Log($"Error: No elements found While getting '{Name}'.");
                }
            }
            catch (Exception)
            {
                //M.Log($"Error: While getting '{Name}', got error '{ex.Message}'.");
            }
            return Ret;
        }
    }
}
