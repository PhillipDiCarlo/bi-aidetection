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


        public static string GetMaskFile(string cameraname)
        {
            Camera cam = GetCamera(cameraname, false);
            if (cam != null)
                return cam.GetMaskFile(true);
            else
                return "";
        }



        public static MaskResultInfo Outsidemask(Camera cam, double xmin, double xmax, double ymin, double ymax, int width, int height)
        {
            using var Trace = new Trace();  //This c# 8.0 using feature will auto dispose when the function is done.

            //Log($"      Checking if object is outside privacy mask of {cameraname}:");
            //Log("         Loading mask file...");
            MaskResultInfo ret = new MaskResultInfo();
            string fileType = "";
            string foundfile = "";
            try
            {

                foundfile = cam.GetMaskFile(true);

                if (!string.IsNullOrEmpty(foundfile) && System.IO.File.Exists(foundfile))
                {
                    Log($"Trace:     ->Using found mask file {foundfile}...");
                    fileType = Path.GetExtension(foundfile).ToLower();
                }
                else
                {
                    Log($"Trace:     ->Camera has no mask file yet");
                    ret.IsMasked = false;
                    ret.MaskType = MaskType.None;
                    ret.Result = MaskResult.NoMaskImageFile;
                    return ret;
                }

                //load mask file (in the image all places that have color (transparency > 9 [0-255 scale]) are masked)
                using (var mask_img = new Bitmap(foundfile))
                {
                    //if any coordinates of the object are outside of the mask image, th mask image must be too small.
                    if (mask_img.Width != width || mask_img.Height != height)
                    {
                        Log($"ERROR: The resolution of the mask '{foundfile}' does not equal the resolution of the processed image. Skipping privacy mask feature. Image: {width}x{height}, Mask: {mask_img.Width}x{mask_img.Height}");
                        ret.IsMasked = false;
                        ret.MaskType = MaskType.Image;
                        ret.Result = MaskResult.Error;
                        return ret;
                    }

                    //relative x and y locations of the 9 detection points
                    double[] x_factor = new double[] { 0.25, 0.5, 0.75, 0.25, 0.5, 0.75, 0.25, 0.5, 0.75 };
                    double[] y_factor = new double[] { 0.25, 0.25, 0.25, 0.5, 0.5, 0.5, 0.75, 0.75, 0.75 };

                    //int result = 0; //counts how many of the 9 points are outside of masked area(s)

                    //check the transparency of the mask image in all 9 detection points
                    for (int i = 0; i < 9; i++)
                    {
                        //get image point coordinates (and converting double to int)
                        double x = xmin + (xmax - xmin) * x_factor[i];
                        double y = ymin + (ymax - ymin) * y_factor[i];

                        // Get the color of the pixel
                        System.Drawing.Color pixelColor = mask_img.GetPixel(x.ToInt(), y.ToInt());

                        if (fileType == ".png")
                        {
                            //if the pixel is transparent (A refers to the alpha channel), the point is outside of masked area(s)
                            if (pixelColor.A < 10)
                            {
                                ret.ImagePointsOutsideMask++;
                            }
                        }
                        else
                        {
                            if (pixelColor.A == 0)  // object is in a transparent section of the image (not masked)
                            {
                                ret.ImagePointsOutsideMask++;
                            }
                        }

                    }

                    if (ret.ImagePointsOutsideMask > 4) //if 5 or more of the 9 detection points are outside of masked areas, the majority of the object is outside of masked area(s)
                    {
                        if (ret.ImagePointsOutsideMask == 9)
                        {
                            Log($"Trace:      ->ALL of the object is OUTSIDE of masked area(s). ({ret.ImagePointsOutsideMask} of 9 points)");
                            ret.IsMasked = false;
                            ret.MaskType = MaskType.Image;
                            ret.Result = MaskResult.MajorityOutsideMask;
                            return ret;
                        }
                        else
                        {
                            Log($"Trace:      ->Most of the object is OUTSIDE of masked area(s). ({ret.ImagePointsOutsideMask} of 9 points)");
                            ret.IsMasked = false;
                            ret.MaskType = MaskType.Image;
                            ret.Result = MaskResult.CompletlyOutsideMask;
                            return ret;
                        }
                    }

                    else //if 4 or less of 9 detection points are outside, then 5 or more points are in masked areas and the majority of the object is so too
                    {
                        if (ret.ImagePointsOutsideMask == 0)
                        {
                            Log($"Trace:      ->All of the object is INSIDE a masked area. ({ret.ImagePointsOutsideMask} of 9 points were outside)");
                            ret.IsMasked = true;
                            ret.MaskType = MaskType.Image;
                            ret.Result = MaskResult.CompletlyInsideMask;
                            return ret;

                        }
                        else
                        {
                            Log($"Trace:      ->Most of the object is INSIDE a masked area. ({ret.ImagePointsOutsideMask} of 9 points were outside)");
                            ret.IsMasked = true;
                            ret.MaskType = MaskType.Image;
                            ret.Result = MaskResult.MajorityInsideMask;
                            return ret;
                        }
                    }

                }

            }
            catch (Exception ex)
            {
                Log($"ERROR: While loading the mask file {foundfile}: {ex.Msg()}");
                ret.IsMasked = false;
                ret.MaskType = MaskType.Image;
                ret.Result = MaskResult.Error;
                return ret;
            }

        }
    }
}
