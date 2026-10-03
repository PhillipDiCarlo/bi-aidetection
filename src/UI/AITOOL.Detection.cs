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

        //2.9 - crop before refinement: a refinement prediction whose PercentOfImage (relative to the crop it was
        //produced from) is at or above this is treated as describing the whole crop (eg a vision LLM's "Scene"
        //summary) rather than a localized sub-object within it.
        public const double RefinementWholeCropCoveragePercent = 65;

        /// <summary>
        /// 2.9 - crop before refinement: pads an object's rectangle by PaddingPercent of its own width/height on each
        /// side, then clamps the result to the image bounds. This is the crop sent to a per-object refinement server
        /// instead of the full frame.
        /// </summary>
        public static Rectangle GetRefinementCropRectangle(Rectangle ObjectRect, int ImageWidth, int ImageHeight, int PaddingPercent)
        {
            int padx = (ObjectRect.Width * PaddingPercent) / 100;
            int pady = (ObjectRect.Height * PaddingPercent) / 100;

            int left = Math.Max(0, ObjectRect.Left - padx);
            int top = Math.Max(0, ObjectRect.Top - pady);
            int right = Math.Min(ImageWidth, ObjectRect.Right + padx);
            int bottom = Math.Min(ImageHeight, ObjectRect.Bottom + pady);

            //guard against a zero-size crop (eg a zero-size object rect, or an image smaller than reported)
            if (right <= left)
                right = Math.Min(ImageWidth, left + 1);
            if (bottom <= top)
                bottom = Math.Min(ImageHeight, top + 1);

            return Rectangle.FromLTRB(left, top, right, bottom);
        }

        /// <summary>
        /// 2.9 - crop before refinement: maps a rectangle that is relative to a crop (0,0 at the crop's top-left)
        /// back to full-image pixel coordinates.
        /// </summary>
        public static Rectangle MapCropRelativeRectToFullImage(Rectangle CropArea, Rectangle CropRelativeRect)
        {
            return new Rectangle(CropArea.X + CropRelativeRect.X, CropArea.Y + CropRelativeRect.Y, CropRelativeRect.Width, CropRelativeRect.Height);
        }

        /// <summary>
        /// True if Pred is a relevant prediction whose object type/label is one that RefinementObjects says a
        /// refinement server wants to see. Shared by WaitForNextURL() (decides which refinement servers are
        /// currently usable) and the per-object crop dispatch in DetectObjects() (2.9 - decides WHICH predictions
        /// from the current image to crop and send to a given refinement server).
        /// </summary>
        public static bool IsRefinementMatch(ClsPrediction Pred, string RefinementObjects)
        {
            if (Pred.Result != ResultType.Relevant)
                return false;

            if (RefinementObjects.Has("animal") && Pred.ObjType == ObjectType.Animal)
                return true;
            else if ((RefinementObjects.Has("person") || RefinementObjects.Has("people")) && Pred.ObjType == ObjectType.Person)
                return true;
            else if (RefinementObjects.Has("vehicle") && Pred.ObjType == ObjectType.Vehicle)
                return true;
            else if (Global.IsInList(Pred.Label, RefinementObjects))
                return true;

            return false;
        }


        public class ClsAIServerResponse
        {
            public List<ClsPrediction> Predictions = new List<ClsPrediction>();
            public bool Success = false;
            public string JsonString = "";
            public string Message = "";
            public string Error = "";
            public long SWPostTime = 0;
            public HttpStatusCode StatusCode = HttpStatusCode.Unused;
            public ClsURLItem AIURL = null;
        }

        public static async Task<ClsAIServerResponse> GetDetectionsFromAIServer(ClsImageQueueItem CurImg, ClsURLItem AiUrl, Camera cam)
        {
            using var Trace = new Trace();  //This c# 8.0 using feature will auto dispose when the function is done.

            AiUrl.LastResultMessage = "";
            AiUrl.LastResultSuccess = false;

            ClsAIServerResponse ret;

            AIProviders.IAIProvider provider = AIProviders.AIProviderRegistry.Get(AiUrl.Type);

            if (provider != null)
            {
                ret = await provider.DetectAsync(CurImg, AiUrl, cam, MasterCTS.Token);
            }
            else
            {
                ret = new ClsAIServerResponse();
                ret.AIURL = AiUrl;
                ret.Error = $"Error: URL type not supported yet: '{AiUrl.Type}'";
                AiUrl.LastResultMessage = ret.Error;
            }

            AiUrl.LastResultSuccess = ret.Success || !AiUrl.LastResultMessage.Has("error");

            return ret;

        }


        public static List<ClsPrediction> RemovePredictionDuplicates(List<ClsPrediction> items, Camera cam)
        {
            List<ClsPrediction> result = new List<ClsPrediction>();
            for (int i = 0; i < items.Count; i++)
            {
                // Assume not duplicate.
                bool duplicate = false;
                for (int z = 0; z < i; z++)
                {
                    if (items[z] == items[i] && items[z].ConfidenceString() == items[i].ConfidenceString())
                    {
                        ObjectPosition op1 = new ObjectPosition(items[z].XMin, items[z].XMax, items[z].YMin, items[z].YMax, items[z].Label, items[z].ImageWidth, items[z].ImageHeight, items[z].Camera, items[z].Filename);
                        op1.ScaleConfig = cam.maskManager.ScaleConfig;
                        op1.PercentMatch = cam.maskManager.PercentMatch;
                        ObjectPosition op2 = new ObjectPosition(items[i].XMin, items[i].XMax, items[i].YMin, items[i].YMax, items[i].Label, items[i].ImageWidth, items[i].ImageHeight, items[i].Camera, items[i].Filename);
                        op2.ScaleConfig = cam.maskManager.ScaleConfig;
                        op2.PercentMatch = cam.maskManager.PercentMatch;

                        if (op1 == op2)
                        {
                            // This is a duplicate.
                            duplicate = true;
                            break;
                        }
                    }
                }
                // If not duplicate, add to result.
                if (!duplicate)
                {
                    result.Add(items[i]);
                }
            }
            return result;
        }

        public class ClsPredMatch
        {
            public int Idx = -1;
            public double MatchPercent = 0;
            public List<ClsPrediction> preds = new List<ClsPrediction>();


            public ClsPredMatch(int idx, double matchPercent)
            {
                Idx = idx;
                MatchPercent = matchPercent;

            }
            public ClsPredMatch() { }
        }
        //search for position based on object position
        public static ClsPredMatch ContainsPrediction(ClsPrediction pred, List<ClsPrediction> predictions, Camera cam, bool ObjTypeMustMatch, bool TrueIfInsideOrPartiallyInside)
        {
            using var Trace = new Trace();  //This c# 8.0 using feature will auto dispose when the function is done.



            ClsPredMatch ret = new ClsPredMatch();

            if (predictions.Count == 0)
                return ret;

            ObjectPosition op1 = new ObjectPosition(pred.XMin, pred.XMax, pred.YMin, pred.YMax, pred.Label, pred.ImageWidth, pred.ImageHeight, pred.Camera, pred.Filename);
            op1.ScaleConfig = cam.maskManager.ScaleConfig;
            op1.PercentMatch = cam.maskManager.PercentMatch;

            for (int i = 0; i < predictions.Count; i++)
            {
                //nope out so we dont include ourself?
                //if (predictions[i].GetHashCode() == pred.GetHashCode())
                //    break;

                if (TrueIfInsideOrPartiallyInside)
                {
                    //for face matching, we dont care if it is not a closely matching rectangle size, in fact it should be smaller but mostly within
                    if (RectangleMatches(predictions[i].GetRectangle(), pred.GetRectangle(), cam.MergePredictionsMinMatchPercent, out double matched, TrueIfInsideOrPartiallyInside))
                    {
                        if (!ObjTypeMustMatch || ObjTypeMustMatch && (predictions[i].ObjType == pred.ObjType || predictions[i].Label.IndexOf(pred.Label, StringComparison.OrdinalIgnoreCase) >= 0))
                        {
                            predictions[i].PercentMatchRefinement = matched;
                            ret.preds.Add(predictions[i]);
                        }
                    }

                }
                else  // to see if we can find a similar sized rectangle using the maskmanager's technique of matching roughly the same size
                {
                    ObjectPosition op2 = new ObjectPosition(predictions[i].XMin, predictions[i].XMax, predictions[i].YMin, predictions[i].YMax, predictions[i].Label, predictions[i].ImageWidth, predictions[i].ImageHeight, predictions[i].Camera, predictions[i].Filename);
                    op2.ScaleConfig = cam.maskManager.ScaleConfig;
                    op2.PercentMatch = cam.MergePredictionsMinMatchPercent;  //cam.maskManager.PercentMatch;
                    if (op1 == op2 && (!ObjTypeMustMatch || ObjTypeMustMatch && (predictions[i].ObjType == pred.ObjType || predictions[i].Label.IndexOf(pred.Label, StringComparison.OrdinalIgnoreCase) >= 0)))
                    {
                        predictions[i].PercentMatchRefinement = op1.LastPercentMatch;
                        ret.preds.Add(predictions[i]);
                    }

                }
            }

            //send only best match
            if (ret.preds.Count > 0)
            {
                //sort so highest match is first:
                ret.preds = ret.preds.OrderByDescending(p => p.PercentMatchRefinement).ToList();
                ret.MatchPercent = ret.preds[0].PercentMatchRefinement;
                pred.PercentMatchRefinement = ret.MatchPercent;
            }


            return ret;
        }

        public static bool RectangleMatches(Rectangle MasterRect, Rectangle CompareRect, double PercentMatch, out double MatchedPercent, bool TrueIfInsideOrPartiallyInside)
        {

            MatchedPercent = MasterRect.IntersectPercent(CompareRect);

            if (TrueIfInsideOrPartiallyInside)
            {
                if (MasterRect.IntersectsWith(CompareRect) || MasterRect.Contains(CompareRect))
                {

                    //trying to match a face in a person rectangle.  The face rectangle can be partially outside the person rectangle so just using Contains doesnt always work.  May need to tweak lower and upper
                    if (MatchedPercent >= 5 && MatchedPercent <= 95)
                        return true;
                }

            }
            else
            {
                if (MatchedPercent >= PercentMatch)
                    return true;
            }

            return false;
        }





        public class DetectObjectsResult
        {
            public bool Success = false;
            public string Error = "";
            public string Message = "";
            public List<ClsURLItem> OutURLs = new List<ClsURLItem>();
            public List<ClsPrediction> OutPredictions = new List<ClsPrediction>();
            public int TimeMS = 0;
        }
        //analyze image with AI
        public static async Task<DetectObjectsResult> DetectObjects(ClsImageQueueItem CurImg, List<ClsURLItem> InAiUrls, Camera cam = null)
        {

            using var Trace = new Trace();  //This c# 8.0 using feature will auto dispose when the function is done.

            DetectObjectsResult ret = new DetectObjectsResult();
            ret.OutURLs = InAiUrls;

            foreach (var url in ret.OutURLs)
            {
                url.IncrementQueue();
            }

            //Only set error when there IS an error...

            string filename = Path.GetFileName(CurImg.image_path);

            CurImg.QueueWaitMS = (long)(DateTime.Now - CurImg.TimeAdded).TotalMilliseconds;

            Stopwatch sw = Stopwatch.StartNew();

            long TotalSWPostTime = 0;

            if (cam == null)
                cam = AITOOL.GetCamera(CurImg.image_path);

            cam.last_image_file = CurImg.image_path;

            History hist = null;

            // check if camera is still in the first half of the cooldown. If yes, don't analyze to minimize cpu load.
            //only analyze if 50% of the cameras cooldown time since last detection has passed
            double secs = (DateTime.Now - cam.last_trigger_time).TotalSeconds;
            double halfcool = cam.cooldown_time_seconds / 2;

            //ClsAIServerResponse asr = new ClsAIServerResponse();
            ClsAIServerResponse[] asrs = new ClsAIServerResponse[] { };
            string AISRV = "";

            foreach (var url in ret.OutURLs)
                AISRV += url.CurSrv + ";";

            AISRV = AISRV.Trim(";".ToCharArray());

            ClsURLItem AiUrl = ret.OutURLs[0];  //default to first one just to have something set

            if (secs >= halfcool)
            {
                try
                {

                    Log($"Debug: Starting analysis of {CurImg.image_path}...", AISRV, cam, CurImg);

                    if (CurImg.IsValid())  //Waits for access and loads into memory if not already loaded
                    {
                        cam.UpdateImageResolutions(CurImg);

                        Log($"Debug: (Image resolution={CurImg.Width}x{CurImg.Height} @ {CurImg.DPI} DPI and {Global.FormatBytes(CurImg.FileSize)})", AISRV, cam, CurImg);

                        string fldr = Path.Combine(Path.GetDirectoryName(AppSettings.Settings.SettingsFileName), "LastCamImages");
                        string file = Path.Combine(fldr, $"{cam.Name}-Last.jpg");
                        //Create a copy of the current image for use in mask manager when the original image was deleted
                        if ((DateTime.Now - LastImageBackupTime).TotalMinutes >= 60 || !File.Exists(file))
                        {
                            //File.Copy(CurImg.image_path, file, true);
                            CurImg.CopyFileTo(file);
                            LastImageBackupTime = DateTime.Now;
                        }


                        ///====================================================================================================================
                        ///Get the initial predictions=========================================================================================
                        ///====================================================================================================================

                        //Start processing all urls that may be linked at the same time in separate threads
                        List<Task<ClsAIServerResponse>> urltasks = new List<Task<ClsAIServerResponse>>();

                        bool HasLinked = false;
                        foreach (ClsURLItem url in ret.OutURLs)
                        {
                            if (url.LinkServerResults && !url.LinkedResultsServerList.IsEmpty())
                                HasLinked = true;

                            urltasks.Add(Task.Run(() => GetDetectionsFromAIServer(CurImg, url, cam)));
                        }

                        asrs = await Task.WhenAll(urltasks);

                        List<ClsPrediction> initialpredictions = new List<ClsPrediction>();

                        int order = 0;
                        int RelevantPredictionCount = 0;

                        foreach (ClsAIServerResponse asr in asrs)
                        {

                            TotalSWPostTime += asr.SWPostTime;

                            bool IsPrimary = (urltasks.Count == 1) || !HasLinked || (HasLinked && !asr.AIURL.LinkedResultsServerList.IsEmpty());
                            string primdet = "";
                            if (IsPrimary)
                                primdet = "[Primary]";
                            else
                                primdet = "[Linked]";

                            Log($"Debug: (2/6) {primdet} Posted in {asr.SWPostTime}ms, StatusCode='{asr.StatusCode}', Received a {asr.JsonString.Length} byte JSON response: '{asr.JsonString.Truncate()}'", AiUrl.CurSrv, cam, CurImg);
                            Log($"Debug: (3/6) {primdet} Processing {asr.Predictions.Count} results...", AiUrl.CurSrv, cam, CurImg);


                            if (asr.Success)
                            {

                                foreach (ClsPrediction pred in asr.Predictions)
                                {
                                    order++;
                                    pred.AnalyzePrediction(SkipDynamicMaskCheck: true);
                                    if (pred.Result == ResultType.Relevant)
                                        RelevantPredictionCount++;
                                    pred.ServerType = IsPrimary ? ServerType.Primary : ServerType.Linked;
                                    AiUrl = asr.AIURL;
                                    AiUrl.LastResultSuccess = asr.Success;
                                    pred.OriginalOrder = order;
                                    initialpredictions.Add(pred);
                                }
                            }
                            else
                            {
                                ret.Error = asr.Error;
                                //AiUrl.IncrementError();
                                //AiUrl.LastResultMessage = ret.Error;
                                //Only send error if MaxWaitForAIServerTimeoutError is true
                                string ew = "Debug:";
                                if (AppSettings.Settings.MaxWaitForAIServerTimeoutError)
                                    ew = "Error:";
                                if (ret.Error.Has("request timed out"))
                                {
                                    Log(ew + ret.Error, AiUrl.CurSrv, cam, CurImg);
                                }
                                else
                                {
                                    Log(ret.Error, AiUrl.CurSrv, cam, CurImg);
                                }
                            }
                        }


                        ///====================================================================================================================
                        ///Get the refinement predictions======================================================================================
                        ///====================================================================================================================

                        List<ClsPrediction> refinepredictions = new List<ClsPrediction>();


                        //First check and see if the main or linked servers contained any refinement objects, if so, add them to the list:
                        foreach (ClsPrediction pred in initialpredictions)
                        {
                            if (pred.ObjType == ObjectType.Face || pred.ObjType == ObjectType.LicensePlate)
                                refinepredictions.Add(pred);
                        }

                        //see if there are any refinement servers we need to ask for info...
                        if (AIURLListAvailableRefineServerCount > 0 && RelevantPredictionCount > 0)
                        {
                            List<ClsURLItem> RefineURLs = await WaitForNextURL(cam, true, initialpredictions, "", ret.OutURLs);
                            if (RefineURLs.Count > 0)
                            {
                                //Start processing all refinement urls.  2.9 - crop before refinement: a server with
                                //RefinementCrop enabled gets one call PER matching object (a padded crop of just that
                                //object) instead of a single call with the full frame.
                                urltasks = new List<Task<ClsAIServerResponse>>();

                                //parallel to urltasks: the object a crop call was made for (and the crop rectangle/temp
                                //file used), so the results can be mapped back to full-image coordinates once the call
                                //completes and the temp file can be cleaned up. Null MatchedPred = a normal full-frame call.
                                List<(ClsPrediction MatchedPred, Rectangle CropArea, ClsImageQueueItem CropImg, string TempImagePath)> refinetaskinfo =
                                    new List<(ClsPrediction MatchedPred, Rectangle CropArea, ClsImageQueueItem CropImg, string TempImagePath)>();

                                foreach (ClsURLItem url in RefineURLs)
                                {
                                    List<ClsPrediction> cropmatches = (url.RefinementCrop ?? false)
                                        ? initialpredictions.Where(p => IsRefinementMatch(p, url.RefinementObjects)).ToList()
                                        : new List<ClsPrediction>();

                                    if (cropmatches.Count == 0)
                                    {
                                        //either this server doesn't use crop-before-refinement, or (shouldn't normally
                                        //happen since WaitForNextURL already required a match) nothing currently
                                        //matches - fall back to the full frame
                                        urltasks.Add(Task.Run(() => GetDetectionsFromAIServer(CurImg, url, cam)));
                                        refinetaskinfo.Add((null, Rectangle.Empty, null, null));
                                        continue;
                                    }

                                    foreach (ClsPrediction matched in cropmatches)
                                    {
                                        Rectangle objectrect = matched.GetRectangle();
                                        Rectangle croparea = GetRefinementCropRectangle(objectrect, CurImg.Width, CurImg.Height, url.RefinementCropPaddingPercent);

                                        System.Drawing.Image cropimg = CropImage(CurImg, croparea);
                                        if (cropimg == null)
                                        {
                                            Log($"Debug: [Refinement] Could not crop '{matched.Label}' {objectrect} for '{url.CurSrv}', using full frame instead.", url.CurSrv, cam, CurImg);
                                            urltasks.Add(Task.Run(() => GetDetectionsFromAIServer(CurImg, url, cam)));
                                            refinetaskinfo.Add((null, Rectangle.Empty, null, null));
                                            continue;
                                        }

                                        string tmppath = Path.Combine(Global.GetTempFolder(), $"{cam.Name}.Refine.{Guid.NewGuid():N}.jpg");
                                        cropimg.Save(tmppath, System.Drawing.Imaging.ImageFormat.Jpeg);
                                        cropimg.Dispose();

                                        ClsImageQueueItem cropqueueitem = new ClsImageQueueItem(tmppath, 0);

                                        urltasks.Add(Task.Run(() => GetDetectionsFromAIServer(cropqueueitem, url, cam)));
                                        refinetaskinfo.Add((matched, croparea, cropqueueitem, tmppath));
                                    }
                                }

                                asrs = await Task.WhenAll(urltasks);

                                int refineorder = 0;

                                for (int t = 0; t < asrs.Length; t++)
                                {
                                    ClsAIServerResponse asr = asrs[t];
                                    var info = refinetaskinfo[t];

                                    AiUrl = asr.AIURL;
                                    AiUrl.LastResultSuccess = asr.Success;
                                    TotalSWPostTime += asr.SWPostTime;

                                    //add to the list of url's we called
                                    if (!ret.OutURLs.Contains(AiUrl))
                                        ret.OutURLs.Add(AiUrl);

                                    Log($"Debug: (2.1/6) [Refinement Server] Posted in {asr.SWPostTime}ms, StatusCode='{asr.StatusCode}', Received a {asr.JsonString.Length} byte JSON response: '{asr.JsonString.Truncate()}'", AiUrl.CurSrv, cam, CurImg);
                                    Log($"Debug: (3.1/6) [Refinement Server] Processing {asr.Predictions.Count} results...", AiUrl.CurSrv, cam, CurImg);


                                    if (asr.Success)
                                    {

                                        foreach (ClsPrediction pred in asr.Predictions)
                                        {
                                            refineorder++;
                                            pred.ServerType = ServerType.Refine;
                                            pred.OriginalOrder = order;

                                            bool mergedintoobject = false;

                                            if (info.MatchedPred != null)
                                            {
                                                //this came from a per-object crop - decide BEFORE remapping, while
                                                //pred.PercentOfImage is still relative to the crop it was produced from
                                                bool coverswholecrop = pred.PercentOfImage >= RefinementWholeCropCoveragePercent;

                                                Rectangle fullrect = coverswholecrop
                                                    ? info.MatchedPred.GetRectangle()
                                                    : MapCropRelativeRectToFullImage(info.CropArea, pred.GetRectangle());

                                                //map back to full-image coordinates (and swap in the full image for mask
                                                //checks) BEFORE AnalyzePrediction() below, so it evaluates against the
                                                //camera's real mask coordinate space instead of the crop's
                                                pred.SetFullImageRectangle(fullrect, CurImg);

                                                if (coverswholecrop)
                                                {
                                                    //covers (almost) the entire crop - eg a vision LLM's "Scene" summary of
                                                    //the cropped object - so it describes the object itself. Merge its detail
                                                    //straight into that object instead of adding a near-duplicate prediction
                                                    //with the same rectangle (this is the whole point of cropping: "person:
                                                    //carrying a package" instead of a description of the whole yard).
                                                    info.MatchedPred.Detail = info.MatchedPred.Detail.Append(pred.Detail, "; ");
                                                    info.MatchedPred.RefineMergedCount++;
                                                    info.MatchedPred.PercentMatchRefinement = 100;
                                                    mergedintoobject = true;
                                                }
                                            }

                                            pred.AnalyzePrediction(SkipDynamicMaskCheck: true);

                                            if (mergedintoobject)
                                            {
                                                Log($"Debug: [Refinement]   Merged crop detail '{pred.Detail}' from {pred.Server} into {info.MatchedPred.Server} detection: {info.MatchedPred.ToString()} [{info.MatchedPred.Result}]", AiUrl.CurSrv, cam, CurImg);
                                                continue;
                                            }

                                            refinepredictions.Add(pred);
                                        }
                                    }
                                    else
                                    {
                                        ret.Error = asr.Error;
                                        //AiUrl.IncrementError();
                                        //AiUrl.LastResultMessage = ret.Error;
                                        Log(ret.Error, AiUrl.CurSrv, cam, CurImg);
                                    }

                                    if (info.TempImagePath.IsNotEmpty())
                                    {
                                        ((IDisposable)info.CropImg).Dispose();
                                        Global.SafeFileDelete(info.TempImagePath, "RefinementCrop");
                                    }
                                }


                            }
                        }

                        ///====================================================================================================================
                        ///merge the refinement predictions====================================================================================
                        ///====================================================================================================================


                        for (int i = 0; i < refinepredictions.Count; i++)
                        {
                            ClsPrediction RefinePred = refinepredictions[i];

                            Log($"Debug: [Refinement] Processing prediction #{i + 1} of {refinepredictions.Count} - {RefinePred.ToString()} [{RefinePred.Result}]...", AiUrl.CurSrv, cam, CurImg);

                            //See if there are any existing predictions that can be refined...
                            for (int r = 0; r < initialpredictions.Count; r++)
                            {
                                ClsPrediction ExistingPred = initialpredictions[r];
                                //sighthound and deepstack face detection is a rectangle around the face inside the person rectangle so it wont be an exact match.  Try to combine the face detail with person
                                if (RefinePred.ObjType == ObjectType.Face && ExistingPred.ObjType == ObjectType.Person)
                                {
                                    Rectangle personRect = ExistingPred.GetRectangle();
                                    Rectangle faceRect = RefinePred.GetRectangle();

                                    //check for at least an 80% match since various ai servers create different size rectangles 
                                    if (RectangleMatches(personRect, faceRect, cam.MergePredictionsMinMatchPercent, out double matched, true))
                                    {
                                        Log($"Debug: [Refinement]   Added face detail '{ExistingPred.Detail}' (r={r}) from {RefinePred.Server} for original {ExistingPred.Server} detection: {ExistingPred.ToString()} [{ExistingPred.Result}], Rectangle Match={matched.ToPercent()}");
                                        ExistingPred.PercentMatchRefinement = matched;
                                        ExistingPred.RefineMergedCount++;
                                        ExistingPred.Detail = ExistingPred.Detail.Append(RefinePred.Detail, "; ");
                                        //RefinePred.Result = ResultType.RefinementObject;
                                    }
                                    else
                                    {
                                        Log($"Debug: [Refinement]   Did *NOT* match face (r={r}) from {RefinePred.Server} for original {ExistingPred.Server} detection: {ExistingPred.ToString()} [{ExistingPred.Result}], Rectangle Match={matched}%, required={cam.MergePredictionsMinMatchPercent.ToPercent()} (Json CAM setting='MergePredictionsMinMatchPercent')");
                                    }
                                    RefinePred.PercentMatchRefinement = matched;

                                }
                                //try to match a license plate to a vehicle:
                                else if (RefinePred.ObjType == ObjectType.LicensePlate && ExistingPred.ObjType == ObjectType.Vehicle)
                                {
                                    Rectangle VehicleRect = ExistingPred.GetRectangle();
                                    Rectangle PlateRect = RefinePred.GetRectangle();

                                    //check for at least an 80% match since various ai servers create different size rectangles 
                                    if (RectangleMatches(VehicleRect, PlateRect, cam.MergePredictionsMinMatchPercent, out double matched, true))
                                    {
                                        Log($"Debug: [Refinement]   Added license plate detail '{ExistingPred.Detail}' (r={r}) from {RefinePred.Server} for original {ExistingPred.Server} detection: {ExistingPred.ToString()} [{ExistingPred.Result}], Rectangle Match={matched.ToPercent()}");
                                        ExistingPred.PercentMatchRefinement = matched;
                                        ExistingPred.RefineMergedCount++;
                                        ExistingPred.Detail = ExistingPred.Detail.Append(RefinePred.Detail, "; ");
                                    }
                                    else
                                    {
                                        Log($"Debug: [Refinement]   Did *NOT* match license plate (r={r}) from {RefinePred.Server} for original {ExistingPred.Server} detection: {ExistingPred.ToString()} [{ExistingPred.Result}], Rectangle Match={matched.ToPercent()}, required={cam.MergePredictionsMinMatchPercent.ToPercent()} (Json CAM setting='MergePredictionsMinMatchPercent')");
                                    }
                                    RefinePred.PercentMatchRefinement = matched;
                                }
                                else
                                {
                                    //lets just add it for now (new people, trucks, etc from refinement servers)
                                }

                            }

                            if (RefinePred.Result == ResultType.Relevant)
                                RelevantPredictionCount++;

                            RefinePred.OriginalOrder = initialpredictions.Count + 1;
                            initialpredictions.Add(RefinePred);


                        }


                        ///====================================================================================================================
                        ///De-duplicate + merge=================================================================================================
                        ///====================================================================================================================

                        ////lets sort the predictions so that lowest confidence are processed first so they are replaced with duplicates of higher confidence:
                        initialpredictions = initialpredictions.OrderBy(p => p.Result == ResultType.Relevant ? 1 : 999)
                                                               .ThenBy(p => p.ObjectPriority)
                                                               .ThenByDescending(p => p.Confidence).ToList();


                        List<ClsPrediction> predictions = new List<ClsPrediction>();

                        if (AppSettings.Settings.HistoryMergeDuplicatePredictions)
                        {
                            //take duplicates out of the queue as we go...
                            while (initialpredictions.Count > 0)
                            {
                                ClsPrediction TestPred = initialpredictions[0];
                                ClsPredMatch pm = ContainsPrediction(TestPred, initialpredictions, cam, ObjTypeMustMatch: true, TrueIfInsideOrPartiallyInside: false);
                                if (pm.preds.Count > 1) //if only 1, then it is itself
                                {
                                    //We only want to keep the first one of any that look alike, it will already be sorted with high confidence
                                    for (int i = 0; i < pm.preds.Count; i++)
                                    {
                                        if (i > 0)
                                        {
                                            pm.preds[i].Result = pm.preds[i].Result == ResultType.Relevant || pm.preds[i].Result == ResultType.RelevantDuplicateObject ? ResultType.RelevantDuplicateObject : ResultType.DuplicateObject;
                                            pm.preds[i].DupeCount++;
                                            pm.preds[0].Detail = pm.preds[0].Detail.Append(pm.preds[i].Detail, "; ");
                                            if (!pm.preds[0].Label.EqualsIgnoreCase(pm.preds[i].Label))
                                            {
                                                //add the dupe object name into the details column
                                                pm.preds[0].Detail = pm.preds[0].Detail.Append(pm.preds[i].Label, "; ");
                                            }
                                        }
                                        predictions.Add(pm.preds[i]);
                                        initialpredictions.Remove(pm.preds[i]);
                                    }

                                }
                                else
                                {
                                    predictions.Add(TestPred);
                                    initialpredictions.Remove(TestPred);
                                }
                            }


                        }
                        else
                        {
                            predictions.AddRange(initialpredictions);
                        }

                        ///====================================================================================================================
                        ///Run dynamic mask check last so that it does not increase mask counts of duplicate objects===========================
                        ///====================================================================================================================

                        foreach (ClsPrediction pred in predictions)
                        {
                            if (pred.Result == ResultType.Relevant)
                                pred.AnalyzePrediction(SkipDynamicMaskCheck: false);  //this may increase things like hitcount for relevant objects since it is run twice, may want to figure this out later
                        }


                        //sort predictions so most important are at the top
                        predictions = predictions.Distinct().OrderBy(p => p.Result == ResultType.Relevant ? 1 : 999)
                                                            .ThenBy(p => p.ObjectPriority)
                                                            .ThenByDescending(p => p.Confidence).ToList();

                        //update object tracking (for loitering) with this image's relevant predictions, using the
                        //image's capture time if we have it so tracking is correct even if processing is delayed/queued.
                        //Must run before PredictionsJSON is built below so TrackId/TrackSeconds get persisted with it.
                        DateTime TrackImageTime = CurImg.TimeCreated != DateTime.MinValue ? CurImg.TimeCreated : DateTime.Now;
                        List<TrackMatch> TrackMatches = cam.Tracker.Update(cam, TrackImageTime, predictions.Where(p => p.Result == ResultType.Relevant).ToList());
                        foreach (TrackMatch tm in TrackMatches)
                        {
                            tm.Prediction.TrackId = tm.Track.Id;
                            tm.Prediction.TrackSeconds = tm.Age.TotalSeconds;
                        }

                        //save any images with faces
                        foreach (ClsPrediction pred in predictions)
                        {
                            if (pred.ObjType == ObjectType.Face)
                                FaceMan.TryAddFaceFile(CurImg, pred.Label);
                        }

                        string PredictionsJSON = Global.GetJSONString(predictions);

                        ret.OutPredictions = predictions;

                        ///====================================================================================================================
                        ///====================================================================================================================
                        ///====================================================================================================================

                        int cancelactions = 0;
                        if (cam.Action_mqtt_enabled && !cam.Action_mqtt_payload_cancel.IsEmpty())
                            cancelactions++;

                        if (cam.Action_CancelURL_Enabled && cam.cancel_urls.Length > 0)
                            cancelactions++;

                        if (cam.Action_webhook_enabled && cam.Action_webhook_cancel_url.IsNotEmpty())
                            cancelactions++;

                        //process the combined predictions
                        if (predictions.Count > 0)
                        {
                            List<string> relevant_objects = new List<string>(); //list that will be filled with all objects that were detected and are triggering_objects for the camera
                            List<double> objects_confidence = new List<double>(); //list containing ai confidence value of object at same position in List objects
                            List<string> objects_details = new List<string>(); //list containing ai confidence value of object at same position in List objects
                            List<string> objects_position = new List<string>(); //list containing object positions (xmin, ymin, xmax, ymax)

                            List<string> irrelevant_objects = new List<string>(); //list that will be filled with all irrelevant objects
                            List<double> irrelevant_objects_confidence = new List<double>(); //list containing ai confidence value of irrelevant object at same position in List objects
                            List<string> irrelevant_objects_details = new List<string>(); //list containing ai confidence value of irrelevant object at same position in List objects
                            List<string> irrelevant_objects_position = new List<string>(); //list containing irrelevant object positions (xmin, ymin, xmax, ymax)


                            int masked_counter = 0; //this value is incremented if an object is in a masked area
                            int threshold_counter = 0; // this value is incremented if an object does not satisfy the confidence limit requirements
                            int irrelevant_counter = 0; // this value is incremented if an irrelevant (but not masked or out of range) object is detected
                            int error_counter = 0;

                            //if we are not using the local deepstack windows version, this means nothing:
                            DeepStackServerControl.IsActivated = true;

                            bool HasIgnore = false;
                            //Find out if there is even one ignored object to prevent the trigger later
                            foreach (ClsPrediction pred in predictions)
                            {
                                if (pred.Result == ResultType.IgnoredObject)
                                    HasIgnore = true;
                            }

                            //print every detected object with the according confidence-level
                            if (HasIgnore)
                                Log($"Debug:    Detected objects ('Ignored' object found):", AISRV, cam, CurImg);
                            else
                                Log($"Debug:    Detected objects:", AISRV, cam, CurImg);

                            foreach (ClsPrediction pred in predictions)
                            {

                                string clr = "";
                                if (pred.Result != ResultType.Error)
                                    DeepStackServerControl.VisionDetectionRunning = true;

                                if (pred.Result == ResultType.Relevant && !HasIgnore)
                                {
                                    relevant_objects.Add(pred.Label);
                                    objects_confidence.Add(pred.Confidence);
                                    objects_details.Add(pred.Detail);
                                    objects_position.Add($"{pred.XMin.Round(0)},{pred.YMin.Round(0)},{pred.XMax.Round(0)},{pred.YMax.Round(0)}");
                                    clr = "{" + AppSettings.Settings.RectRelevantColor.Name + "}";
                                }
                                else
                                {
                                    clr = "{" + AppSettings.Settings.RectIrrelevantColor.Name + "}";
                                    irrelevant_objects.Add(pred.Label);
                                    irrelevant_objects_details.Add(pred.Detail);
                                    irrelevant_objects_confidence.Add(pred.Confidence);
                                    string position = $"{pred.XMin.Round(0)},{pred.YMin.Round(0)},{pred.XMax.Round(0)},{pred.YMax.Round(0)}";
                                    irrelevant_objects_position.Add(position);

                                    if (pred.Result == ResultType.NoConfidence)
                                    {
                                        threshold_counter++;
                                    }
                                    else if (pred.Result == ResultType.ImageMasked || pred.Result == ResultType.DynamicMasked || pred.Result == ResultType.StaticMasked)
                                    {
                                        clr = "{" + AppSettings.Settings.RectMaskedColor.Name + "}";
                                        masked_counter++;
                                    }
                                    else if (pred.Result == ResultType.Error)
                                    {
                                        clr = "{red}";
                                        error_counter++;
                                    }
                                    else
                                    {
                                        irrelevant_counter++;
                                    }
                                }

                                if (pred.Result == ResultType.Relevant || pred.Result == ResultType.Error)
                                    Log($"     {clr}Result='{pred.Result}', Detail='{pred.ToString()}', ObjType='{pred.ObjType}', ObjectResult={pred.ObjectResult}, DynMaskResult='{pred.DynMaskResult}', DynMaskType='{pred.DynMaskType}', ImgMaskResult='{pred.ImgMaskResult}', ImgMaskType='{pred.ImgMaskType}, PercentOfImage={pred.PercentOfImage.ToPercent()}", pred.Server, cam, CurImg);
                                else
                                    Log($"Debug:     {clr}Result='{pred.Result}', Detail='{pred.ToString()}', ObjType='{pred.ObjType}', ObjectResult={pred.ObjectResult}, DynMaskResult='{pred.DynMaskResult}', DynMaskType='{pred.DynMaskType}', ImgMaskResult='{pred.ImgMaskResult}', ImgMaskType='{pred.ImgMaskType}'", pred.Server, cam, CurImg);

                            }


                            //mark the end of AI detection for the current image
                            cam.maskManager.LastDetectionDate = DateTime.Now;


                            //if one or more objects were detected, that are 1. relevant, 2. within confidence limits and 3. outside of masked areas
                            if (relevant_objects.Count > 0)
                            {
                                //store these last detections for the specific camera
                                cam.last_detections = relevant_objects;
                                cam.last_confidences = objects_confidence;
                                cam.last_details = objects_details;
                                cam.last_positions = objects_position;
                                cam.last_image_file_with_detections = CurImg.image_path;

                                //the new way


                                //create summary string for this detection
                                StringBuilder detectionsTextSb = new StringBuilder();
                                for (int i = 0; i < relevant_objects.Count; i++)
                                {
                                    detectionsTextSb.Append($"{relevant_objects[i]} {String.Format(AppSettings.Settings.DisplayPercentageFormat, objects_confidence[i])}; "); // String.Format("{0} ({1}%) | ", objects[i], Math.Round((objects_confidence[i] * 100), 2)));
                                }

                                cam.last_detections_summary = detectionsTextSb.ToString().Trim("; ".ToCharArray());

                                //create text string objects and confidences
                                string objects_and_confidences = "";
                                string object_positions_as_string = "";
                                //for (int i = 0; i < objects.Count; i++)
                                //{
                                //    objects_and_confidences += $"{objects[i]} {String.Format(AppSettings.Settings.DisplayPercentageFormat, objects_confidence[i])}; ";
                                //    object_positions_as_string += $"{objects_position[i]};";
                                //}

                                foreach (ClsPrediction pred in predictions)
                                {
                                    if (pred.Result != ResultType.Relevant && AppSettings.Settings.HistoryOnlyDisplayRelevantObjects)
                                        continue;

                                    objects_and_confidences += $"{pred.ToString()}; ";
                                    object_positions_as_string += $"{pred.PositionString()};";
                                }

                                objects_and_confidences = objects_and_confidences.Trim("; ".ToCharArray());

                                Log($"Debug: The summary:" + cam.last_detections_summary, AISRV, cam, CurImg);

                                //loitering gate: if the camera requires an object to be present for N seconds,
                                //only proceed if at least one relevant prediction's track has aged that far
                                bool LoiterOk = ObjectTracker.ShouldTrigger(cam, TrackMatches, out ObjectTrack LoiterTrack, out TimeSpan LoiterAge);

                                if (LoiterOk)
                                {
                                    Log($"Debug: (5/6) Performing alert actions:", AISRV, cam, CurImg);


                                    hist = new History().Create(CurImg.image_path, DateTime.Now, cam.Name, objects_and_confidences, object_positions_as_string, true, PredictionsJSON, AISRV, TotalSWPostTime, true);

                                    await TriggerActionQueue.AddTriggerActionAsync(TriggerType.All, cam, CurImg, hist, true, !cam.Action_queued, AISRV, ""); //make TRIGGER

                                    cam.IncrementAlerts(); //stats update
                                    Log($"Debug: (6/6) SUCCESS.", AISRV, cam, CurImg);

                                    //add to history list
                                    //Log($"Debug: Adding detection to history list.", AiUrl.CurSrv, cam.name);
                                    Global.CreateHistoryItem(hist);
                                }
                                else
                                {
                                    //not loitering long enough yet - treat like the existing cooldown-skip path: no trigger
                                    //actions, but still record it in history (wording includes "skipped" so History.WasSkipped
                                    //picks it up and the existing "skipped" history filter/coloring applies)
                                    string LoiterLabel = LoiterTrack != null ? LoiterTrack.Label : "object";
                                    int LoiterTrackId = LoiterTrack != null ? LoiterTrack.Id : 0;

                                    Log($"Debug: (5/6) Not triggering - {LoiterLabel} track #{LoiterTrackId} present {LoiterAge.TotalSeconds.Round()}s of {cam.LoiterSecondsRequired}s required.", AISRV, cam, CurImg);

                                    hist = new History().Create(CurImg.image_path, DateTime.Now, cam.Name, $"Skipped alert, not loitering long enough yet ({LoiterLabel} track #{LoiterTrackId} present {LoiterAge.TotalSeconds.Round()}s of {cam.LoiterSecondsRequired}s required) : {objects_and_confidences}", object_positions_as_string, false, PredictionsJSON, AISRV, TotalSWPostTime, false);

                                    cam.stats_skipped_images++;
                                    cam.stats_skipped_images_session++;

                                    Global.CreateHistoryItem(hist);
                                }
                            }
                            //if no object fulfills all 3 requirements but there are other objects: 
                            else if (irrelevant_objects.Count > 0)
                            {
                                //IRRELEVANT ALERT

                                //retrieve confidences and positions
                                string objects_and_confidences = "";
                                string object_positions_as_string = "";

                                //for (int i = 0; i < irrelevant_objects.Count; i++)
                                //{
                                //    objects_and_confidences += $"{irrelevant_objects[i]} {String.Format(AppSettings.Settings.DisplayPercentageFormat, irrelevant_objects_confidence[i])}; "; // ({Math.Round((irrelevant_objects_confidence[i] * 100), 0)}%); ";
                                //    object_positions_as_string += $"{irrelevant_objects_position[i]};";
                                //}

                                foreach (ClsPrediction pred in predictions)
                                {
                                    //if (pred.Result != ResultType.Relevant)
                                    //{
                                    objects_and_confidences += $"{pred.ToString()}; ";
                                    object_positions_as_string += $"{pred.PositionString()};";
                                    //}
                                }


                                objects_and_confidences = objects_and_confidences.Trim("; ".ToCharArray());

                                //string text contains what is written in the log and in the history list
                                string text = "";
                                if (masked_counter > 0)//if masked objects, add them
                                {
                                    text += $"{masked_counter}x masked; ";
                                }
                                if (threshold_counter > 0)//if objects out of confidence range, add them
                                {
                                    text += $"{threshold_counter}x not in confidence range; ";
                                }
                                if (irrelevant_counter > 0) //if other irrelevant objects, add them
                                {
                                    text += $"{irrelevant_counter}x irrelevant; ";
                                }
                                if (error_counter > 0) //if other irrelevant objects, add them
                                {
                                    text += $"{error_counter}x errors; ";
                                }

                                if (text != "") //remove last ";"
                                {
                                    text = text.Remove(text.Length - 2);
                                }


                                Log($"Debug: {text}, so it's an irrelevant alert.", AISRV, cam, CurImg);


                                hist = new History().Create(CurImg.image_path, DateTime.Now, cam.Name, $"{text} : {objects_and_confidences}", object_positions_as_string, false, PredictionsJSON, AISRV, TotalSWPostTime, false);

                                if (cancelactions > 0)
                                {
                                    Log($"Debug: (5/6) Performing {cancelactions} CANCEL actions:", AISRV, cam, CurImg);
                                    await TriggerActionQueue.AddTriggerActionAsync(TriggerType.Cancel, cam, CurImg, hist, false, !cam.Action_queued, AISRV, ""); //make TRIGGER
                                }
                                else
                                    Log($"Debug: (5/6) NO CANCEL actions defined, skipping.", AISRV, cam, CurImg);


                                cam.IncrementIrrelevantAlerts(); //stats update
                                Log($"Debug: (6/6) Camera {cam.Name} caused an irrelevant alert.", AISRV, cam, CurImg);

                                //add to history list
                                Global.CreateHistoryItem(hist);
                            }
                        }
                        else
                        {
                            Log($"Debug:      ((NO DETECTED OBJECTS))", AISRV, cam, CurImg);
                            // FALSE ALERT

                            cam.IncrementFalseAlerts(); //stats update

                            hist = new History().Create(CurImg.image_path, DateTime.Now, cam.Name, "false alert", "", false, "", AISRV, TotalSWPostTime, false);

                            if (cancelactions > 0)
                            {
                                Log($"Debug: (5/6) Performing {cancelactions} CANCEL actions:", AISRV, cam, CurImg);
                                await TriggerActionQueue.AddTriggerActionAsync(TriggerType.Cancel, cam, CurImg, hist, false, !cam.Action_queued, AISRV, ""); //make TRIGGER
                            }
                            else
                                Log($"Debug: (5/6) NO CANCEL actions defined, skipping.", AISRV, cam, CurImg);


                            Log($"Debug: (6/6) Camera {cam.Name} caused a false alert, nothing detected.", AISRV, cam, CurImg);

                            //add to history list
                            Global.CreateHistoryItem(hist);
                        }


                    }
                    else
                    {
                        //could not access the file for 30 seconds??   Or unexpected error
                        ret.Error = $"Error: Last Image message: '{CurImg.LastError}'.  ({CurImg.FileLockMS}ms, with {CurImg.FileLockErrRetryCnt} retries)";
                        CurImg.ErrCount++;
                        CurImg.ResultMessage = ret.Error;
                        Log(ret.Error, AISRV, cam, CurImg);
                    }

                }
                catch (Exception ex)
                {

                    ret.Error = $"ERROR: {ex.Msg()}";
                    AiUrl.IncrementError();
                    AiUrl.LastResultMessage = ret.Error;
                    AiUrl.LastResultSuccess = false;
                    Log(ret.Error, AISRV, cam, CurImg);
                }
                //exitfunction:
                if (!string.IsNullOrEmpty(ret.Error) && AppSettings.Settings.send_telegram_errors && cam.telegram_enabled && !(cam.Paused && cam.PauseTelegram))
                {
                    //bool success = await TelegramUpload(CurImg, "Error");
                    if (hist == null)
                    {
                        hist = new History().Create(CurImg.image_path, DateTime.Now, cam.Name, "error", "", false, "", AiUrl.CurSrv, TotalSWPostTime, false);
                    }
                    await TriggerActionQueue.AddTriggerActionAsync(TriggerType.TelegramImageUpload, cam, CurImg, hist, false, !cam.Action_queued, AiUrl.CurSrv, "Error"); //make TRIGGER
                }


                //I notice deepstack takes a lot longer the very first run?

                CurImg.TotalTimeMS = (long)(DateTime.Now - CurImg.TimeAdded).TotalMilliseconds; //sw.ElapsedMilliseconds + CurImg.QueueWaitMS + CurImg.FileLockMS;
                CurImg.DeepStackTimeMS = TotalSWPostTime;

                CurImg.LifeTimeMS = (long)(DateTime.Now - CurImg.TimeCreated).TotalMilliseconds;
                tcalc.AddToCalc(CurImg.TotalTimeMS);
                qcalc.AddToCalc(CurImg.QueueWaitMS);
                fcalc.AddToCalc(CurImg.FileLockMS);
                lcalc.AddToCalc(CurImg.FileLoadMS);
                icalc.AddToCalc(CurImg.LifeTimeMS);

                Log($"Debug:          Total Time: {CurImg.TotalTimeMS.ToString().PadLeft(6)} ms (Count={tcalc.Count.ToString().PadLeft(6)}, Min={tcalc.MinS.PadLeft(6)} ms, Max={tcalc.MaxS.PadLeft(6)} ms, Avg={tcalc.AvgS.PadLeft(6)} ms)", AiUrl.CurSrv, cam, CurImg);

                foreach (ClsURLItem url in ret.OutURLs)
                {
                    Log($"Debug:       AI (URL) Time: {url.LastTimeMS.ToString().PadLeft(6)} ms (Count={url.AITimeCalcs.Count.ToString().PadLeft(6)}, Min={url.AITimeCalcs.MinS.PadLeft(6)} ms, Max={url.AITimeCalcs.MaxS.PadLeft(6)} ms, Avg={url.AITimeCalcs.AvgS.PadLeft(6)} ms)", url.CurSrv, cam, CurImg);
                }

                Log($"Debug:      File lock Time: {CurImg.FileLockMS.ToString().PadLeft(6)} ms (Count={fcalc.Count.ToString().PadLeft(6)}, Min={fcalc.MinS.PadLeft(6)} ms, Max={fcalc.MaxS.PadLeft(6)} ms, Avg={fcalc.AvgS.PadLeft(6)} ms)", AiUrl.CurSrv, cam, CurImg);
                Log($"Debug:      File load Time: {CurImg.FileLoadMS.ToString().PadLeft(6)} ms (Count={lcalc.Count.ToString().PadLeft(6)}, Min={lcalc.MinS.PadLeft(6)} ms, Max={lcalc.MaxS.PadLeft(6)} ms, Avg={lcalc.AvgS.PadLeft(6)} ms)", AiUrl.CurSrv, cam, CurImg);
                Log($"Debug:    Image Queue Time: {CurImg.QueueWaitMS.ToString().PadLeft(6)} ms (Count={qcalc.Count.ToString().PadLeft(6)}, Min={qcalc.MinS.PadLeft(6)} ms, Max={qcalc.MaxS.PadLeft(6)} ms, Avg={qcalc.AvgS.PadLeft(6)} ms)", AiUrl.CurSrv, cam, CurImg);
                Log($"Debug:     Image Life Time: {CurImg.LifeTimeMS.ToString().PadLeft(6)} ms (Count={icalc.Count.ToString().PadLeft(6)}, Min={icalc.MinS.PadLeft(6)} ms, Max={icalc.MaxS.PadLeft(6)} ms, Avg={icalc.AvgS.PadLeft(6)} ms)", AiUrl.CurSrv, cam, CurImg);
                Log($"Debug:   Image Queue Depth: {CurImg.CurQueueSize.ToString().PadLeft(6)}    (Count={scalc.Count.ToString().PadLeft(6)}, Min={scalc.MinS.PadLeft(6)},    Max={scalc.MaxS.PadLeft(6)},    Avg={scalc.AvgS.PadLeft(6)})", AiUrl.CurSrv, cam, CurImg);

            }
            else
            {
                cam.stats_skipped_images++;
                cam.stats_skipped_images_session++;
                Log($"Skipping detection for '{filename}' because cooldown has not been met for camera '{cam.Name}':  '{secs.Round()}' of '{halfcool.Round()}' seconds (half of trigger cooldown time), Session Skip Count={cam.stats_skipped_images_session}", AiUrl.CurSrv, cam, CurImg);
                Global.CreateHistoryItem(new History().Create(CurImg.image_path, DateTime.Now, cam.Name, $"Skipped image, cooldown was '{secs.Round()}' of '{halfcool.Round()}' seconds.", "", false, "", AiUrl.CurSrv, TotalSWPostTime, false));
            }

            foreach (var url in ret.OutURLs)
            {
                url.DecrementQueue();
            }

            ret.Success = (ret.Error == "");
            ret.TimeMS = (int)TotalSWPostTime;

            return ret;

        }
    }
}
