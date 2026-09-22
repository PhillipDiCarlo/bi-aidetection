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
    /// <summary>Amazon Rekognition object (label) and face detection.</summary>
    public class AwsRekognitionProvider : IAIProvider
    {
        public Task<ClsAIServerResponse> DetectAsync(ClsImageQueueItem CurImg, ClsURLItem AiUrl, Camera cam, CancellationToken ct)
        {
            if (AiUrl.Type == URLTypeEnum.AWSRekognition_Faces)
                return DetectFacesAsync(CurImg, AiUrl, cam, ct);
            return DetectObjectsAsync(CurImg, AiUrl, cam, ct);
        }

        private async Task<ClsAIServerResponse> DetectObjectsAsync(ClsImageQueueItem CurImg, ClsURLItem AiUrl, Camera cam, CancellationToken ct)
        {
            using var Trace = new Trace();

            ClsAIServerResponse ret = new ClsAIServerResponse();
            ret.AIURL = AiUrl;
            bool OverrideThreshold = AiUrl.Threshold_Lower > 0 || (AiUrl.Threshold_Upper > 0 && AiUrl.Threshold_Upper < 100);

            Stopwatch swposttime = new Stopwatch();

            try
            {

                //string testjson = JsonConvert.SerializeObject(cdr);

                long FileSize = new FileInfo(CurImg.image_path).Length;
                //https://docs.aws.amazon.com/general/latest/gr/rande.html


                RegionEndpoint endpoint = RegionEndpoint.GetBySystemName(AppSettings.Settings.AmazonRegionEndpoint);
                AmazonRekognitionClient rekognitionClient = new AmazonRekognitionClient(new BasicAWSCredentials(AppSettings.Settings.AmazonAccessKeyId, AppSettings.Settings.AmazonSecretKey), endpoint);

                DetectLabelsRequest dlr = new DetectLabelsRequest();

                dlr.MaxLabels = AppSettings.Settings.AmazonMaxLabels;
                dlr.MinConfidence = AppSettings.Settings.AmazonMinConfidence;

                if (AppSettings.Settings.HistoryRestrictMinThresholdAtSource && !OverrideThreshold)
                    dlr.MinConfidence = cam.threshold_lower;

                if (AppSettings.Settings.HistoryRestrictMinThresholdAtSource && OverrideThreshold)
                    dlr.MinConfidence = AiUrl.Threshold_Lower;

                Amazon.Rekognition.Model.Image rekognitionImage = new Amazon.Rekognition.Model.Image();

                //byte[] data = null;

                //using (FileStream fileStream = new FileStream(CurImg.image_path, FileMode.Open, FileAccess.Read))
                //{
                //    data = new byte[fileStream.Length];
                //    await fileStream.ReadAsync(data, 0, (int)fileStream.Length);
                //}

                rekognitionImage.Bytes = CurImg.ToMemStream();

                dlr.Image = rekognitionImage;


                Log($"Debug: (1/6) Uploading a {Global.FormatBytes(FileSize)} image to '{AiUrl.Type}' AI Server at {AiUrl}", AiUrl.CurSrv, cam, CurImg);

                swposttime.Restart();

                DetectLabelsResponse response = await rekognitionClient.DetectLabelsAsync(dlr);

                swposttime.Stop();

                if (response != null)
                {
                    ret.StatusCode = response.HttpStatusCode;
                    if (response.HttpStatusCode == System.Net.HttpStatusCode.OK)
                    {
                        if (response.Labels.Count > 0)
                        {

                            foreach (Amazon.Rekognition.Model.Label lbl in response.Labels)
                            {
                                //not sure if there will ever be more than one instance
                                for (int i = 0; i < lbl.Instances.Count; i++)
                                {
                                    ClsPrediction pred = new ClsPrediction(ObjectType.Object, cam, lbl, i, CurImg, AiUrl);

                                    ret.Predictions.Add(pred);

                                }

                            }


                        }

                        ret.Success = true;
                        AiUrl.LastResultMessage = $"{ret.Predictions.Count} predictions found.";
                    }
                    else
                    {
                        ret.Error = $"ERROR: Amazon Rekognition 'HttpStatusCode' is '{response.HttpStatusCode}' ({Convert.ToInt32(response.HttpStatusCode)}).";
                        AiUrl.IncrementError();
                        AiUrl.LastResultMessage = ret.Error;
                    }
                }
                else
                {
                    ret.Error = $"ERROR: Amazon Rekognition 'Response' is null.";
                    AiUrl.IncrementError();
                    AiUrl.LastResultMessage = ret.Error;
                }


            }
            catch (Exception ex)
            {
                swposttime.Stop();

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

        private async Task<ClsAIServerResponse> DetectFacesAsync(ClsImageQueueItem CurImg, ClsURLItem AiUrl, Camera cam, CancellationToken ct)
        {
            using var Trace = new Trace();

            ClsAIServerResponse ret = new ClsAIServerResponse();
            ret.AIURL = AiUrl;
            bool OverrideThreshold = AiUrl.Threshold_Lower > 0 || (AiUrl.Threshold_Upper > 0 && AiUrl.Threshold_Upper < 100);

            Stopwatch swposttime = new Stopwatch();

            try
            {

                //string testjson = JsonConvert.SerializeObject(cdr);

                long FileSize = new FileInfo(CurImg.image_path).Length;
                //https://docs.aws.amazon.com/general/latest/gr/rande.html


                RegionEndpoint endpoint = RegionEndpoint.GetBySystemName(AppSettings.Settings.AmazonRegionEndpoint);
                AmazonRekognitionClient rekognitionClient = new AmazonRekognitionClient(new BasicAWSCredentials(AppSettings.Settings.AmazonAccessKeyId, AppSettings.Settings.AmazonSecretKey), endpoint);

                DetectFacesRequest dlr = new DetectFacesRequest();

                //dlr.MaxLabels = AppSettings.Settings.AmazonMaxLabels;
                //dlr.MinConfidence = AppSettings.Settings.AmazonMinConfidence;

                //if (AppSettings.Settings.HistoryRestrictMinThresholdAtSource && !OverrideThreshold)
                //    dlr.MinConfidence = cam.threshold_lower;

                //if (AppSettings.Settings.HistoryRestrictMinThresholdAtSource && OverrideThreshold)
                //    dlr.MinConfidence = AiUrl.Threshold_Lower;

                Amazon.Rekognition.Model.Image rekognitionImage = new Amazon.Rekognition.Model.Image();

                //byte[] data = null;

                //using (FileStream fileStream = new FileStream(CurImg.image_path, FileMode.Open, FileAccess.Read))
                //{
                //    data = new byte[fileStream.Length];
                //    await fileStream.ReadAsync(data, 0, (int)fileStream.Length);
                //}

                rekognitionImage.Bytes = CurImg.ToMemStream();

                dlr.Image = rekognitionImage;
                dlr.Attributes.Add("ALL");

                Log($"Debug: (1/6) Uploading a {Global.FormatBytes(FileSize)} image to '{AiUrl.Type}' AI Server at {AiUrl}", AiUrl.CurSrv, cam, CurImg);

                swposttime.Restart();

                DetectFacesResponse response = await rekognitionClient.DetectFacesAsync(dlr);

                swposttime.Stop();

                if (response != null)
                {
                    ret.StatusCode = response.HttpStatusCode;
                    if (response.HttpStatusCode == System.Net.HttpStatusCode.OK)
                    {
                        if (response.FaceDetails.Count > 0)
                        {

                            foreach (Amazon.Rekognition.Model.FaceDetail face in response.FaceDetails)
                            {
                                ClsPrediction pred = new ClsPrediction(ObjectType.Object, cam, face, CurImg, AiUrl);

                                ret.Predictions.Add(pred);

                            }


                        }

                        ret.Success = true;
                        AiUrl.LastResultMessage = $"{ret.Predictions.Count} predictions found.";
                    }
                    else
                    {
                        ret.Error = $"ERROR: Amazon Rekognition 'HttpStatusCode' is '{response.HttpStatusCode}' ({Convert.ToInt32(response.HttpStatusCode)}).";
                        AiUrl.IncrementError();
                        AiUrl.LastResultMessage = ret.Error;
                    }
                }
                else
                {
                    ret.Error = $"ERROR: Amazon Rekognition 'Response' is null.";
                    AiUrl.IncrementError();
                    AiUrl.LastResultMessage = ret.Error;
                }


            }
            catch (Exception ex)
            {
                swposttime.Stop();

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
