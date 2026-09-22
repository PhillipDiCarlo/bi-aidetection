using System;
using System.Collections.Generic;
using System.Linq;

namespace AITool.AIProviders
{
    /// <summary>
    /// Pure math for YOLOv8/v11 ONNX pre/post-processing: letterbox transform, output decoding and NMS.
    /// Kept free of ONNX Runtime/ImageSharp types so it can be unit tested without a model or an image.
    /// </summary>
    public static class YoloPostProcessor
    {
        public readonly struct LetterboxTransform
        {
            public double Scale { get; init; }
            public double PadX { get; init; }
            public double PadY { get; init; }
        }

        public readonly struct YoloBox
        {
            public float X1 { get; init; }
            public float Y1 { get; init; }
            public float X2 { get; init; }
            public float Y2 { get; init; }
            public float Score { get; init; }
            public int ClassId { get; init; }
        }

        /// <summary>Same scale/center-pad math ImageSharp's ResizeMode.Pad uses, so boxes predicted on the padded image can be mapped back.</summary>
        public static LetterboxTransform ComputeLetterbox(int origWidth, int origHeight, int targetSize)
        {
            double scale = Math.Min((double)targetSize / origWidth, (double)targetSize / origHeight);
            double newWidth = origWidth * scale;
            double newHeight = origHeight * scale;

            return new LetterboxTransform
            {
                Scale = scale,
                PadX = (targetSize - newWidth) / 2.0,
                PadY = (targetSize - newHeight) / 2.0,
            };
        }

        /// <summary>Undoes ComputeLetterbox and clamps to the original image bounds.</summary>
        public static (float X1, float Y1, float X2, float Y2) MapBoxToOriginal(float x1, float y1, float x2, float y2, LetterboxTransform t, int origWidth, int origHeight)
        {
            float ox1 = (float)((x1 - t.PadX) / t.Scale);
            float oy1 = (float)((y1 - t.PadY) / t.Scale);
            float ox2 = (float)((x2 - t.PadX) / t.Scale);
            float oy2 = (float)((y2 - t.PadY) / t.Scale);

            ox1 = Math.Clamp(ox1, 0, origWidth);
            oy1 = Math.Clamp(oy1, 0, origHeight);
            ox2 = Math.Clamp(ox2, 0, origWidth);
            oy2 = Math.Clamp(oy2, 0, origHeight);

            return (ox1, oy1, ox2, oy2);
        }

        /// <summary>Falls back to the model's default (e.g. 640) when the ONNX input dimension is dynamic (&lt;= 0).</summary>
        public static int ResolveInputSize(long modelDim, int defaultSize = 640)
        {
            return modelDim > 0 ? (int)modelDim : defaultSize;
        }

        /// <summary>
        /// Decodes a raw YOLOv8/v11 output tensor into boxes in letterboxed (model input) pixel space.
        /// Handles both the usual channels-first [1, 4+nc, N] layout (needs transpose) and [1, N, 4+nc].
        /// </summary>
        public static List<YoloBox> DecodeOutput(float[] data, int[] dims, int numClasses, float confThreshold)
        {
            if (dims.Length != 3 || dims[0] != 1)
                throw new ArgumentException($"Expected a [1, C, N] or [1, N, C] output tensor, got [{string.Join(",", dims)}]");

            int channels = 4 + numClasses;
            bool channelsFirst;
            int numBoxes;

            if (dims[1] == channels)
            {
                channelsFirst = true;
                numBoxes = dims[2];
            }
            else if (dims[2] == channels)
            {
                channelsFirst = false;
                numBoxes = dims[1];
            }
            else
            {
                //numClasses didn't match either dimension (e.g. mismatched/missing .names file) - fall back to
                //assuming the smaller dimension is the channel one, since N (anchors) is normally in the thousands
                channelsFirst = dims[1] <= dims[2];
                channels = channelsFirst ? dims[1] : dims[2];
                numBoxes = channelsFirst ? dims[2] : dims[1];
                numClasses = channels - 4;
            }

            List<YoloBox> ret = new List<YoloBox>();

            for (int i = 0; i < numBoxes; i++)
            {
                float GetVal(int channelIdx) => channelsFirst ? data[channelIdx * numBoxes + i] : data[i * channels + channelIdx];

                float cx = GetVal(0);
                float cy = GetVal(1);
                float w = GetVal(2);
                float h = GetVal(3);

                int bestClass = -1;
                float bestScore = 0;
                for (int c = 0; c < numClasses; c++)
                {
                    float score = GetVal(4 + c);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestClass = c;
                    }
                }

                if (bestClass >= 0 && bestScore >= confThreshold)
                {
                    ret.Add(new YoloBox
                    {
                        X1 = cx - w / 2f,
                        Y1 = cy - h / 2f,
                        X2 = cx + w / 2f,
                        Y2 = cy + h / 2f,
                        Score = bestScore,
                        ClassId = bestClass,
                    });
                }
            }

            return ret;
        }

        public static float IoU(YoloBox a, YoloBox b)
        {
            float x1 = Math.Max(a.X1, b.X1);
            float y1 = Math.Max(a.Y1, b.Y1);
            float x2 = Math.Min(a.X2, b.X2);
            float y2 = Math.Min(a.Y2, b.Y2);

            float interArea = Math.Max(0, x2 - x1) * Math.Max(0, y2 - y1);
            float areaA = Math.Max(0, a.X2 - a.X1) * Math.Max(0, a.Y2 - a.Y1);
            float areaB = Math.Max(0, b.X2 - b.X1) * Math.Max(0, b.Y2 - b.Y1);
            float union = areaA + areaB - interArea;

            return union <= 0 ? 0 : interArea / union;
        }

        /// <summary>Class-aware NMS: boxes of different classes never suppress each other.</summary>
        public static List<YoloBox> Nms(IEnumerable<YoloBox> boxes, float iouThreshold)
        {
            List<YoloBox> ret = new List<YoloBox>();

            foreach (IGrouping<int, YoloBox> group in boxes.GroupBy(b => b.ClassId))
            {
                List<YoloBox> remaining = group.OrderByDescending(b => b.Score).ToList();

                while (remaining.Count > 0)
                {
                    YoloBox best = remaining[0];
                    ret.Add(best);
                    remaining.RemoveAt(0);
                    remaining.RemoveAll(b => IoU(best, b) > iouThreshold);
                }
            }

            return ret;
        }
    }
}
