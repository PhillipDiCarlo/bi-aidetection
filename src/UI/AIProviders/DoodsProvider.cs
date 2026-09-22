using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Amazon;
using Amazon.Rekognition;
using Amazon.Rekognition.Model;
using Amazon.Runtime;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using static AITool.AITOOL;
using static AITool.Global;
using Rectangle = System.Drawing.Rectangle;

namespace AITool.AIProviders
{
    /// <summary>DOODS (Dedicated Open Object Detection Service).</summary>
    public class DoodsProvider : IAIProvider
    {
        public async Task<ClsAIServerResponse> DetectAsync(ClsImageQueueItem CurImg, ClsURLItem AiUrl, Camera cam, CancellationToken ct)
        {
            using var Trace = new Trace();

            ClsAIServerResponse ret = new ClsAIServerResponse();
            ret.AIURL = AiUrl;
            bool OverrideThreshold = AiUrl.Threshold_Lower > 0 || (AiUrl.Threshold_Upper > 0 && AiUrl.Threshold_Upper < 100);

            Stopwatch swposttime = new Stopwatch();

            try
            {
                ClsDoodsRequest cdr = new ClsDoodsRequest();

                //We could prevent doods from giving back ALL of its results but then we couldnt fully see how it was working:
                //So many things come back from Doods, cluttering up the db, we really need to limit at the source

                if (AppSettings.Settings.HistoryRestrictMinThresholdAtSource && !OverrideThreshold)
                    cdr.Detect.MinPercentMatch = cam.threshold_lower;

                if (AppSettings.Settings.HistoryRestrictMinThresholdAtSource && OverrideThreshold)
                    cdr.Detect.MinPercentMatch = AiUrl.Threshold_Lower;

                cdr.DetectorName = AppSettings.Settings.DOODSDetectorName;

                //string testjson = JsonConvert.SerializeObject(cdr);

                long FileSize = new FileInfo(CurImg.image_path).Length;

                cdr.Data = CurImg.ToMemStream().ConvertToBase64();

                using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, AiUrl.ToString()))
                {

                    using HttpContent httpContent = Global.CreateHttpContentString(cdr);

                    request.Content = httpContent;

                    Log($"Debug: (1/6) Uploading a {Global.FormatBytes(FileSize)} image to '{AiUrl.Type}' AI Server at {AiUrl}", AiUrl.CurSrv, cam, CurImg);


                    //  Got http status code 'RequestEntityTooLarge' (413) in 42ms: Request Entity Too Large
                    swposttime.Restart();

                    using HttpResponseMessage output = await AiUrl.HttpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead, ct);

                    swposttime.Stop();
                    ret.StatusCode = output.StatusCode;


                    if (output.IsSuccessStatusCode)
                    {
                        ret.JsonString = await output.Content.ReadAsStringAsync();

                        if (ret.JsonString != null && !string.IsNullOrWhiteSpace(ret.JsonString))
                        {
                            string cleanjsonString = ret.JsonString.CleanString();

                            ClsDoodsResponse response = null;

                            try
                            {
                                //This can throw an exception
                                response = JsonConvert.DeserializeObject<ClsDoodsResponse>(ret.JsonString);

                                if (response != null)
                                {
                                    if (response.Detections != null)
                                    {
                                        if (response.Detections.Count > 0)
                                        {

                                            foreach (ClsDoodsDetection DSObj in response.Detections)
                                            {
                                                ClsPrediction pred = new ClsPrediction(ObjectType.Object, cam, DSObj, CurImg, AiUrl);

                                                ret.Predictions.Add(pred);

                                            }


                                        }

                                        ret.Success = true;
                                        AiUrl.LastResultMessage = $"{ret.Predictions.Count} predictions found.";


                                    }
                                    else
                                    {
                                        ret.Error = $"ERROR: No predictions?  JSON: '{cleanjsonString}')";
                                        AiUrl.IncrementError();
                                        AiUrl.LastResultMessage = ret.Error;
                                    }


                                }
                                else if (string.IsNullOrEmpty(ret.Error))
                                {
                                    //deserialization did not cause exception, it just gave a null response in the object?
                                    //probably wont happen but just making sure
                                    ret.Error = $"ERROR: Deserialization of 'Response' from DeepStack failed. response is null. JSON: '{cleanjsonString}'";
                                    AiUrl.IncrementError();
                                    AiUrl.LastResultMessage = ret.Error;
                                }
                            }
                            catch (Exception ex)
                            {
                                ret.Error = $"ERROR: Deserialization of 'Response' from '{AiUrl.Type.ToString()}' failed: {ex.Msg()}, JSON: '{cleanjsonString}'";
                                AiUrl.IncrementError();
                                AiUrl.LastResultMessage = ret.Error;
                            }
                        }
                        else
                        {
                            ret.Error = $"ERROR: Empty string returned from HTTP post.";
                            AiUrl.IncrementError();
                            AiUrl.LastResultMessage = ret.Error;
                        }


                    }
                    else
                    {
                        ret.Error = $"ERROR: Got http status code '{output.StatusCode}' ({Convert.ToInt32(output.StatusCode)}) in {swposttime.ElapsedMilliseconds}ms: {output.ReasonPhrase}";
                        AiUrl.IncrementError();
                        AiUrl.LastResultMessage = ret.Error;
                    }

                }



            }
            catch (Exception ex)
            {
                swposttime.Stop();

                long seconds = swposttime.ElapsedMilliseconds / 1000;
                if (seconds >= AiUrl.GetTimeout().TotalSeconds)
                {
                    ret.Error = $"ERROR: HTTPClient timeout at {seconds} seconds (Max={AiUrl.GetTimeout().TotalSeconds} set in 'HTTPClientTimeoutSeconds' in aitool.settings.json): {ex.Msg()}";
                }
                else
                {
                    ret.Error = $"ERROR: {ex.Msg()}";
                }

                ret.Error = $"ERROR: {ex.Msg()}";
                AiUrl.IncrementError();
                AiUrl.LastResultMessage = ret.Error;
            }
            finally
            {
                ret.SWPostTime = swposttime.ElapsedMilliseconds;
                AiUrl.LastTimeMS = (int)swposttime.ElapsedMilliseconds;
                AiUrl.AITimeCalcs.AddToCalc(AiUrl.LastTimeMS);

            }


            return ret;
        }
    }
}
