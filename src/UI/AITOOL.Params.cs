using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Media;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

using Amazon;
using Amazon.Rekognition;
using Amazon.Rekognition.Model;
using Amazon.Runtime;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using NLog;

using NPushover;

using OSVersionExtension;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

using AITool.WebDashboard;

using static AITool.Global;

using Rectangle = System.Drawing.Rectangle;

namespace AITool
{
    public static partial class AITOOL
    {

        public static string ReplaceParams(Camera cam, History hist, ClsImageQueueItem CurImg, string instr, Global.IPType iptype, ClsPrediction curpred = null)
        {
            string ret = instr;

            try
            {
                string camname = "TESTCAMERANAME";
                string prefix = "TESTCAMERANAMEPREFIX";
                string imgpath = "C:\\TESTFILE.jpg";
                string caminputfolder = "C:\\TESTFOLDER";

                if (cam != null)
                {
                    camname = cam.BICamName;
                    prefix = cam.Prefix;
                    caminputfolder = cam.input_path;
                }

                if (CurImg != null)
                {
                    imgpath = CurImg.image_path;
                }
                else if (hist != null)
                {
                    imgpath = hist.Filename;
                }
                else if (cam != null)
                {
                    imgpath = cam.last_image_file;
                }

                //handle environment variables too:
                ret = Environment.ExpandEnvironmentVariables(ret);

                //handle date and time:
                ret = Global.ReplaceCaseInsensitive(ret, "%date%", DateTime.Now.ToString("d"));
                ret = Global.ReplaceCaseInsensitive(ret, "%time%", DateTime.Now.ToString("t"));
                ret = Global.ReplaceCaseInsensitive(ret, "%datetime%", DateTime.Now.ToString(AppSettings.Settings.DateFormat));


                //handle custom variables
                ret = Global.ReplaceCaseInsensitive(ret, "[camera]", camname);
                ret = Global.ReplaceCaseInsensitive(ret, "[prefix]", prefix);
                ret = Global.ReplaceCaseInsensitive(ret, "[caminputfolder]", caminputfolder);
                ret = Global.ReplaceCaseInsensitive(ret, "[inputfolder]", AppSettings.Settings.input_path);
                ret = Global.ReplaceCaseInsensitive(ret, "[imagepath]", imgpath); //gives the full path of the image that caused the trigger
                ret = Global.ReplaceCaseInsensitive(ret, "[imagepathescaped]", Uri.EscapeUriString(imgpath)); //gives the full path of the image that caused the trigger
                ret = Global.ReplaceCaseInsensitive(ret, "[imagefilename]", Path.GetFileName(imgpath)); //gives the image name of the image that caused the trigger
                ret = Global.ReplaceCaseInsensitive(ret, "[imagefilenamenoext]", Path.GetFileNameWithoutExtension(imgpath)); //gives the image name of the image that caused the trigger

                if (!AppSettings.Settings.DefaultUserName.IsEmpty())
                    ret = Global.ReplaceCaseInsensitive(ret, "[username]", AppSettings.Settings.DefaultUserName); //gives the image name of the image that caused the trigger

                string pw = AppSettings.Settings.DefaultPasswordEncrypted.Decrypt();
                if (!pw.IsEmpty())
                    ret = Global.ReplaceCaseInsensitive(ret, "[password]", pw); //gives the image name of the image that caused the trigger

                if (BlueIrisInfo.Result == BlueIrisResult.Valid)
                {
                    ret = Global.ReplaceCaseInsensitive(ret, "[blueirisserverip]", Global.IP2Str(AppSettings.Settings.BlueIrisServer.Trim(), iptype)); //gives the image name of the image that caused the trigger
                    ret = Global.ReplaceCaseInsensitive(ret, "[blueirisurl]", BlueIrisInfo.URL); //gives the image name of the image that caused the trigger
                }


                if (hist != null || curpred != null)
                {
                    List<ClsPrediction> preds = new List<ClsPrediction>();

                    if (hist != null)
                        preds = hist.Predictions();
                    else if (curpred != null)
                        preds.Add(curpred);

                    List<ClsDeepstackDetection> detectionslst = new List<ClsDeepstackDetection>();

                    if (preds != null && preds.Count > 0)
                    {
                        string detections = "";
                        string confidences = "";

                        foreach (ClsPrediction pred in preds)
                        {
                            if (pred.Result != ResultType.Relevant && AppSettings.Settings.HistoryOnlyDisplayRelevantObjects)
                                continue;

                            detectionslst.Add(pred.ToDeepstackDetection());

                            confidences += pred.ConfidenceString() + ",";
                            detections += pred.ToString() + ",";
                        }
                        ret = Global.ReplaceCaseInsensitive(ret, "[summarynonescaped]", hist.Detections); //summary text including all detections and confidences, p.e."person (91,53%)"
                        ret = Global.ReplaceCaseInsensitive(ret, "[summary]", Uri.EscapeUriString(hist.Detections)); //summary text including all detections and confidences, p.e."person (91,53%)"
                        ret = Global.ReplaceCaseInsensitive(ret, "[detection]", preds[0].ToString()); //only gives first detection 
                        ret = Global.ReplaceCaseInsensitive(ret, "[label]", preds[0].Label); //only gives first detection 
                        ret = Global.ReplaceCaseInsensitive(ret, "[detail]", preds[0].Detail);
                        ret = Global.ReplaceCaseInsensitive(ret, "[detailescaped]", Uri.EscapeUriString(preds[0].Detail));
                        ret = Global.ReplaceCaseInsensitive(ret, "[result]", preds[0].Result.ToString());
                        ret = Global.ReplaceCaseInsensitive(ret, "[percentofimage]", preds[0].PercentOfImage.Round().ToString());
                        ret = Global.ReplaceCaseInsensitive(ret, "[position]", preds[0].PositionString());
                        ret = Global.ReplaceCaseInsensitive(ret, "[confidence]", preds[0].ConfidenceString());
                        ret = Global.ReplaceCaseInsensitive(ret, "[detections]", detections.Trim(",".ToCharArray()));
                        ret = Global.ReplaceCaseInsensitive(ret, "[confidences]", confidences.Trim(",".ToCharArray()));

                        //longest-present track among this detection's predictions (0/0 if tracking/loitering is not in use)
                        ClsPrediction TrackPred = preds.OrderByDescending(p => p.TrackSeconds).First();
                        ret = Global.ReplaceCaseInsensitive(ret, "[trackseconds]", TrackPred.TrackSeconds.Round().ToString());
                        ret = Global.ReplaceCaseInsensitive(ret, "[trackid]", TrackPred.TrackId.ToString());

                    }
                    else
                    {
                        ret = Global.ReplaceCaseInsensitive(ret, "[summarynonescaped]", "Test Summary");
                        ret = Global.ReplaceCaseInsensitive(ret, "[summary]", "Test Summary"); //summary text including all detections and confidences, p.e."person (91,53%)"
                        ret = Global.ReplaceCaseInsensitive(ret, "[detection]", "Test Detection");
                        ret = Global.ReplaceCaseInsensitive(ret, "[label]", "Person");
                        ret = Global.ReplaceCaseInsensitive(ret, "[detail]", "Test Detail");
                        ret = Global.ReplaceCaseInsensitive(ret, "[detailescaped]", "Test Detail");
                        ret = Global.ReplaceCaseInsensitive(ret, "[result]", "Relevant");
                        ret = Global.ReplaceCaseInsensitive(ret, "[position]", "0,0,0,0");
                        ret = Global.ReplaceCaseInsensitive(ret, "[percentofimage]", "50.123");
                        ret = Global.ReplaceCaseInsensitive(ret, "[confidence]", string.Format(AppSettings.Settings.DisplayPercentageFormat, 99.123));
                        ret = Global.ReplaceCaseInsensitive(ret, "[detections]", "Detection1, Detection2");
                        ret = Global.ReplaceCaseInsensitive(ret, "[confidences]", string.Format(AppSettings.Settings.DisplayPercentageFormat, 99.123) + ", " + string.Format(AppSettings.Settings.DisplayPercentageFormat, 90.01));
                        ret = Global.ReplaceCaseInsensitive(ret, "[trackseconds]", "12.3");
                        ret = Global.ReplaceCaseInsensitive(ret, "[trackid]", "1");
                    }

                    if (ret.IndexOf("[Summaryjson]", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        ret = Global.ReplaceCaseInsensitive(ret, "[SummaryJson]", "{\"summary\": \"" + Uri.EscapeUriString(hist.Detections) + "\"}");
                    }

                    if (ret.IndexOf("[Detectionsjson]", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        string jsonstr = GetJSONString(detectionslst.ToArray(), Formatting.None).CleanString();
                        ret = Global.ReplaceCaseInsensitive(ret, "[DetectionsJson]", "{\"detections\": \"" + jsonstr + "\"}");
                    }

                    if (ret.IndexOf("[alljson]", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        AllJson json = new AllJson();
                        json.Time = hist.Date;
                        json.Camera = cam.Name;
                        json.analysisDurationMS = hist.analysisDurationMS;
                        json.fileName = hist.Filename;
                        json.baseName = Path.GetFileName(hist.Filename);
                        json.summary = hist.Detections;
                        json.state = hist.state;
                        json.predictions = detectionslst.ToArray();
                        string jsonstr = GetJSONString(json, Formatting.None, TypeNameHandling.None, PreserveReferencesHandling.None).CleanString();
                        ret = Global.ReplaceCaseInsensitive(ret, "[alljson]", jsonstr);
                    }

                }
                else if (cam != null)
                {
                    if (cam.last_detections != null && cam.last_detections.Count > 0)
                    {
                        ret = Global.ReplaceCaseInsensitive(ret, "[summarynonescaped]", cam.last_detections_summary); //summary text including all detections and confidences, p.e."person (91,53%)"
                        ret = Global.ReplaceCaseInsensitive(ret, "[summary]", Uri.EscapeUriString(cam.last_detections_summary)); //summary text including all detections and confidences, p.e."person (91,53%)"
                        ret = Global.ReplaceCaseInsensitive(ret, "[detection]", cam.last_detections.ElementAt(0)); //only gives first detection (maybe not most relevant one)
                        ret = Global.ReplaceCaseInsensitive(ret, "[label]", cam.last_detections.ElementAt(0)); //only gives first detection (maybe not most relevant one)
                        ret = Global.ReplaceCaseInsensitive(ret, "[detail]", cam.last_details.ElementAt(0)); //only gives first detection (maybe not most relevant one)
                        ret = Global.ReplaceCaseInsensitive(ret, "[detailescaped]", Uri.EscapeUriString(cam.last_details.ElementAt(0))); //only gives first detection (maybe not most relevant one)
                        ret = Global.ReplaceCaseInsensitive(ret, "[position]", cam.last_positions.ElementAt(0));
                        ret = Global.ReplaceCaseInsensitive(ret, "[confidence]", string.Format(AppSettings.Settings.DisplayPercentageFormat, cam.last_confidences.ElementAt(0)));
                        ret = Global.ReplaceCaseInsensitive(ret, "[result]", "Unknown");
                        ret = Global.ReplaceCaseInsensitive(ret, "[percentofimage]", "50.123");
                        ret = Global.ReplaceCaseInsensitive(ret, "[detections]", string.Join(",", cam.last_detections));
                        ret = Global.ReplaceCaseInsensitive(ret, "[confidences]", string.Join(",", cam.last_confidences.Select(x => x.ToString(AppSettings.Settings.DisplayPercentageFormat))));
                        ret = Global.ReplaceCaseInsensitive(ret, "[trackseconds]", "0");
                        ret = Global.ReplaceCaseInsensitive(ret, "[trackid]", "0");
                    }
                    else
                    {
                        ret = Global.ReplaceCaseInsensitive(ret, "[summarynonescaped]", "Test Summary");
                        ret = Global.ReplaceCaseInsensitive(ret, "[summary]", "Test Summary"); //summary text including all detections and confidences, p.e."person (91,53%)"
                        ret = Global.ReplaceCaseInsensitive(ret, "[detection]", "Test Detection"); //only gives first detection (maybe not most relevant one)
                        ret = Global.ReplaceCaseInsensitive(ret, "[detail]", "Test Detail");
                        ret = Global.ReplaceCaseInsensitive(ret, "[detailescaped]", "Test Detail");
                        ret = Global.ReplaceCaseInsensitive(ret, "[label]", "Person");
                        ret = Global.ReplaceCaseInsensitive(ret, "[result]", "Relevant");
                        ret = Global.ReplaceCaseInsensitive(ret, "[percentofimage]", "50.123");
                        ret = Global.ReplaceCaseInsensitive(ret, "[position]", "0,0,0,0");
                        ret = Global.ReplaceCaseInsensitive(ret, "[confidence]", string.Format(AppSettings.Settings.DisplayPercentageFormat, 99.123));
                        ret = Global.ReplaceCaseInsensitive(ret, "[detections]", "Detection1, Detection2");
                        ret = Global.ReplaceCaseInsensitive(ret, "[confidences]", string.Format(AppSettings.Settings.DisplayPercentageFormat, 99.123) + ", " + string.Format(AppSettings.Settings.DisplayPercentageFormat, 90.01));
                        ret = Global.ReplaceCaseInsensitive(ret, "[trackseconds]", "12.3");
                        ret = Global.ReplaceCaseInsensitive(ret, "[trackid]", "1");
                    }

                    if (ret.IndexOf("[Summaryjson]", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        ret = Global.ReplaceCaseInsensitive(ret, "[SummaryJson]", "{\"summary\": \"Test Summary\"}");
                    }

                    if (ret.IndexOf("[Detectionsjson]", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        ret = Global.ReplaceCaseInsensitive(ret, "[DetectionsJson]", "{\"detections\": \"[]\"}");
                    }


                    if (ret.EqualsIgnoreCase("[alljson]"))
                    {
                        AllJson json = new AllJson();
                        json.analysisDurationMS = 999;
                        json.fileName = imgpath;
                        json.Camera = cam.Name;
                        json.baseName = Path.GetFileName(imgpath);
                        if (cam.last_detections_summary.IsEmpty())
                            cam.last_detections_summary = "Test Detection";
                        json.summary = Uri.EscapeUriString(cam.last_detections_summary);
                        json.state = "on";
                        json.predictions = new List<ClsDeepstackDetection>().ToArray();
                        json.Time = DateTime.Now;
                        string jsonstr = GetJSONString(json, Formatting.None, TypeNameHandling.None, PreserveReferencesHandling.None).CleanString();
                        ret = Global.ReplaceCaseInsensitive(ret, "[alljson]", jsonstr);
                    }



                }

            }
            catch (Exception ex)
            {

                Log($"Error: {ex.Msg()}");
            }

            return ret;

        }
    }
}
