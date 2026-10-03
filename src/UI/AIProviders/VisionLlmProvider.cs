using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using static AITool.AITOOL;
using static AITool.Global;

namespace AITool.AIProviders
{
    /// <summary>
    /// Any OpenAI-compatible /v1/chat/completions vision endpoint (Ollama, LM Studio, OpenAI, OpenRouter, Gemini's
    /// OpenAI-compat endpoint) plus the native Anthropic Messages API. Used as a refinement server: the description
    /// comes back as a full-image "Scene" prediction (same pattern DeepStackCompatibleProvider uses), and any objects
    /// the model localizes come back as their own predictions so they can merge into the original detections.
    /// </summary>
    public class VisionLlmProvider : IAIProvider
    {
        public const string DefaultPrompt = "Look at this security camera image and respond with ONLY a JSON object (no markdown, no commentary) in this exact form: " +
            "{\"description\":\"one sentence describing the scene\",\"objects\":[{\"label\":\"person\",\"detail\":\"carrying a package\",\"confidence\":0.9,\"box\":[x_min,y_min,x_max,y_max]}]}. " +
            "Box coordinates are normalized 0..1 (left,top,right,bottom) - omit \"box\" for any object you cannot localize, and omit \"objects\" entirely if there is nothing notable.";

        public async Task<ClsAIServerResponse> DetectAsync(ClsImageQueueItem CurImg, ClsURLItem AiUrl, Camera cam, CancellationToken ct)
        {
            using var Trace = new Trace();

            ClsAIServerResponse ret = new ClsAIServerResponse();
            ret.AIURL = AiUrl;

            bool IsAnthropic = AiUrl.Type == URLTypeEnum.Anthropic_Vision;

            if (IsAnthropic && AiUrl.ApiKey.IsEmpty())
            {
                ret.Error = $"ERROR: No API key set for '{AiUrl.Name}'. (ApiKey in the AI Server edit screen).";
                AiUrl.IncrementError();
                AiUrl.LastResultMessage = ret.Error;
                return ret;
            }

            Stopwatch swposttime = new Stopwatch();
            string cleanjsonString = "";

            try
            {
                long FileSize = new FileInfo(CurImg.image_path).Length;

                string base64Jpeg = await ResizeToBase64JpegAsync(CurImg, AiUrl.ImageMaxDimension > 0 ? AiUrl.ImageMaxDimension : 1024);

                string modelName = AiUrl.ModelName.IsNotEmpty() ? AiUrl.ModelName : (IsAnthropic ? "claude-sonnet-5" : "llava");
                string prompt = AITOOL.ReplaceParams(cam, null, CurImg, AiUrl.Prompt.IsNotEmpty() ? AiUrl.Prompt : DefaultPrompt, Global.IPType.Path);
                int maxTokens = AiUrl.MaxTokens > 0 ? AiUrl.MaxTokens : 512;

                using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, AiUrl.ToString());

                JObject body;
                if (IsAnthropic)
                {
                    body = BuildAnthropicRequest(modelName, prompt, base64Jpeg, maxTokens);
                    ApplyAnthropicHeaders(request, AiUrl.ApiKey);
                }
                else
                {
                    body = BuildOpenAiRequest(modelName, prompt, base64Jpeg, maxTokens);
                    ApplyOpenAiHeaders(request, AiUrl.ApiKey);
                }

                request.Content = new StringContent(body.ToString(Formatting.None), Encoding.UTF8, "application/json");

                Log($"Debug: (1/6) Uploading a {Global.FormatBytes(FileSize)} image to '{AiUrl.Type}' AI Server at {AiUrl}", AiUrl.CurSrv, cam, CurImg);

                swposttime.Restart();

                using HttpResponseMessage output = await AiUrl.HttpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead, ct);

                swposttime.Stop();
                ret.StatusCode = output.StatusCode;
                ret.JsonString = await output.Content.ReadAsStringAsync();

