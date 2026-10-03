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

        public static void UpdateAIURLs()
        {


            if (AppSettings.GetURL(type: URLTypeEnum.AWSRekognition_Objects) != null || AppSettings.GetURL(type: URLTypeEnum.AWSRekognition_Faces) != null) // || this.url.Equals("aws", StringComparison.OrdinalIgnoreCase) || this.url.Equals("rekognition", StringComparison.OrdinalIgnoreCase))
            {
                string error = AITOOL.UpdateAmazonSettings();

                if (!string.IsNullOrEmpty(error))
                {
                    AITOOL.Log($"Error: {error}");

                    if (error.IndexOf("endpoint", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        //hardcode the list for now:  https://docs.aws.amazon.com/general/latest/gr/rande.html
                        List<string> endpoints = new List<string>();
                        endpoints.Add("US East (N. Virginia)  \tus-east-1");
                        endpoints.Add("US East (Ohio)  \tus-east-2");
                        endpoints.Add("US West (N. California)  \tus-west-1");
                        endpoints.Add("US West (Oregon)  \tus-west-2");
                        endpoints.Add("Canada (Central)  \tca-central-1");
                        endpoints.Add("Europe (London)  \teu-west-2");
                        endpoints.Add("Europe (Frankfurt)  \teu-central-1");
                        endpoints.Add("Europe (Ireland)  \teu-west-1");
                        endpoints.Add("Europe (Milan)  \teu-south-1");
                        endpoints.Add("Europe (Paris)  \teu-west-3");
                        endpoints.Add("Europe (Stockholm)  \teu-north-1");
                        endpoints.Add("Africa (Cape Town)  \taf-south-1");
                        endpoints.Add("Middle East (Bahrain)  \tme-south-1");
                        endpoints.Add("South America (São Paulo)  \tsa-east-1");
                        endpoints.Add("China (Beijing)  \tcn-north-1");
                        endpoints.Add("China (Ningxia)  \tcn-northwest-1");
                        endpoints.Add("Asia Pacific (Hong Kong)  \tap-east-1");
                        endpoints.Add("Asia Pacific (Mumbai)  \tap-south-1");
                        endpoints.Add("Asia Pacific (Osaka-Local)  \tap-northeast-3");
                        endpoints.Add("Asia Pacific (Seoul)  \tap-northeast-2");
                        endpoints.Add("Asia Pacific (Singapore)  \tap-southeast-1");
                        endpoints.Add("Asia Pacific (Sydney)  \tap-southeast-2");
                        endpoints.Add("Asia Pacific (Tokyo)  \tap-northeast-1");

                        using (var form = new InputForm("Select Amazon AWS endpoint near you:", "Amazon AWS Endpoint", cbitems: endpoints))
                        {
                            var result = form.ShowDialog();
                            if (result == DialogResult.OK)
                            {
                                string region = "";
                                if (!string.IsNullOrEmpty(form.text))
                                {
                                    if (form.text.Contains("\t"))
                                    {
                                        region = form.text.GetWord("\t", "").Trim();
                                    }
                                    else if (form.text.Contains("-"))
                                    {
                                        region = form.text.Trim();
                                    }

                                }
                                if (string.IsNullOrEmpty(region))
                                {
                                    MessageBox.Show($"Error: No endpoint selected '{form.text}'");
                                }
                                else
                                {
                                    AppSettings.Settings.AmazonRegionEndpoint = region;
                                }
                            }
                        }
                    }

                    error = AITOOL.UpdateAmazonSettings();

                    if (!string.IsNullOrEmpty(error))
                    {
                        AITOOL.Log($"Error: {error}");
                        if (error.IndexOf("rootkey", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            MessageBox.Show(error, "Missing AWS credentials", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }

                }
            }

            if (AppSettings.GetURL(type: URLTypeEnum.SightHound_Person) != null || AppSettings.GetURL(type: URLTypeEnum.SightHound_Vehicle) != null)
            {
                if (string.IsNullOrWhiteSpace(AppSettings.Settings.SightHoundAPIKey))
                {
                    using (var form = new InputForm("Enter SightHound API Key", "SightHound API Key"))
                    {
                        var result = form.ShowDialog();
                        if (result == DialogResult.OK)
                        {
                            if (!string.IsNullOrEmpty(form.text) && form.text.Trim().Length > 30) //It looks like they are 36 chars
                            {
                                AppSettings.Settings.SightHoundAPIKey = form.text.Trim();
                            }
                            else
                            {
                                MessageBox.Show("Enter a valid key.");
                            }
                        }
                    }
                }
            }

            //let the image loop (running in another thread) know to recheck ai server url settings.
            //AIURLSettingsChanged= true;

            AITOOL.UpdateAIURLList(true);

        }

        public static string UpdateAmazonSettings()
        {

            using var Trace = new Trace();  //This c# 8.0 using feature will auto dispose when the function is done.

            if (AppSettings.GetURL(type: URLTypeEnum.AWSRekognition_Objects) == null && AppSettings.GetURL(type: URLTypeEnum.AWSRekognition_Faces) == null) // || this.url.Equals("aws", StringComparison.OrdinalIgnoreCase) || this.url.Equals("rekognition", StringComparison.OrdinalIgnoreCase))
                return "";

            string error = "";

            RegionEndpoint endpoint = null;
            try
            {
                if (!string.IsNullOrWhiteSpace(AppSettings.Settings.AmazonRegionEndpoint))
                    endpoint = RegionEndpoint.GetBySystemName(AppSettings.Settings.AmazonRegionEndpoint);
                else
                    error = "No AmazonRegionEndpoint set.";
            }
            catch (Exception ex)
            {
                error = $"Could not set Amazon region endpoint to '{AppSettings.Settings.AmazonRegionEndpoint}' (JSON setting=AmazonRegionEndpoint): {ex.Message}";
            }

            if (endpoint != null)
            {

                AITOOL.Log($"Debug: Amazon RegionEndpoint DisplayName={endpoint.DisplayName}, PartitionDnsSuffix={endpoint.PartitionDnsSuffix}, PartitionName={endpoint.PartitionName}, SystemName={endpoint.SystemName}");
                //always try to extract latest from rootkey.csv if found
                string pth = Path.Combine(Path.GetDirectoryName(AppSettings.Settings.SettingsFileName), "rootkey.csv");
                if (File.Exists(pth))
                {
                    string csv = File.ReadAllText(pth);
                    if (!string.IsNullOrWhiteSpace(csv))
                    {
                        AITOOL.Log($"Debug: Extracting AWSAccessKeyId and AWSSecretKey from {pth}");
                        if (csv.Contains(",") && !csv.Has("AWSAccessKeyId="))
                        {
                            //User name, Password,Access key ID,  Secret access key,          Console login link
                            //aitooluser,        ,XXXXXXXXXXXXX,  xxxxxxxxxxxxxxxxxxxxxxxxxx, https://xxxxxxxxx.signin.aws.amazon.com/console

                            List<string> lines = csv.SplitStr("\r\n", true);
                            if (lines.Count > 1)
                            {
                                List<string> header = lines[0].ToLower().SplitStr(",", false);
                                List<string> firstline = lines[0].SplitStr(",", false);
                                int idxID = header.IndexOf("access key id");
                                int idxSECRET = header.IndexOf("secret access key");
                                if (idxID > -1 && idxSECRET > -1)
                                {
                                    AppSettings.Settings.AmazonAccessKeyId = firstline[idxID];
                                    AppSettings.Settings.AmazonSecretKey = firstline[idxSECRET];
                                }
                                else
                                {
                                    error = $"Error: Could not find 'Access Key ID' or 'Secret Access key' columns in '{pth}'";
                                }
                            }
                            else
                            {
                                error = $"Error: Too few lines in '{pth}'";
                            }
                        }
                        else if (csv.Has("AWSAccessKeyId="))
                        {
                            //old format
                            string tid = csv.GetWord("AWSAccessKeyId=", "\r|\n");
                            string tsid = csv.GetWord("AWSSecretKey=", "\r|\n|");
                            if (!string.IsNullOrEmpty(tid) && tid != AppSettings.Settings.AmazonAccessKeyId)
                                AppSettings.Settings.AmazonAccessKeyId = tid;
                            if (!string.IsNullOrEmpty(tsid) && tsid != AppSettings.Settings.AmazonSecretKey)
                                AppSettings.Settings.AmazonSecretKey = tsid;

                        }
                        else
                        {
                            error = $"Error: File is an unknown format '{pth}'";
                        }

                    }
                    else
                    {
                        error = $"Error: Empty file '{pth}'";
                    }

                }
                else
                {
                    error = $"Could not find AWS credentials file '{pth}'";
                }

                if (string.IsNullOrWhiteSpace(AppSettings.Settings.AmazonAccessKeyId) || string.IsNullOrWhiteSpace(AppSettings.Settings.AmazonSecretKey))
                {
                    error = "Please download 'rootkey.csv' and place it in AITOOL _SETTINGS folder.  1) Sign up for AWS, 2) Create user 3) Export rootkey.csv when prompted.  https://docs.aws.amazon.com/rekognition/latest/dg/setting-up.html  Please note that you have to CREATE NEW in order to see rootkey.csv if one already exists: https://console.aws.amazon.com/iam/home#/security_credentials";
                }
            }
            else
            {
                error = error + "- Please close AITOOL and set 'AmazonRegionEndpoint' in AITOOL.SETTTINGS.JSON to a region code near you such as 'us-east-1': https://docs.aws.amazon.com/general/latest/gr/rande.html";
            }

            return error;

        }

        public static void UpdateAIURLList(bool Force = false)
        {
            using var Trace = new Trace();  //This c# 8.0 using feature will auto dispose when the function is done.

            AIURLListAvailableRefineServerCount = 0;
            //double check all the URL's have a new httpclient
            foreach (ClsURLItem url in AppSettings.Settings.AIURLList)
            {
                url.Update(false);
                url.AITimeCalcs.UpdateDate(true);

                if (url.IsValid && url.UseAsRefinementServer)
                    AIURLListAvailableRefineServerCount++;

                if (url.Type != URLTypeEnum.AWSRekognition_Objects && url.Type != URLTypeEnum.AWSRekognition_Faces && url.HttpClient == null)
                {
                    url.HttpClient = new HttpClient();
                    url.HttpClient.Timeout = url.GetTimeout();
                }

            }

            //remove dupes
            List<ClsURLItem> newlist = new List<ClsURLItem>();
            foreach (ClsURLItem url in AppSettings.Settings.AIURLList)
            {
                if (!newlist.Contains(url))
                    newlist.Add(url);
            }
            AppSettings.Settings.AIURLList = newlist;



            //Check to see if we need to get updated URL list - In theory this should only happen once
            bool hasold = !string.IsNullOrEmpty(AppSettings.Settings.deepstack_url);
            if (((AppSettings.Settings.AIURLList.Count == 0 || Force) && hasold) || hasold)
            {
                int newcnt = 0;

                try
                {
                    Log("Debug: Updating/Resetting AI URL list...");
                    List<string> SpltURLs = AppSettings.Settings.deepstack_url.SplitStr("|;,");

                    //I want to reuse any object that already exists for the url but make sure to get the right order if it changes
                    Dictionary<string, ClsURLItem> tmpdic = new Dictionary<string, ClsURLItem>();

                    foreach (ClsURLItem url in AppSettings.Settings.AIURLList)
                    {
                        string ur = url.ToString().ToLower();
                        if (!tmpdic.ContainsKey(ur))
                        {
                            tmpdic.Add(ur, url);
                        }
                        else
                        {
                            Log($"Debug: ---- (duplicate url configured - {ur})");
                        }
                    }

                    AppSettings.Settings.AIURLList.Clear();


                    for (int i = 0; i < SpltURLs.Count; i++)
                    {
                        if (!SpltURLs[i].Contains(":"))
                        {
                            Log($"Error: Skipping old server name migration because it doesn't have a port: '{SpltURLs[i]}'");
                            continue;
                        }

                        ClsURLItem url = null;
                        try
                        {
                            url = new ClsURLItem(SpltURLs[i], i + 1, URLTypeEnum.Unknown);
                        }
                        catch (Exception ex) { Log($"Error: url='{SpltURLs[i]}': {ex.Msg()}"); }

                        if (url != null && url.IsValid)
                        {
                            //if it already exists, use it, otherwise add a new one
                            if (tmpdic.ContainsKey(url.ToString().ToLower()))
                            {
                                url = tmpdic[url.ToString().ToLower()];
                                AppSettings.Settings.AIURLList.Add(url);
                                url.Order = i + 1;
                                //url.InUse = false;
                                url.CurErrCount = 0;
                                url.Enabled = true;
                                Log($"Debug: ----   #{url.Order}: Re-added known URL: '{url}'");
                            }
                            else
                            {
                                newcnt++;
                                AppSettings.Settings.AIURLList.Add(url);
                                Log($"Debug: ----   #{url.Order}: Added new URL: '{url}'");
                            }

                        }
                        else
                        {
                            Log($"Debug: ----   #{url.Order}: Skipped INVALID URL: '{SpltURLs[i]}'");

                        }
                    }

                }
                catch (Exception ex)
                {
                    Log($"Error: {ex.Msg()}");
                }

                Log($"Debug: ...{newcnt} new AI URL's migrated from old settings, with a total of {AppSettings.Settings.AIURLList.Count} AI URL's");

                AppSettings.Settings.deepstack_url = "";  //we are not going to use this any longer
                //AIURLSettingsChanged = false;

            }


            //add a default in-process Local_ONNX server if none found at all, so detection works out of the box on a fresh install
            AddDefaultLocalOnnxServerIfEmpty(AppSettings.Settings.AIURLList, AppSettings.Settings.OnnxDefaultModelPath);

        }

        //Factored out of UpdateAIURLList so it can be unit tested without the rest of the settings/load machinery.
        //Only touches AIURLList when it is completely empty (a fresh install) - never overrides a user's existing servers.
        public static bool AddDefaultLocalOnnxServerIfEmpty(List<ClsURLItem> AIURLList, string DefaultModelPath)
        {
            if (AIURLList.Count > 0 || DefaultModelPath.IsEmpty())
                return false;

            Log("Debug: ----   No AI servers configured - adding default in-process 'Local YOLO (built-in)' server so detection works out of the box.");

            ClsURLItem url = new ClsURLItem(DefaultModelPath, 1, URLTypeEnum.Local_ONNX);
            url.Name = "Local YOLO (built-in)";
            AIURLList.Add(url);

            return true;
        }

        public static async Task<List<ClsURLItem>> WaitForNextURL(Camera cam, bool GetRefinementServer, List<ClsPrediction> predictions = null, string RequiredLinkURLList = "", List<ClsURLItem> MainURLs = null)
        {
            //lets wait in here forever until a URL is available...  Unless trying to get a refinement server
            using var Trace = new Trace();  //This c# 8.0 using feature will auto dispose when the function is done.

            List<ClsURLItem> ret = new List<ClsURLItem>();

            DateTime LastWaitingLog = DateTime.MinValue;
            bool displayedbad = false;
            bool displayedretry = false;
            List<string> CurrentURLs = new List<string>();
            List<string> LinkedRequiredURLs = RequiredLinkURLList.SplitStr(",;|", ToLower: true);
            bool GetLinkedRequiredList = LinkedRequiredURLs.Count > 0;
            int FoundRequiredCount = 0;
            int FoundRefinementCount = 0;
            int RefineNoMatchCount = 0;
            int RefineMatchCount = 0;
            string refinepreds = "";
            DateTime now = DateTime.Now;

            Stopwatch sw = Stopwatch.StartNew();

            try
            {
                AIURLListAvailableRefineServerCount = 0;

                //preprocess a few things...
                for (int i = 0; i < AppSettings.Settings.AIURLList.Count; i++)
                {
                    AppSettings.Settings.AIURLList[i].RefinementUseCurrentlyValid = false;  //assume false at start of each loop

                    if (AppSettings.Settings.AIURLList[i].UseAsRefinementServer)
                    {
                        bool notenabled = !AppSettings.Settings.AIURLList[i].Enabled;
                        bool notintime = !Global.IsTimeBetween(now, AppSettings.Settings.AIURLList[i].ActiveTimeRange);
                        bool notinlist = !Global.IsInList(cam.Name, AppSettings.Settings.AIURLList[i].Cameras, TrueIfEmpty: true);
                        if (notenabled || notintime || notinlist)
                        {
                            AppSettings.Settings.AIURLList[i].LastTestedTime = DateTime.Now;
                            AppSettings.Settings.AIURLList[i].LastSkippedReason = $"Refine: NotEnabled={notenabled}, NotInTime={notintime}, NotInCamList={notinlist}";
                            continue;
                        }

                        //dont let a refinement server be the same as the main server
                        if (MainURLs != null && MainURLs.Count > 0)
                        {
                            bool fnd = false;
                            foreach (ClsURLItem url in MainURLs)
                            {
                                if (string.Equals(AppSettings.Settings.AIURLList[i].ToString(), url.ToString(), StringComparison.OrdinalIgnoreCase))
                                {
                                    fnd = true;
                                    break;
                                }
                            }
                            if (fnd)
                            {
                                AppSettings.Settings.AIURLList[i].LastTestedTime = DateTime.Now;
                                AppSettings.Settings.AIURLList[i].LastSkippedReason = $"Refine: FoundSameAsMainServer={fnd}";
                                continue;
                            }
                        }

                        AIURLListAvailableRefineServerCount++;

                        if (GetRefinementServer && predictions != null)
                        {
                            //set temp flag to indicate if the server can be CURRENTLY used as a refinement server
                            foreach (ClsPrediction pred in predictions)
                            {
                                bool fnd = false;

                                if (pred.Result == ResultType.Relevant)
                                {
                                    refinepreds += pred.Label + ",";
                                    fnd = IsRefinementMatch(pred, AppSettings.Settings.AIURLList[i].RefinementObjects);

                                    if (fnd)
                                    {
                                        RefineMatchCount++;
                                        AppSettings.Settings.AIURLList[i].RefinementUseCurrentlyValid = true;
                                        break;
                                    }
                                }

                            }


                            if (!AppSettings.Settings.AIURLList[i].RefinementUseCurrentlyValid)
                            {
                                AppSettings.Settings.AIURLList[i].LastTestedTime = DateTime.Now;
                                AppSettings.Settings.AIURLList[i].LastSkippedReason = $"Refine: No matched refine objects";
                                RefineNoMatchCount++;  //count number of failed tries to match refinement server objects
                            }
                        }

                    }
                }

                while (ret.Count == 0)
                {
                    int disabled = 0;
                    int inuse = 0;
                    int incorrectcam = 0;
                    int notintimerange = 0;
                    int maxpermonth = 0;
                    int notrefined = 0;
                    int notrequired = 0;
                    int onlylinked = 0;
                    int notonline = 0;

                    //If no refinement servers or less than should be available were returned
                    if (GetRefinementServer && RefineMatchCount == 0)
                    {
                        Log($"Debug: ---- Refinement server requested, but none were available for predictions '{refinepreds}'. ({sw.ElapsedMilliseconds}ms)");
                        break;
                    }

                    try
                    {
                        UpdateAIURLList();

                        List<ClsURLItem> sortedurls = new List<ClsURLItem>();

                        if (AppSettings.Settings.deepstack_urls_are_queued)
                        {
                            //always use oldest first
                            sortedurls = AppSettings.Settings.AIURLList.OrderBy((d) => d.LastUsedTime).ToList();
                        }
                        else
                        {
                            //use original order
                            sortedurls.AddRange(AppSettings.Settings.AIURLList);
                        }
                        //sort by oldest last used

                        //quick initial count of valid servers
                        int ValidServerCnt = 0;
                        foreach (ClsURLItem url in sortedurls)
                        {
                            if (url.IsValid && url.Enabled && !url.UseOnlyAsLinkedServer)  //may need to tweak this to exclude refinement/linked only servers?
                                ValidServerCnt++;
                        }


                        for (int i = 0; i < sortedurls.Count; i++)
                        {
                            ClsURLItem url = sortedurls[i];

                            if (i > 0 && !GetRefinementServer && !GetLinkedRequiredList)
                            {
                                int debugging = 0;
                            }

                            now = DateTime.Now;

                            if (!url.Enabled)
                            {
                                url.LastTestedTime = now;  // (Write here, right now
                                                           //  Watching the world wake up from history)
                                url.LastSkippedReason = url.LastSkippedReason.Prepend("NotEnabled");
                                continue;
                            }

                            if (!url.ErrDisabled)
                            {
                                if (url.IsServerReady(AITOOL.ImageProcessQueue.Count, ValidServerCnt))
                                {
                                    if (Global.IsInList(cam.Name, url.Cameras, TrueIfEmpty: true))
                                    {
                                        if (GetRefinementServer && url.UseAsRefinementServer || !url.UseAsRefinementServer)
                                        {
                                            if (url.MaxImagesPerMonth == 0 || url.AITimeCalcs.CountMonth <= url.MaxImagesPerMonth)
                                            {

                                                if (Global.IsTimeBetween(now, url.ActiveTimeRange))
                                                {
                                                    //if set to ignoreconnection errors do an extra ping check to see if the server is available.  If not, skip it
                                                    if (url.IgnoreOfflineError)
                                                    {
                                                        if (!await url.CheckIfOnlineAsync())
                                                        {
                                                            url.LastTestedTime = now;
                                                            url.LastSkippedReason = url.LastSkippedReason.Prepend("NotOnline");
                                                            notonline++;
                                                            continue;
                                                        }
                                                    }
                                                    if (url.CurErrCount == 0)
                                                    {

                                                        if (url.UseOnlyAsLinkedServer && (GetRefinementServer || (!GetLinkedRequiredList)))
                                                        {
                                                            url.LastTestedTime = now;
                                                            url.LastSkippedReason = url.LastSkippedReason.Prepend("UseOnlyAsLinked");
                                                            onlylinked++;
                                                            continue;
                                                        }

                                                        if (GetRefinementServer)
                                                        {
                                                            if (url.RefinementUseCurrentlyValid)
                                                            {
                                                                url.LastResultMessage = "Working [refinement]...";
                                                                url.LastSkippedReason = url.LastSkippedReason.Prepend("[UsedRefine]");
                                                                url.CurOrder = i + 1;
                                                                url.IncrementQueue(now);
                                                                FoundRefinementCount++;
                                                                ret.Add(url);
                                                                // Dont break out of loop since we may need more than one refinement server
                                                            }
                                                            else
                                                            {
                                                                url.LastTestedTime = now;
                                                                if (!url.UseAsRefinementServer)
                                                                    url.LastSkippedReason = url.LastSkippedReason.Prepend("RefineNotRequired");
                                                            }

                                                        }
                                                        else if (GetLinkedRequiredList)
                                                        {

                                                            if (Global.IsInList(url.ToString(), LinkedRequiredURLs, TrueIfEmpty: false))
                                                            {
                                                                url.LastResultMessage = "Working [Linked]...";
                                                                url.LastSkippedReason = url.LastSkippedReason.Prepend("[UsedLinked]");
                                                                url.CurOrder = i + 1;
                                                                url.IncrementQueue(now);
                                                                FoundRequiredCount++;
                                                                ret.Add(url);
                                                                //dont break out of loop since we may need more than one linked/required URL
                                                            }
                                                            else
                                                            {
                                                                url.LastTestedTime = now;
                                                                if (!url.UseAsRefinementServer) ;
                                                                url.LastSkippedReason = url.LastSkippedReason.Prepend("RefineNotRequired");
                                                                notrequired++;
                                                            }
                                                        }
                                                        else if (!url.UseAsRefinementServer)
                                                        {
                                                            url.LastResultMessage = "Working...";
                                                            url.LastSkippedReason = url.LastSkippedReason.Prepend("[Used]");
                                                            url.CurOrder = i + 1;
                                                            url.IncrementQueue(now);
                                                            ret.Add(url);
                                                            break;
                                                        }
                                                        else
                                                        {
                                                            url.LastTestedTime = now;
                                                            url.LastSkippedReason = url.LastSkippedReason.Prepend("NoConditionsMet?");
                                                        }
                                                    }
                                                    else
                                                    {
                                                        double secs = Math.Round((now - url.LastUsedTime).TotalSeconds, 0);
                                                        if (secs >= AppSettings.Settings.MinSecondsBetweenFailedURLRetry)
                                                        {
                                                            url.LastResultMessage = "Working...";
                                                            url.LastSkippedReason = url.LastSkippedReason.Prepend("[UsedAfterRetry]");
                                                            url.CurOrder = i + 1;
                                                            url.IncrementQueue(now);
                                                            ret.Add(url);
                                                            if (!displayedretry)  //if we get in a long loop waiting for URL
                                                            {
                                                                Log($"---- Trying previously failed URL again after {secs} seconds. (ErrCount={url.CurErrCount}, Setting 'MinSecondsBetweenFailedURLRetry'={AppSettings.Settings.MinSecondsBetweenFailedURLRetry}): {url}");
                                                                displayedretry = true;
                                                            }
                                                            break;
                                                        }
                                                        else
                                                        {
                                                            url.LastTestedTime = now;
                                                            url.LastSkippedReason = url.LastSkippedReason.Prepend("WaitingForRetry");
                                                            if (!displayedbad)  //if we get in a long loop waiting for URL
                                                            {
                                                                Log($"---- Waiting {AppSettings.Settings.MinSecondsBetweenFailedURLRetry - secs} seconds before retrying bad URL. (ErrCount={url.CurErrCount} of {AppSettings.Settings.MaxQueueItemRetries}, Setting 'MinSecondsBetweenFailedURLRetry'={AppSettings.Settings.MinSecondsBetweenFailedURLRetry}): {url}");
                                                                displayedbad = true;
                                                            }
                                                        }

                                                    }
                                                }
                                                else
                                                {
                                                    url.LastTestedTime = now;
                                                    url.LastSkippedReason = url.LastSkippedReason.Prepend("NotInTimeRange");
                                                    notintimerange++;
                                                }

                                            }
                                            else
                                            {
                                                url.LastTestedTime = now;
                                                url.LastSkippedReason = url.LastSkippedReason.Prepend("MaxImagesPerMonthMet");
                                                maxpermonth++;
                                            }

                                        }
                                        else
                                        {
                                            url.LastTestedTime = now;
                                            url.LastSkippedReason = url.LastSkippedReason.Prepend("NotRefinementServer");
                                            notrefined++;
                                        }

                                    }
                                    else
                                    {
                                        url.LastTestedTime = now;
                                        url.LastSkippedReason = url.LastSkippedReason.Prepend("NotInCamList");
                                        incorrectcam++;
                                    }

                                }
                                else
                                {
                                    url.LastTestedTime = now;
                                    url.LastSkippedReason = url.LastSkippedReason.Prepend(url.NotReadyReason);
                                    inuse++;

                                    if (!GetRefinementServer && !GetLinkedRequiredList) { int debugging = 0; }

                                }
                            }
                            //disabled, but check to see if we need to reenable
                            else
                            {
                                double mins = (DateTime.Now - url.LastUsedTime).TotalMinutes;
                                url.LastTestedTime = now;
                                url.LastSkippedReason = url.LastSkippedReason.Prepend("ErrDisabled");

                                disabled++;
                                if (mins >= AppSettings.Settings.URLResetAfterDisabledMinutes)
                                {
                                    //check to see if can be re-enabled yet
                                    url.ErrDisabled = false;
                                    url.CurErrCount = 0;
                                    url.DecrementQueue();
                                    url.LastResultMessage = "(Re-enabled)";

                                    Log($"---- Re-enabling disabled URL because {AppSettings.Settings.URLResetAfterDisabledMinutes} (URLResetAfterDisabledMinutes) minutes have passed: " + url);
                                    url.CurOrder = i + 1;
                                    url.IncrementQueue();
                                    ret.Add(url);
                                    break;
                                }
                            }
                        }

                    }
                    catch (Exception ex)
                    {
                        Log("Error: getting next URL: " + ex.ToString());
                    }


                    if ((GetLinkedRequiredList && (FoundRequiredCount >= LinkedRequiredURLs.Count)) ||
                        (GetRefinementServer && (FoundRefinementCount >= RefineMatchCount)))
                    {
                        break;
                    }

                    if ((GetRefinementServer || GetLinkedRequiredList) && sw.ElapsedMilliseconds >= AppSettings.Settings.MaxWaitForAIServerMS)
                    {
                        string ew = "Debug:";
                        if (AppSettings.Settings.MaxWaitForAIServerTimeoutError)
                            ew = "Error:";

                        if (GetRefinementServer)
                            Log($"{ew} ---- URL request for REFINEMENT AI Server timed out, but only '{ret.Count}' of '{RefineMatchCount}' available. ({sw.ElapsedMilliseconds}ms - Setting in AITOOL.SETTINGS.JSON 'MaxWaitForAIServerMS' and is set to {AppSettings.Settings.MaxWaitForAIServerMS})");
                        else if (GetLinkedRequiredList)
                            Log($"{ew} ---- URL request for LINKED AI Server timed out, but only '{ret.Count}' of '{LinkedRequiredURLs.Count}' available. ({sw.ElapsedMilliseconds}ms) - Setting in AITOOL.SETTINGS.JSON 'MaxWaitForAIServerMS' and is set to {AppSettings.Settings.MaxWaitForAIServerMS})");
                        break;
                    }

                    //otherwise
                    if (ret.Count > 0)
                    {
                        break;
                    }

                    if ((DateTime.Now - LastWaitingLog).TotalMinutes >= 5)
                    {
                        Log($"---- All URL's are in use, disabled, camera name doesnt match or time range was not met.  ({inuse} inuse, {disabled} disabled, {incorrectcam} wrong camera, {notintimerange} not in time range, {maxpermonth} at max per month limit, {notrefined} not refinement server, {onlylinked} only use as linked server) Waiting...");
                        LastWaitingLog = DateTime.Now;
                    }


                    //short wait
                    await Task.Delay(AppSettings.Settings.loop_delay_ms);

                }

                //=====================================================================================================

                sw.Stop();


                //see if any servers have 'LINKED' servers
                if (!GetRefinementServer && !GetLinkedRequiredList && ret.Count > 0)
                {
                    List<ClsURLItem> linked = new List<ClsURLItem>();
                    foreach (ClsURLItem url in ret)
                    {
                        if (url.LinkServerResults && !string.IsNullOrEmpty(url.LinkedResultsServerList))
                        {
                            linked.AddRange(await WaitForNextURL(cam, false, null, url.LinkedResultsServerList));
                        }
                    }
                    if (linked.Count > 0)
                    {
                        Log($"Debug: ---- Found '{linked.Count}' linked AI URL's.");
                        ret.AddRange(linked);
                    }
                }


            }
            catch (Exception ex)
            {

                Log($"Error: {ex.Msg()}");
            }

            //remove any dupes just in case
            ret = ret.Distinct().ToList();


            return ret;


        }

        //public static ClsURLItem GetURL(string urlstring)
        //{
        //    ClsURLItem ret = null;

        //    if (!urlstring.Contains("//"))
        //    {
        //        foreach (var url in AppSettings.Settings.AIURLList)
        //        {
        //            if (urlstring.IndexOf(url.CurSrv, StringComparison.OrdinalIgnoreCase) >= 0)
        //            {
        //                return url;
        //            }
        //        }
        //        return ret;
        //    }

        //    //first look for exact match - where more than just the URL should match
        //    ClsURLItem test = new ClsURLItem(urlstring, 0, URLTypeEnum.Unknown);

        //    int fnd = AppSettings.Settings.AIURLList.IndexOf(test);

        //    if (fnd > -1)
        //    {
        //        ret = AppSettings.Settings.AIURLList[fnd];
        //    }
        //    else  //lets do a loose search for just the URL string 
        //    {
        //        for (int i = 0; i < AppSettings.Settings.AIURLList.Count; i++)
        //        {
        //            if (!AppSettings.Settings.AIURLList[i].Enabled)
        //                continue;

        //            if (string.Equals(AppSettings.Settings.AIURLList[i].ToString(), test.ToString(), StringComparison.OrdinalIgnoreCase))
        //            {
        //                ret = AppSettings.Settings.AIURLList[i];
        //                break;
        //            }
        //        }
        //    }

        //    return ret;

        //}

        public static ClsURLItem GetURL(string NameOrURLOrPartialString, bool ReturnDefault, bool OnlyEnabled)
        {
            NameOrURLOrPartialString = NameOrURLOrPartialString.Trim();

            if (AppSettings.Settings.AIURLList.IsEmpty())
                return null;

            List<ClsURLItem> urls = new List<ClsURLItem>();

            //first look for exact match - where more than just the URL should match
            foreach (ClsURLItem url in AppSettings.Settings.AIURLList)
            {
                if ((!OnlyEnabled || OnlyEnabled && url.Enabled) && url.Name.EqualsIgnoreCase(NameOrURLOrPartialString))
                    urls.Add(url);
            }
            foreach (ClsURLItem url in AppSettings.Settings.AIURLList)
            {
                if ((!OnlyEnabled || OnlyEnabled && url.Enabled) && url.url.EqualsIgnoreCase(NameOrURLOrPartialString))
                    urls.Add(url);
            }
            // lets look for a partial match in the URL (like if we are looking for a hostname or ip)
            foreach (ClsURLItem url in AppSettings.Settings.AIURLList)
            {
                if ((!OnlyEnabled || OnlyEnabled && url.Enabled) && url.url.Has(NameOrURLOrPartialString))
                    urls.Add(url);
            }

            //ability to search by type
            foreach (ClsURLItem url in AppSettings.Settings.AIURLList)
            {
                if ((!OnlyEnabled || OnlyEnabled && url.Enabled) && url.Type.ToString().EqualsIgnoreCase(NameOrURLOrPartialString))
                    urls.Add(url);
            }

            if (urls.IsEmpty())
            {
                Log($"Trace: No enabled AI URL with the same name or partial URL match found for '{NameOrURLOrPartialString}'.");
                //check if there is a default camera which accepts any prefix, select it
                if (ReturnDefault)
                {
                    //return the first enabled url
                    foreach (ClsURLItem url in AppSettings.Settings.AIURLList)
                    {
                        if (!OnlyEnabled || OnlyEnabled && url.Enabled)
                            urls.Add(url);
                    }
                }
            }

            if (urls.IsEmpty())
                return null;

            if (urls.Count > 1)
            {
                Log($"Trace: Multiple enabled AI URLs found for '{NameOrURLOrPartialString}'.  Using the first one found: {urls[0].Name}: {urls[0]}");
            }

            return urls[0];

        }
        public static Camera GetCamera(String ImageOrNameOrPrefix, bool ReturnDefault = true)
        {
            //using var Trace = new Trace();  //This c# 8.0 using feature will auto dispose when the function is done.


            //we got here too early, no warning messages
            if (AppSettings.Settings.CameraList.Count == 0)
                return null;

            List<Camera> cams = new List<Camera>();

            try
            {
                ImageOrNameOrPrefix = ImageOrNameOrPrefix.Trim();

                //search by path or filename prefix if we are passed a full path to image file
                if (ImageOrNameOrPrefix.Contains("\\"))
                {
                    string pth = Path.GetDirectoryName(ImageOrNameOrPrefix);
                    string fname = Path.GetFileNameWithoutExtension(ImageOrNameOrPrefix);

                    //&CAM.%Y%m%d_%H%M%S
                    //AIFOSCAMDRIVEWAY.20200827_131840312.jpg
                    //sgrtgrdg - Kopie (2).jpg

                    //Dont try to break the filename apart, just look at any characters matching the first part of the filename

                    //first look for . or - at end of prefix if not specified.   So CAM1 prefix wont match CAM12
                    foreach (Camera ccam in AppSettings.Settings.CameraList)
                    {
                        //if (!ccam.enabled)
                        //    continue;

                        if (ccam.Prefix.Contains("*") || ccam.Prefix.Contains("?"))
                        {
                            if (Regex.IsMatch(Global.WildCardToRegular(ccam.Prefix), ImageOrNameOrPrefix, RegexOptions.IgnoreCase) || Regex.IsMatch(Global.WildCardToRegular(ccam.Prefix), fname, RegexOptions.IgnoreCase))
                            {
                                if (!cams.Contains(ccam))
                                {
                                    ccam.LastGetCameraMatchResult = "(Regex)";
                                    cams.Add(ccam);
                                }
                            }
                        }
                        else if (ccam.Prefix.EndsWith("-") || ccam.Prefix.EndsWith("."))
                        {
                            if (fname.StartsWith(ccam.Prefix.Trim(), StringComparison.OrdinalIgnoreCase))
                            {
                                {
                                    if (!cams.Contains(ccam))
                                    {
                                        ccam.LastGetCameraMatchResult = "(StartsWith-Prefix-Dot)";
                                        cams.Add(ccam);
                                    }
                                }
                            }
                        }
                        else if (!string.IsNullOrWhiteSpace(ccam.Prefix))
                        {
                            //&CAM.A.%Y%m%d_%H%M%S
                            //AIFOSCAMDRIVEWAY.A.20200827_131840312

                            //loop through so that we always check a match with the LONGEST prefix (between 2 dots), before the shorter ones
                            string tmpfname = fname;
                            int meno = 0;
                            do
                            {
                                meno = tmpfname.LastIndexOf(".");
                                if (meno > 0)
                                {
                                    tmpfname = fname.Substring(0, meno);
                                    if (tmpfname.Equals(ccam.Prefix.Trim(), StringComparison.OrdinalIgnoreCase))
                                    {
                                        if (!cams.Contains(ccam))
                                        {
                                            ccam.LastGetCameraMatchResult = "(Equals-Prefix-AnyDot)";
                                            cams.Add(ccam);
                                        }
                                    }

                                }

                            } while (meno > 0);
                        }
                    }

                    //regular search
                    foreach (Camera ccam in AppSettings.Settings.CameraList)
                    {
                        //if (!ccam.enabled)
                        //    continue;

                        if (!string.IsNullOrWhiteSpace(ccam.Prefix) && fname.StartsWith(ccam.Prefix.Trim(), StringComparison.OrdinalIgnoreCase))
                        {
                            if (!cams.Contains(ccam))
                            {
                                ccam.LastGetCameraMatchResult = "(StartsWith-Prefix)";
                                cams.Add(ccam);
                            }
                        }
                    }


                    //if it is not found, search by the camera input path
                    foreach (Camera ccam in AppSettings.Settings.CameraList)
                    {
                        //if (!ccam.enabled)
                        //    continue;

                        //If the watched path is c:\bi\cameraname but the full path of found file is 
                        //                       c:\bi\cameraname\date\time\randomefilename.jpg 
                        //we just check the beginning of the path
                        if (!String.IsNullOrWhiteSpace(ccam.input_path) && ccam.input_path.Trim().StartsWith(pth, StringComparison.OrdinalIgnoreCase))
                        {
                            if (!cams.Contains(ccam))
                            {
                                ccam.LastGetCameraMatchResult = "(StartsWith-InputPath)";
                                cams.Add(ccam);
                            }
                        }
                    }


                }
                else
                {
                    //find by name - we dont care if the camera is disabled
                    foreach (Camera ccam in AppSettings.Settings.CameraList)
                    {
                        if (ImageOrNameOrPrefix.Equals(ccam.Name, StringComparison.OrdinalIgnoreCase))
                        {
                            if (!cams.Contains(ccam))
                            {
                                ccam.LastGetCameraMatchResult = "(ByAICamName)";
                                cams.Add(ccam);
                            }
                        }
                    }
                    ////find by actual cam name if we have to
                    //foreach (Camera ccam in AppSettings.Settings.CameraList)
                    //{
                    //    if (ImageOrNameOrPrefix.Equals(ccam.BICamName, StringComparison.OrdinalIgnoreCase))
                    //    {
                    //        if (!cams.Contains(ccam))
                    //        {
                    //            ccam.LastGetCameraMatchResult = "(ByBICamName?)";
                    //            cams.Add(ccam);
                    //        }
                    //    }
                    //}

                }


                //if we didnt find a camera see if there is a default camera name we can use without a prefix
                if (cams.Count == 0)
                {
                    Log($"Debug: No enabled camera with the same filename, cameraname, or prefix found for '{ImageOrNameOrPrefix}'");
                    //check if there is a default camera which accepts any prefix, select it
                    if (ReturnDefault)
                    {

                        int i = AppSettings.Settings.CameraList.FindIndex(x => string.Equals(x.Name.Trim(), "default", StringComparison.OrdinalIgnoreCase));

                        if (i == -1)
                            i = AppSettings.Settings.CameraList.FindIndex(x => x.Prefix.Trim() == "");

                        if (i > -1)
                        {
                            if (!cams.Contains(AppSettings.Settings.CameraList[i]))
                                cams.Add(AppSettings.Settings.CameraList[i]);

                            if (!ImageOrNameOrPrefix.EqualsIgnoreCase("none"))
                                Log($"Trace:(Found a DEFAULT camera for '{ImageOrNameOrPrefix}': '{AppSettings.Settings.CameraList[i].Name}')");
                        }
                        else
                        {
                            Log($"Debug: No default camera found. Aborting. (Out of {AppSettings.Settings.CameraList.Count} cameras.)");
                        }
                    }

                }

            }
            catch (Exception ex)
            {

                Log(ex.Msg());
            }

            Camera cam = null;

            if (cams.Count == 0)
            {
                Log($"Debug: Cannot match '{ImageOrNameOrPrefix}' to an existing camera. (Out of {AppSettings.Settings.CameraList.Count} cameras.)");
            }
            else
            {
                cam = cams[0];
                if (cams.Count > 1)
                {
                    Log($"Trace: *** Note: More than one configured camera matched '{ImageOrNameOrPrefix}', using the first one matched: '{cams[0].Name}' {cams[0].LastGetCameraMatchResult} ***");
                    for (int i = 0; i < cams.Count; i++)
                    {
                        Log($"Trace:    ----{i + 1}: Name='{cams[i].Name}', MatchResult={cams[i].LastGetCameraMatchResult}, BICamName={cams[i].BICamName}, Prefix='{cams[i].Prefix}', InputPath='{cams[i].input_path}'");
                    }
                }
            }

            return cam;

        }
    }
}
