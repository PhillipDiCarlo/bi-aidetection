using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using static AITool.AITOOL;

namespace AITool.AIProviders
{
    /// <summary>Runs a local YOLOv8/v11 .onnx model in-process via ONNX Runtime (DirectML GPU, falling back to CPU). No external server needed.</summary>
    public class OnnxYoloProvider : IAIProvider
    {
        //One InferenceSession per model file path, created lazily and reused across calls/threads - InferenceSession.Run is thread-safe once created.
        private static readonly ConcurrentDictionary<string, Lazy<InferenceSession>> Sessions = new ConcurrentDictionary<string, Lazy<InferenceSession>>(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, List<string>> ClassNamesCache = new ConcurrentDictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        //Only one default-model download should run at a time - other callers that race in just wait for it to finish.
        private static readonly SemaphoreSlim DownloadLock = new SemaphoreSlim(1, 1);
        private const long MinModelFileSizeBytes = 1_000_000; //1MB - a truncated/failed download will be much smaller than the real ~6MB yolov8n.onnx

        private const float NmsIouThreshold = 0.45f;
        private const float DefaultConfThreshold = 0.25f;

        public async Task<ClsAIServerResponse> DetectAsync(ClsImageQueueItem CurImg, ClsURLItem AiUrl, Camera cam, CancellationToken ct)
        {
            using var Trace = new Trace();

            ClsAIServerResponse ret = new ClsAIServerResponse();
            ret.AIURL = AiUrl;

            Stopwatch sw = Stopwatch.StartNew();

            try
            {
                string modelPath = AiUrl.url;

                if (modelPath.IsNotEmpty() && !File.Exists(modelPath) && modelPath.Equals(AppSettings.Settings.OnnxDefaultModelPath, StringComparison.OrdinalIgnoreCase))
                {
                    string downloadError = await EnsureDefaultModelDownloadedAsync(modelPath, ct);
                    if (downloadError.IsNotEmpty())
                    {
                        ret.Error = $"ERROR: ONNX model file not found: '{modelPath}'. Export one (e.g. with Ultralytics: 'pip install ultralytics' then 'yolo export model=yolo11n.pt format=onnx') and set this AI server's URL field to the path of the resulting .onnx file. Also tried to auto-download the default model but failed: {downloadError}";
                        AiUrl.IncrementError();
                        AiUrl.LastResultMessage = ret.Error;
                        return ret;
                    }
                }

                if (modelPath.IsEmpty() || !File.Exists(modelPath))
                {
                    ret.Error = $"ERROR: ONNX model file not found: '{modelPath}'. The default model downloads automatically the first time it's needed - see docs/local-detection.md. To use a different model, export one (e.g. with Ultralytics: 'pip install ultralytics' then 'yolo export model=yolo11n.pt format=onnx') and set this AI server's URL field to the path of the resulting .onnx file.";
                    AiUrl.IncrementError();
                    AiUrl.LastResultMessage = ret.Error;
                    return ret;
                }

                InferenceSession session = GetOrCreateSession(modelPath, AiUrl.OnnxUseGpu);
                List<string> classNames = GetClassNames(modelPath);

                string inputName = session.InputMetadata.Keys.First();
                int[] inputDims = session.InputMetadata[inputName].Dimensions;
                int inputSize = YoloPostProcessor.ResolveInputSize(inputDims.Length >= 4 ? inputDims[2] : -1);

                using Image<Rgb24> image = Image.Load<Rgb24>(CurImg.ToMemStream());
                int origWidth = image.Width;
                int origHeight = image.Height;

                //letterbox resize: fit within inputSize x inputSize keeping aspect ratio, pad with gray (114) - matches YoloPostProcessor.ComputeLetterbox
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size = new SixLabors.ImageSharp.Size(inputSize, inputSize),
                    Mode = ResizeMode.Pad,
                    PadColor = new Rgba32(114, 114, 114, 255),
                }));

                DenseTensor<float> inputTensor = new DenseTensor<float>(new[] { 1, 3, inputSize, inputSize });
                image.ProcessPixelRows(accessor =>
                {
                    for (int y = 0; y < accessor.Height; y++)
                    {
                        Span<Rgb24> row = accessor.GetRowSpan(y);
                        for (int x = 0; x < row.Length; x++)
                        {
                            Rgb24 px = row[x];
                            inputTensor[0, 0, y, x] = px.R / 255f;
                            inputTensor[0, 1, y, x] = px.G / 255f;
                            inputTensor[0, 2, y, x] = px.B / 255f;
                        }
                    }
                });

                List<NamedOnnxValue> inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(inputName, inputTensor) };

                float confThreshold = AiUrl.Threshold_Lower > 0 ? (float)(AiUrl.Threshold_Lower / 100.0) : DefaultConfThreshold;

                using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = await Task.Run(() => session.Run(inputs), ct);

                Tensor<float> outputTensor = results.First().AsTensor<float>();
                float[] outputData = outputTensor.ToArray();
                int[] dims = outputTensor.Dimensions.ToArray();

                List<YoloPostProcessor.YoloBox> boxes = YoloPostProcessor.DecodeOutput(outputData, dims, classNames.Count, confThreshold);
                boxes = YoloPostProcessor.Nms(boxes, NmsIouThreshold);

                YoloPostProcessor.LetterboxTransform transform = YoloPostProcessor.ComputeLetterbox(origWidth, origHeight, inputSize);