                if (output.IsSuccessStatusCode)
                {
                    if (ret.JsonString.IsNotEmpty())
                    {
                        cleanjsonString = ret.JsonString.CleanString();

                        try
                        {
                            string rawText = IsAnthropic ? ExtractAnthropicText(ret.JsonString) : ExtractOpenAiText(ret.JsonString);

                            ParsedVisionReply reply = ParseModelReply(rawText);

                            if (reply.Description.IsNotEmpty())
                            {
                                //Same "Scene" pattern DeepStackCompatibleProvider uses for a full-image description:
                                //try to create a rectangle smaller than the image so the label will fit
                                ClsDeepstackDetection spred = new ClsDeepstackDetection();
                                spred.label = "Scene";
                                spred.Detail = reply.Description;
                                spred.confidence = 1;
                                spred.x_min = 5;
                                spred.y_min = 5;
                                spred.x_max = CurImg.Width - 5;
                                spred.y_max = CurImg.Height - 40;
                                ret.Predictions.Add(new ClsPrediction(ObjectType.Object, cam, spred, CurImg, AiUrl));
                            }

                            foreach (ParsedVisionObject obj in reply.Objects)
                            {
                                ClsDeepstackDetection DSObj = ToDeepstackDetection(obj, CurImg.Width, CurImg.Height);
                                ret.Predictions.Add(new ClsPrediction(ObjectType.Object, cam, DSObj, CurImg, AiUrl));
                            }

                            ret.Success = true;
                            AiUrl.LastResultMessage = $"{ret.Predictions.Count} predictions found.";
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
                    ret.Error = $"ERROR: Got http status code '{output.StatusCode}' ({Convert.ToInt32(output.StatusCode)}) in {swposttime.ElapsedMilliseconds}ms: {output.ReasonPhrase}. Body: '{ret.JsonString.Truncate()}'";
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
                ret.SWPostTime = swposttime.ElapsedMilliseconds;
                AiUrl.AITimeCalcs.AddToCalc(AiUrl.LastTimeMS);
            }

            return ret;
        }

        //================================================================================
        // Request building, response parsing, and coordinate mapping are static/internal so they can be
        // unit tested without any network access.
        //================================================================================

        internal static JObject BuildOpenAiRequest(string modelName, string prompt, string base64Jpeg, int maxTokens)
        {
            return new JObject
            {
                ["model"] = modelName,
                ["max_tokens"] = maxTokens,
                ["messages"] = new JArray
                {
                    new JObject
                    {
                        ["role"] = "user",
                        ["content"] = new JArray
                        {
                            new JObject { ["type"] = "text", ["text"] = prompt },
                            new JObject
                            {
                                ["type"] = "image_url",
                                ["image_url"] = new JObject { ["url"] = $"data:image/jpeg;base64,{base64Jpeg}" }
                            }
                        }
                    }
                }
            };
        }

        internal static JObject BuildAnthropicRequest(string modelName, string prompt, string base64Jpeg, int maxTokens)
        {
            return new JObject
            {
                ["model"] = modelName,
                ["max_tokens"] = maxTokens,
                ["messages"] = new JArray
                {
                    new JObject
                    {
                        ["role"] = "user",
                        ["content"] = new JArray
                        {
                            new JObject
                            {
                                ["type"] = "image",
                                ["source"] = new JObject
                                {
                                    ["type"] = "base64",
                                    ["media_type"] = "image/jpeg",
                                    ["data"] = base64Jpeg
                                }
                            },
                            new JObject { ["type"] = "text", ["text"] = prompt }
                        }
                    }
                }
            };
        }

        internal static void ApplyOpenAiHeaders(HttpRequestMessage request, string apiKey)
        {
            //Local servers such as Ollama or LM Studio don't require a key - only add the header if one was configured
            if (apiKey.IsNotEmpty())
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
        }

        internal static void ApplyAnthropicHeaders(HttpRequestMessage request, string apiKey)
        {
            request.Headers.Add("x-api-key", apiKey ?? "");
            request.Headers.Add("anthropic-version", "2023-06-01");
        }

        internal static string ExtractOpenAiText(string json)
        {
            JObject jo = JObject.Parse(json);
            return (string)jo["choices"]?[0]?["message"]?["content"] ?? "";
        }

        internal static string ExtractAnthropicText(string json)
        {
            JObject jo = JObject.Parse(json);
            JArray content = jo["content"] as JArray;
            if (content != null)
            {
                foreach (JToken block in content)
                {
                    if ((string)block["type"] == "text")
                        return (string)block["text"] ?? "";
                }
            }
            return "";
        }

        internal class ParsedVisionObject
        {
            public string Label = "";
            public string Detail = "";
            public double Confidence = 0;
            public double[] Box = null;  //normalized 0..1: [x_min, y_min, x_max, y_max], null if the model didn't localize it
        }

        internal class ParsedVisionReply
        {
            public string Description = "";
            public List<ParsedVisionObject> Objects = new List<ParsedVisionObject>();
        }

        /// <summary>
        /// Parses leniently: strips markdown code fences, then takes the first '{' through the last '}'. If that
        /// isn't valid JSON (or there's no '{' at all), the whole reply text is treated as the description.
        /// </summary>
        internal static ParsedVisionReply ParseModelReply(string rawText)
        {
            ParsedVisionReply ret = new ParsedVisionReply();

            if (rawText.IsEmpty())
                return ret;

            string text = rawText.Trim();

            if (text.StartsWith("```"))
            {
                int firstNewline = text.IndexOf('\n');
                text = firstNewline >= 0 ? text.Substring(firstNewline + 1) : text.TrimStart('`');
                if (text.EndsWith("```"))
                    text = text.Substring(0, text.Length - 3);
                text = text.Trim();
            }

            int start = text.IndexOf('{');
            int end = text.LastIndexOf('}');

            if (start >= 0 && end > start)
            {
                try
                {
                    JObject jo = JObject.Parse(text.Substring(start, end - start + 1));

                    ret.Description = (string)jo["description"] ?? "";

                    if (jo["objects"] is JArray objs)
                    {
                        foreach (JToken t in objs)
                        {
                            ParsedVisionObject obj = new ParsedVisionObject();
                            obj.Label = (string)t["label"] ?? "";
                            obj.Detail = (string)t["detail"] ?? "";
                            obj.Confidence = t["confidence"] != null ? (double)t["confidence"] : 0;

                            if (t["box"] is JArray box && box.Count == 4)
                                obj.Box = box.Select(b => (double)b).ToArray();

                            if (obj.Label.IsNotEmpty())
                                ret.Objects.Add(obj);
                        }
                    }

                    return ret;
                }
                catch (JsonException)
                {
                    //not valid JSON after all - fall through and treat the whole reply as the description
                }
            }

            ret.Description = text;
            return ret;
        }

        /// <summary>Maps a parsed object's normalized 0..1 box to pixel coordinates. A missing box covers the full image.</summary>
        internal static ClsDeepstackDetection ToDeepstackDetection(ParsedVisionObject obj, int imageWidth, int imageHeight)
        {
            ClsDeepstackDetection ret = new ClsDeepstackDetection();
            ret.label = obj.Label;
            ret.Detail = obj.Detail;
            ret.confidence = obj.Confidence;

            if (obj.Box != null && obj.Box.Length == 4)
            {
                ret.x_min = obj.Box[0] * imageWidth;
                ret.y_min = obj.Box[1] * imageHeight;
                ret.x_max = obj.Box[2] * imageWidth;
                ret.y_max = obj.Box[3] * imageHeight;
            }
            else
            {
                ret.x_min = 0;
                ret.y_min = 0;
                ret.x_max = imageWidth;
                ret.y_max = imageHeight;
            }

            return ret;
        }

        /// <summary>Downscales so neither dimension exceeds maxDimension (keeping aspect ratio), then encodes as base64 JPEG - keeps request size/cost down.</summary>
        internal static async Task<string> ResizeToBase64JpegAsync(ClsImageQueueItem CurImg, int maxDimension)
        {
            using SixLabors.ImageSharp.Image image = await SixLabors.ImageSharp.Image.LoadAsync(CurImg.ToMemStream());

            if (image.Width > maxDimension || image.Height > maxDimension)
                image.Mutate(i => i.Resize(new ResizeOptions { Mode = ResizeMode.Max, Size = new SixLabors.ImageSharp.Size(maxDimension, maxDimension) }));

            using MemoryStream ms = new MemoryStream();
            await image.SaveAsJpegAsync(ms, new JpegEncoder { Quality = 85 });
            return Convert.ToBase64String(ms.ToArray());
        }
    }
}
