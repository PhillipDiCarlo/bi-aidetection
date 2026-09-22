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
    /// <summary>CodeProject.AI, DeepStack, and anything else that speaks the DeepStack /v1/vision/* API (e.g. Blue Onyx).</summary>
    public class DeepStackCompatibleProvider : IAIProvider
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
                long FileSize = new FileInfo(CurImg.image_path).Length;

                using MultipartFormDataContent request = new MultipartFormDataContent();

                using StreamContent sc = new StreamContent(CurImg.ToMemStream());

                request.Add(sc, "image", Path.GetFileName(CurImg.image_path));

                string overr = "(NoLowerThresholdOverride)";

                double minconf = 0;
                if (AppSettings.Settings.HistoryRestrictMinThresholdAtSource && !OverrideThreshold)
                {
                    overr = $"(CAM_LowerThresholdOverride={cam.threshold_lower},Upper={cam.threshold_upper})";
                    minconf = cam.threshold_lower;
                }
                else if (AppSettings.Settings.HistoryRestrictMinThresholdAtSource && OverrideThreshold)
                {
                    overr = $"(URL_LowerThresholdOverride={AiUrl.Threshold_Lower},Upper={AiUrl.Threshold_Upper})";
                    minconf = AiUrl.Threshold_Lower;
                }

                double pc = 0;

                if (minconf > 0)
                {
                    pc = minconf / 100;
                    overr += $"({pc})";
                    StringContent scmc = new StringContent((pc).ToString().Replace(",", "."));  //replace comma with period in cases where the regional decimal symbol is a comma - Deepstack doesnt like that.
                    request.Add(scmc, "min_confidence");
                }

                Log($"Debug: (1/6) Uploading a {Global.FormatBytes(FileSize)} image to '{AiUrl.Type}' {overr} AI Server at {AiUrl}", AiUrl.CurSrv, cam, CurImg);

                swposttime.Restart();


                using HttpResponseMessage output = await AiUrl.HttpClient.PostAsync(AiUrl.ToString(), request, ct);

                swposttime.Stop();
                ret.StatusCode = output.StatusCode;
                ret.JsonString = await output.Content.ReadAsStringAsync();

                ClsDeepStackResponse response = null;
                string cleanjsonString = "";
                if (ret.JsonString != null && !string.IsNullOrWhiteSpace(ret.JsonString))
                {
                    cleanjsonString = ret.JsonString.CleanString();
                    try
                    {
                        response = JsonConvert.DeserializeObject<ClsDeepStackResponse>(ret.JsonString);
                    }
                    catch (Exception ex)
                    {
                        //deserialization did not cause exception, it just gave a null response in the object?
                        //probably wont happen but just making sure
                        ret.Error = $"ERROR: Deserialization of 'Response' from DeepStack failed. JSON: '{cleanjsonString}': {ex.Message}";
                        AiUrl.IncrementError();
                        AiUrl.LastResultMessage = ret.Error;
                    }
                }
                else
                {
                    ret.Error = $"ERROR: Empty string returned from HTTP post?";
                    AiUrl.IncrementError();
                    AiUrl.LastResultMessage = ret.Error;
                }

                if (output.IsSuccessStatusCode)
                {
                    try
                    {
                        if (response != null)
                        {
                            if (!response.success || !string.IsNullOrWhiteSpace(response.error)) // || )
                            {
                                string err = "";
                                if (!string.IsNullOrWhiteSpace(response.error))
                                    err = response.error;

                                bool MeshError = err.Has("Exception when forwarding request");

                                // if we would normally be ignoring offline errors, dont let the response be considered an error...
                                // JSON: '{"Error":"Exception when forwarding request to CDODGE8","Code":500,"Hostname":"K4","Success":false,"processedBy":"CDODGE8","timestampUTC":"Sun, 26 May 2024 01:03:10 GMT"}'

                                // extract the mesh hostname from the json and try to match it to an existing ai server so we can check its IgnoreOfflineError setting:
                                //"processedBy":"CDODGE8",
                                ClsURLItem fndurl = AiUrl;
                                bool IgnoreOfflineErr = false;
                                if (MeshError)
                                {
                                    string meshhostname = cleanjsonString.GetWord("\"processedBy\":\"", "\",");
                                    if (meshhostname.IsNotEmpty())
                                    {
                                        ClsURLItem murl = AITOOL.GetURL(meshhostname, false, true);
                                        if (murl != null)
                                            fndurl = murl;

                                        IgnoreOfflineErr = fndurl.IgnoreOfflineError;
                                    }
                                }

                                if (IgnoreOfflineErr)
                                {
                                    //we have a mesh error connecting to another computer, ignore it and remove error from json string so it is not flagged elsewhere
                                    ret.Error = $"DEBUG: CPAI Mesh Failure on '{fndurl.Name}', ignoring due to 'IgnoreOfflineErr'.  Type='{AiUrl.Type.ToString()}'. Failure='{err}'. JSON: '{cleanjsonString.Replace("Error", "Failure")}'";
                                }
                                else
                                {
                                    AiUrl.IncrementError();
                                    ret.Error = $"ERROR: Failure response from '{AiUrl.Type.ToString()}'. Error='{err}'. JSON: '{cleanjsonString}'";
                                }
                                AiUrl.LastResultMessage = ret.Error;
                            }
                            else
                            {
                                List<ClsDeepstackDetection> addto = new List<ClsDeepstackDetection>();

                                //intialize array if none returned so we can add a scene if needed
                                if (response.predictions != null)
                                    addto = response.predictions.ToList();

                                //check to see if we have a scene rather than normal detection and create a prediction from it
                                if (!string.IsNullOrEmpty(response.label) && response.confidence > 0)
                                {
                                    //{'success': True, 'confidence': 0.7373981, 'label': 'conference_room'
                                    ClsDeepstackDetection spred = new ClsDeepstackDetection();
                                    spred.label = $"Scene";
                                    spred.Detail = response.label;
                                    spred.confidence = response.confidence;
                                    //try to create a rectangle smaller than the image so the label will fit
                                    spred.x_min = 5;
                                    spred.y_min = 5;
                                    spred.x_max = CurImg.Width - 5;
                                    spred.y_max = CurImg.Height - 40;
                                    addto.Add(spred);
                                }

                                foreach (ClsDeepstackDetection DSObj in addto)
                                {
                                    ClsPrediction pred = new ClsPrediction(ObjectType.Object, cam, DSObj, CurImg, AiUrl);

                                    ret.Predictions.Add(pred);

                                }


                                ret.Success = true;
                                AiUrl.LastResultMessage = $"{ret.Predictions.Count} predictions found.";
                                if (response.message.IsNotEmpty())
                                    AiUrl.LastResultMessage += $" Message: '{response.message}'";

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
                    if (response != null && !string.IsNullOrEmpty(response.error))
                        ret.Error = $"ERROR: AI Server '{AiUrl.Type}' returned '{response.error}' - http status code '{output.StatusCode}' ({Convert.ToInt32(output.StatusCode)}) in {swposttime.ElapsedMilliseconds}ms: {output.ReasonPhrase}";
                    else
                        ret.Error = $"ERROR: Got http status code '{output.StatusCode}' ({Convert.ToInt32(output.StatusCode)}) in {swposttime.ElapsedMilliseconds}ms: {output.ReasonPhrase}";

                    AiUrl.IncrementError();
                    AiUrl.LastResultMessage = ret.Error;
                }

            }
            catch (Exception ex)
            {
                swposttime.Stop();
                long seconds = swposttime.ElapsedMilliseconds / 1000;
                if (seconds >= AiUrl.GetTimeout().TotalSeconds)
                {
                    ret.Error = $"ERROR: HTTPClient timeout at {seconds} seconds ('HTTPClientTimeoutSeconds' is currently set to {AiUrl.GetTimeout().TotalSeconds} in AITOOL.Settings.JSON file.): {ex.Msg()}";
                }
                else
                {
                    ret.Error = $"ERROR: {ex.Msg()}";
                }
                AiUrl.IncrementError();
                AiUrl.LastResultMessage = ret.Error;
            }
            finally
            {
                AiUrl.LastTimeMS = (int)swposttime.ElapsedMilliseconds;
                ret.SWPostTime = (int)swposttime.ElapsedMilliseconds;
                AiUrl.AITimeCalcs.AddToCalc(AiUrl.LastTimeMS);

                if (!string.IsNullOrEmpty(ret.Error))
                    DeepStackServerControl.PrintDeepStackError();  //only prints error if we have locally installed windows deepstack and there is a new entry in stderr.txt

            }


            return ret;
        }
    }
}
