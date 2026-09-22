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
    /// <summary>SightHound cloud vehicle and person detection.</summary>
    public class SightHoundProvider : IAIProvider
    {
        public async Task<ClsAIServerResponse> DetectAsync(ClsImageQueueItem CurImg, ClsURLItem AiUrl, Camera cam, CancellationToken ct)
        {
            using var Trace = new Trace();

            ClsAIServerResponse ret = new ClsAIServerResponse();
            ret.AIURL = AiUrl;


            if (string.IsNullOrEmpty(AppSettings.Settings.SightHoundAPIKey))
            {
                ret.Success = false;
                ret.Error = $"ERROR: No SightHound API key set. (SightHoundAPIKey in AITOOL.SETTINGS.JSON).'";
                AiUrl.IncrementError();
                AiUrl.LastResultMessage = ret.Error;
                return ret;
            }

            Stopwatch swposttime = new Stopwatch();

            //WebRequest request = null;
            //HttpWebResponse WebResponse = null;
            //Stream requestStream = null;

            MultipartFormDataContent request = null;
            //StreamContent stream = null;

            try
            {
                long FileSize = new FileInfo(CurImg.image_path).Length;

                request = new MultipartFormDataContent();

                //stream = new StreamContent(CurImg.ToStream());
                //string base64Img = CurImg.ToStream().ConvertToBase64();
                //using StringContent imgstr = new StringContent(base64Img, Encoding.UTF8, "image/jpeg");  //Encoding.UTF8, "application/json"


                //Dictionary<string, byte[]> dict = new Dictionary<string, byte[]>();
                Dictionary<string, string> dict = new Dictionary<string, string>();
                dict.Add("image", CurImg.ToMemStream().ConvertToBase64());
                string json = Global.GetJSONString((object)dict); //JsonConvert.SerializeObject((object)dict);
                //byte[] body = Encoding.UTF8.GetBytes(json);
                //ByteArrayContent content = new ByteArrayContent(body);
                //HttpContent content = Global.CreateHttpContentString(dict);
                StringContent content = new StringContent(json, Encoding.UTF8, "application/json");
                //request.Add(content);

                if (!AiUrl.HttpClient.DefaultRequestHeaders.Contains("X-Access-Token"))
                {
                    AiUrl.HttpClient.DefaultRequestHeaders.Add("X-Access-Token", AppSettings.Settings.SightHoundAPIKey);
                }

                //Dictionary<string, byte[]> dict = new Dictionary<string, byte[]>();
                //dict.Add("image", CurImg.ImageByteArray);
                //string json = JsonConvert.SerializeObject((object)dict);
                //byte[] body = Encoding.UTF8.GetBytes(json);

                //request = WebRequest.Create(AiUrl.ToString());

                //request.Timeout = AppSettings.Settings.HTTPClientLocalTimeoutSeconds * 1000;

                //request.Method = "POST";
                //request.ContentType = "application/json";
                //request.ContentLength = json.Length;
                //request.Headers["X-Access-Token"] = AppSettings.Settings.SightHoundAPIKey;

                Log($"Debug: (1/6) Uploading a {Global.FormatBytes(FileSize)} image ({FileSize} bytes in request) to '{AiUrl.Type}' AI Server at {AiUrl}", AiUrl.CurSrv, cam, CurImg);

                swposttime.Restart();

                //requestStream = request.GetRequestStream();
                //requestStream.Write(body, 0, body.Length);

                //WebResponse = (HttpWebResponse)request.GetResponse();


                using HttpResponseMessage output = await AiUrl.HttpClient.PostAsync(AiUrl.ToString(), content, ct);

                swposttime.Stop();
                ret.StatusCode = output.StatusCode;
                ret.JsonString = await output.Content.ReadAsStringAsync();

                //ret.StatusCode = WebResponse.StatusCode;

                //Successful queries will return a 200 (OK) response with a JSON body describing all detected objects and the attributes of the processed image.
                if (output.IsSuccessStatusCode)
                {

                    //using StreamReader reader = new StreamReader(WebResponse.GetResponseStream(), Encoding.UTF8);
                    //ret.JsonString = reader.ReadToEnd();

                    swposttime.Stop();

                    if (ret.JsonString != null && !string.IsNullOrWhiteSpace(ret.JsonString))
                    {
                        string cleanjsonString = ret.JsonString.CleanString();

                        try
                        {

                            JObject JOResult = JObject.Parse(ret.JsonString);

                            if (AiUrl.Type == URLTypeEnum.SightHound_Vehicle)
                            {
                                //Vehicle Recognition

                                //This can throw an exception
                                SightHoundVehicleRoot SHObj = JsonConvert.DeserializeObject<SightHoundVehicleRoot>(ret.JsonString);

                                if (SHObj != null)
                                {
                                    if (SHObj.Objects != null)
                                    {


                                        if (SHObj.Objects.Count > 0)
                                        {
                                            foreach (SightHoundVehicleObject DSObj in SHObj.Objects)
                                            {
                                                //Get the vehicle and plate as 2 separate predictions
                                                ClsPrediction predv = new ClsPrediction(ObjectType.Object, cam, DSObj, CurImg, AiUrl, SHObj.Image, false);
                                                if (!string.IsNullOrEmpty(predv.Label))
                                                    ret.Predictions.Add(predv);
                                                ClsPrediction predp = new ClsPrediction(ObjectType.Object, cam, DSObj, CurImg, AiUrl, SHObj.Image, true);
                                                if (!string.IsNullOrEmpty(predp.Label))
                                                    ret.Predictions.Add(predp);

                                            }
                                        }

                                        ret.Success = true;
                                        AiUrl.LastResultMessage = $"{ret.Predictions.Count} Vehicle predictions found.";
                                    }
                                    else
                                    {
                                        ret.Error = $"ERROR: No Vehicle predictions?  JSON: '{cleanjsonString}')";
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
                            else if (AiUrl.Type == URLTypeEnum.SightHound_Person)
                            {
                                //face/person detection

                                //This can throw an exception
                                SightHoundPersonRoot SHObj = JsonConvert.DeserializeObject<SightHoundPersonRoot>(ret.JsonString);

                                if (SHObj != null)
                                {
                                    if (SHObj.Objects != null)
                                    {
                                        if (SHObj.Objects.Count > 0)
                                        {
                                            foreach (SightHoundPersonObject DSObj in SHObj.Objects)
                                            {
                                                ClsPrediction pred = new ClsPrediction(ObjectType.Object, cam, DSObj, CurImg, AiUrl, SHObj.Image);
                                                ret.Predictions.Add(pred);
                                            }
                                        }

                                        ret.Success = true;
                                        AiUrl.LastResultMessage = $"{ret.Predictions.Count} Person predictions found.";

                                    }
                                    else
                                    {
                                        ret.Error = $"ERROR: No Person predictions?  JSON: '{cleanjsonString}')";
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
                    //try to get the error response:
                    //{
                    //        "error": "ERROR MESSAGE"
                    //        "reason": "reason for error",
                    //        "reasonCode": 00000,
                    //        "details": {
                    //              "statusCode": 000,
                    //              "statusMessage": "reason for error",
                    //              "body": "description of error"
                    //             }
                    //    }

                    //{
                    //      "error": "ERROR_MEDIA_DATA",
                    //      "reason": "Cannot identify image format",
                    //      "reasonCode": 40012,
                    //      "details": {
                    //            "statusCode": 406,
                    //            "statusMessage": "Cannot identify image format",
                    //            "body": "image error cannot identify image file (-6)"
                    //      }
                    //}


                    string error = "";
                    swposttime.Stop();
                    if (ret.JsonString != null && !string.IsNullOrWhiteSpace(ret.JsonString))
                    {
                        SightHoundError SHErr = JsonConvert.DeserializeObject<SightHoundError>(ret.JsonString);
                        if (!string.IsNullOrEmpty(SHErr.Details.Body))
                            error = $"{SHErr.Error} (ReasonCode={SHErr.ReasonCode}): {SHErr.Details.Body}";
                        else
                            error = $"{SHErr.Error} (ReasonCode={SHErr.ReasonCode}): {SHErr.Reason}";
                    }
                    else
                    {
                        error = "(EmptyJsonResponse?)";
                    }


                    swposttime.Stop();
                    ret.Error = $"ERROR: Got http status code '{output.StatusCode}' ({Convert.ToInt32(output.StatusCode)}) - '{error}' in {swposttime.ElapsedMilliseconds}ms: {output.ReasonPhrase}";
                    AiUrl.IncrementError();
                    AiUrl.LastResultMessage = ret.Error;
                }

            }
            catch (Exception ex)
            {

                string error = "";

                swposttime.Stop();
                long seconds = swposttime.ElapsedMilliseconds / 1000;
                if (seconds >= AiUrl.GetTimeout().TotalSeconds)
                {
                    ret.Error = $"ERROR: HTTPClient timeout at {seconds} seconds (Max={AiUrl.GetTimeout().TotalSeconds} set in 'HTTPClientTimeoutSeconds' in aitool.settings.json): - '{error}': {ex.Msg()}";
                }
                else
                {
                    ret.Error = $"ERROR: '{error}': {ex.Msg()}";
                }
                AiUrl.IncrementError();
                AiUrl.LastResultMessage = ret.Error;
            }
            finally
            {
                ret.SWPostTime = swposttime.ElapsedMilliseconds;
                AiUrl.LastTimeMS = (int)swposttime.ElapsedMilliseconds;
                AiUrl.AITimeCalcs.AddToCalc(AiUrl.LastTimeMS);
                if (request != null)
                    request.Dispose();
            }

            return ret;
        }
    }
}