                foreach (YoloPostProcessor.YoloBox box in boxes)
                {
                    (float x1, float y1, float x2, float y2) = YoloPostProcessor.MapBoxToOriginal(box.X1, box.Y1, box.X2, box.Y2, transform, origWidth, origHeight);

                    ClsDeepstackDetection DSObj = new ClsDeepstackDetection();
                    DSObj.label = box.ClassId < classNames.Count ? classNames[box.ClassId] : $"Class{box.ClassId}";
                    DSObj.confidence = box.Score;
                    DSObj.x_min = x1;
                    DSObj.y_min = y1;
                    DSObj.x_max = x2;
                    DSObj.y_max = y2;

                    ClsPrediction pred = new ClsPrediction(ObjectType.Object, cam, DSObj, CurImg, AiUrl);

                    ret.Predictions.Add(pred);
                }

                ret.Success = true;
                AiUrl.LastResultMessage = $"{ret.Predictions.Count} predictions found.";
            }
            catch (Exception ex)
            {
                ret.Error = $"ERROR: {ex.Msg()}";
                AiUrl.IncrementError();
                AiUrl.LastResultMessage = ret.Error;
            }
            finally
            {
                sw.Stop();
                AiUrl.LastTimeMS = (int)sw.ElapsedMilliseconds;
                ret.SWPostTime = sw.ElapsedMilliseconds;
                AiUrl.AITimeCalcs.AddToCalc(AiUrl.LastTimeMS);
            }

            return ret;
        }

        //Downloads AppSettings.Settings.OnnxDefaultModelUrl to modelPath if it's still missing once this call gets the lock (another
        //thread may have already finished it while we were waiting). Returns "" on success, or an error message on failure.
        private static async Task<string> EnsureDefaultModelDownloadedAsync(string modelPath, CancellationToken ct)
        {
            await DownloadLock.WaitAsync(ct);
            try
            {
                if (File.Exists(modelPath) && new FileInfo(modelPath).Length > MinModelFileSizeBytes)
                    return "";

                string modelUrl = AppSettings.Settings.OnnxDefaultModelUrl;
                if (modelUrl.IsEmpty())
                    return "OnnxDefaultModelUrl is not set.";

                string tempPath = modelPath + ".download";

                try
                {
                    Log($"Debug: Default ONNX model not found at '{modelPath}', downloading from '{modelUrl}'...");

                    Directory.CreateDirectory(Path.GetDirectoryName(modelPath));

                    using (HttpClient client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) })
                    using (HttpResponseMessage response = await client.GetAsync(modelUrl, HttpCompletionOption.ResponseHeadersRead, ct))
                    {
                        response.EnsureSuccessStatusCode();

                        using Stream httpStream = await response.Content.ReadAsStreamAsync(ct);
                        using FileStream fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write);
                        await httpStream.CopyToAsync(fileStream, ct);
                    }

                    long size = new FileInfo(tempPath).Length;
                    if (size < MinModelFileSizeBytes)
                    {
                        File.Delete(tempPath);
                        return $"Downloaded file was too small ({size} bytes) - the download likely failed.";
                    }

                    File.Move(tempPath, modelPath, true);

                    Log($"Debug: Downloaded default ONNX model to '{modelPath}' ({size / 1024 / 1024}MB).");

                    return "";
                }
                catch (Exception ex)
                {
                    try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { /* best effort cleanup */ }
                    return ex.Msg();
                }
            }
            finally
            {
                DownloadLock.Release();
            }
        }

        private static InferenceSession GetOrCreateSession(string modelPath, bool useGpu)
        {
            //Lazy<T> makes concurrent first-time creation for the same path safe without a global lock on every call
            return Sessions.GetOrAdd(modelPath, path => new Lazy<InferenceSession>(() => CreateSession(path, useGpu), LazyThreadSafetyMode.ExecutionAndPublication)).Value;
        }

        private static InferenceSession CreateSession(string modelPath, bool useGpu)
        {
            SessionOptions options = new SessionOptions();

            if (useGpu)
            {
                try
                {
                    options.AppendExecutionProvider_DML(0);
                }
                catch (Exception ex)
                {
                    Log($"Debug: DirectML execution provider unavailable, falling back to CPU for '{modelPath}': {ex.Msg()}");
                }
            }

            return new InferenceSession(modelPath, options);
        }

        private static List<string> GetClassNames(string modelPath)
        {
            return ClassNamesCache.GetOrAdd(modelPath, path =>
            {
                foreach (string ext in new[] { ".names", ".txt" })
                {
                    string sidecar = Path.ChangeExtension(path, ext);
                    if (File.Exists(sidecar))
                    {
                        List<string> names = File.ReadAllLines(sidecar).Select(l => l.Trim()).Where(l => l.IsNotEmpty()).ToList();
                        if (names.Count > 0)
                            return names;
                    }
                }

                return CocoClassNames.ToList();
            });
        }

        //Default class list for a stock (non-custom-trained) YOLOv8/v11 model
        private static readonly string[] CocoClassNames = new[]
        {
            "person","bicycle","car","motorcycle","airplane","bus","train","truck","boat","traffic light",
            "fire hydrant","stop sign","parking meter","bench","bird","cat","dog","horse","sheep","cow",
            "elephant","bear","zebra","giraffe","backpack","umbrella","handbag","tie","suitcase","frisbee",
            "skis","snowboard","sports ball","kite","baseball bat","baseball glove","skateboard","surfboard","tennis racket","bottle",
            "wine glass","cup","fork","knife","spoon","bowl","banana","apple","sandwich","orange",
            "broccoli","carrot","hot dog","pizza","donut","cake","chair","couch","potted plant","bed",
            "dining table","toilet","tv","laptop","mouse","remote","keyboard","cell phone","microwave","oven",
            "toaster","sink","refrigerator","book","clock","vase","scissors","teddy bear","hair drier","toothbrush",
        };
    }
}
