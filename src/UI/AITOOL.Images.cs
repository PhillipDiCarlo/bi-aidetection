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

        public static bool DrawAnnotation(Graphics g, ClsPrediction pred, double ImgWidth, double ImgHeight, double BoxWidth = 0, double BoxHeight = 0)
        {
            bool ret = false;

            try
            {

                if (BoxWidth == 0)
                    BoxWidth = ImgWidth;
                if (BoxHeight == 0)
                    BoxHeight = ImgHeight;

                string AnnoText = pred.ToString();

                bool Merge = false;

                if (AppSettings.Settings.HistoryOnlyDisplayRelevantObjects && pred.Result == ResultType.Relevant)
                    Merge = true;
                else if (!AppSettings.Settings.HistoryOnlyDisplayRelevantObjects)
                    Merge = true;

                if (Merge)
                {

                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.SmoothingMode = SmoothingMode.HighQuality;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    //http://csharphelper.com/blog/2014/09/understand-font-aliasing-issues-in-c/
                    g.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;

                    System.Drawing.Color color = new System.Drawing.Color();

                    double BorderWidth = AppSettings.Settings.RectBorderWidth;

                    if (pred.Result == ResultType.Relevant)
                    {
                        color = System.Drawing.Color.FromArgb(AppSettings.Settings.RectRelevantColorAlpha, AppSettings.Settings.RectRelevantColor);
                    }
                    else if (pred.Result == ResultType.DynamicMasked || pred.Result == ResultType.ImageMasked || pred.Result == ResultType.StaticMasked)
                    {
                        color = System.Drawing.Color.FromArgb(AppSettings.Settings.RectMaskedColorAlpha, AppSettings.Settings.RectMaskedColor);
                    }
                    else
                    {
                        color = System.Drawing.Color.FromArgb(AppSettings.Settings.RectIrrelevantColorAlpha, AppSettings.Settings.RectIrrelevantColor);
                    }

                    //Assume no scaling at first:
                    ///===================================================================================

                    System.Drawing.RectangleF rect = pred.GetRectangleF();

                    double xmin = pred.XMin;
                    double ymin = pred.YMin;
                    double xmax = pred.XMax;
                    double ymax = pred.YMax;

                    double sclxmin = pred.XMin;
                    double sclymin = pred.YMin;
                    double sclxmax = pred.XMax;
                    double sclymax = pred.YMax;

                    double TextSizePoints = AppSettings.Settings.RectDetectionTextSize;

                    ///===================================================================================
                    //check to see if we need to scale based on onscreen zoomed image from picturebox:
                    ///===================================================================================
                    if (ImgWidth != BoxWidth || ImgHeight != BoxHeight)
                    {
                        //these variables store the padding between image border and picturebox border
                        double absX = 0;
                        double absY = 0;

                        //because the sizemode of the picturebox is set to 'zoom', the image is scaled down
                        double scale = 1;

                        //Comparing the aspect ratio of both the control and the image itself.
                        if (ImgWidth / ImgHeight > BoxWidth / BoxHeight) //if the image is p.e. 16:9 and the picturebox is 4:3
                        {
                            scale = BoxWidth / ImgWidth; //get scale factor
                            absY = (BoxHeight - scale * ImgHeight) / 2; //padding on top and below the image
                        }
                        else //if the image is p.e. 4:3 and the picturebox is widescreen 16:9
                        {
                            scale = BoxHeight / ImgHeight; //get scale factor
                            absX = (BoxWidth - scale * ImgWidth) / 2; //padding left and right of the image
                        }

                        //2. inputted position values are for the original image size. As the image is probably smaller in the picturebox, the positions must be adapted. 
                        xmin = (scale * xmin) + absX;
                        xmax = (scale * xmax) + absX;
                        ymin = (scale * ymin) + absY;
                        ymax = (scale * ymax) + absY;

                        double sclWidth = xmax - xmin;
                        double sclHeight = ymax - ymin;

                        sclxmax = BoxWidth - (absX * 2);
                        sclymax = BoxHeight - (absY * 2);
                        sclxmin = absX;
                        sclymin = absY;

                        TextSizePoints = scale * TextSizePoints;

                        BorderWidth = scale * BorderWidth;

                        if (BorderWidth < 1)
                            BorderWidth = 1;

                        rect = new System.Drawing.RectangleF(xmin.ToFloat(),
                                             ymin.ToFloat(),
                                             sclWidth.ToFloat(),
                                             sclHeight.ToFloat());
                    }

                    ///===================================================================================

                    using (Pen pen = new Pen(color, BorderWidth.ToFloat()))
                    {
                        g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height); //draw rectangle
                    }

                    //we need this since people can change the border width in the json file
                    double halfbrd = BorderWidth / 2;

                    System.Drawing.SizeF TextSize = g.MeasureString(AnnoText, new Font(AppSettings.Settings.RectDetectionTextFont, TextSizePoints.ToFloat())); //finds size of text to draw the background rectangle

                    double x = xmin - halfbrd;
                    double y = ymax + halfbrd;

                    //adjust the x / width label so it doesnt go off screen
                    double EndX = x + TextSize.Width;
                    if (EndX > sclxmax)
                    {
                        //int diffx = x - sclxmax;
                        x = xmax - TextSize.Width + halfbrd;
                    }

                    if (x < sclxmin)
                        x = sclxmin;

                    if (x < 0)
                        x = 0;

                    //adjust the y / height label so it doesnt go off screen
                    double EndY = y + TextSize.Height;
                    if (EndY > sclymax)
                    {
                        //float diffy = EndY - sclymax;
                        y = ymax - TextSize.Height - halfbrd;
                    }


                    if (y < 0)
                        y = 0;

                    //object name text below rectangle
                    rect = new System.Drawing.RectangleF(x.ToFloat(),
                                                         y.ToFloat(),
                                                         BoxWidth.ToFloat(),
                                                         BoxHeight.ToFloat()); //sets bounding box for drawn text

                    Brush brush = new SolidBrush(color); //sets background rectangle color
                    if (AppSettings.Settings.RectDetectionTextBackColor != System.Drawing.Color.Gainsboro)
                    {
                        color = System.Drawing.Color.FromArgb(color.A, AppSettings.Settings.RectDetectionTextBackColor);
                        brush = new SolidBrush(color);
                    }

                    Brush forecolor = Brushes.Black;
                    if (AppSettings.Settings.RectDetectionTextForeColor != System.Drawing.Color.Gainsboro)
                        forecolor = new SolidBrush(AppSettings.Settings.RectDetectionTextForeColor);


                    g.FillRectangle(brush,
                                    x.ToFloat(),
                                    y.ToFloat(),
                                    TextSize.Width,
                                    TextSize.Height); //draw grey background rectangle for detection text

                    g.DrawString(AnnoText,
                                 new Font(AppSettings.Settings.RectDetectionTextFont, TextSizePoints.ToFloat()),
                                 forecolor,
                                 rect); //draw detection text

                    g.Flush(FlushIntention.Flush);

                    ret = true;

                }

            }
            catch (Exception ex)
            {

                Log($"Error: {ex.Msg()}");
            }


            return ret;

        }

        public static System.Drawing.Image CropImage(ClsImageQueueItem img, System.Drawing.Rectangle cropArea)
        {

            if (img.IsValid())
            {
                try
                {
                    DecoderOptions dc = new DecoderOptions();
                    using (SixLabors.ImageSharp.Image image = SixLabors.ImageSharp.Image.Load(img.ToMemStream()))  //, out IImageFormat format))
                    {
                        image.Mutate(i => i.Crop(SixLabors.ImageSharp.Rectangle.FromLTRB(cropArea.Left, cropArea.Top, cropArea.Right, cropArea.Bottom)));

                        using (MemoryStream ms = new MemoryStream())
                        {
                            image.Save(ms, image.Metadata.DecodedImageFormat);
                            System.Drawing.Image newimg = System.Drawing.Image.FromStream(ms);
                            return newimg;
                        }
                    }

                }
                catch (Exception ex)
                {

                    Log($"Error: {ex.Msg()}");
                }

            }

            return null;
            //using Bitmap bmpImage = new Bitmap(img);

            //if (cropArea.Right > bmpImage.Width || cropArea.Bottom > bmpImage.Height)
            //{
            //    cropArea.Intersect(new Rectangle(0, 0, bmpImage.Width, bmpImage.Height));
            //}

            //Bitmap bmpCrop = bmpImage.Clone(cropArea, bmpImage.PixelFormat);

            //return (Image)(bmpCrop);

        }

        public static ClsImageAdjust GetImageAdjustProfileByName(string name, bool ReturnDefault)
        {
            ClsImageAdjust ret = null;
            ClsImageAdjust def = null;

            foreach (ClsImageAdjust ia in AppSettings.Settings.ImageAdjustProfiles)
            {
                if (string.Equals(name.Trim(), ia.Name.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    ret = ia;
                }
                if (string.Equals("Default", ia.Name.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    def = ia;
                }
            }

            if (ret == null && ReturnDefault)
                ret = def;

            if (ret == null)
            {
                //ret = new ClsImageAdjust("Default");
                Log($"Error: Could not find Image Adjust profile that matches '{name}'");
            }

            return ret;
        }

        public static bool HasImageAdjustProfile(string name)
        {
            bool ret = false;

            foreach (ClsImageAdjust ia in AppSettings.Settings.ImageAdjustProfiles)
            {
                if (string.Equals(name.Trim(), ia.Name.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return ret;
        }

        public static async Task<System.Drawing.Image> ApplyImageAdjustProfileAsync(ClsImageAdjust IAProfile, string InputImageFile, string OutputImageFile)
        {
            using var Trace = new Trace();  //This c# 8.0 using feature will auto dispose when the function is done.

            System.Drawing.Image retimg = null;
            SixLabors.ImageSharp.Image ISImage = null;
            MemoryStream IStream = new System.IO.MemoryStream();
            try
            {
                if (!string.IsNullOrEmpty(InputImageFile) && File.Exists(InputImageFile))
                {
                    bool SaveToFile = (!string.IsNullOrEmpty(OutputImageFile));

                    //SixLabors.ImageSharp.Configuration config = new Configuration();

                    ISImage = await SixLabors.ImageSharp.Image.LoadAsync(InputImageFile);


                    if (IAProfile.ImageWidth != -1 && IAProfile.ImageHeight != -1 && ISImage.Width != IAProfile.ImageWidth || ISImage.Height != IAProfile.ImageHeight)  //hard coded size
                    {
                        Log($"Resizing image from {ISImage.Width},{ISImage.Height} to {IAProfile.ImageWidth},{IAProfile.ImageHeight}...");
                        ISImage.Mutate(i => i.Resize(IAProfile.ImageWidth, IAProfile.ImageHeight));
                    }
                    else if (IAProfile.ImageSizePercent > 0 && IAProfile.ImageSizePercent < 100)
                    {
                        double fractionalPercentage = (IAProfile.ImageSizePercent / 100.0);
                        double outputWidth = ISImage.Width * fractionalPercentage;
                        double outputHeight = ISImage.Height * fractionalPercentage;

                        Log($"Resizing image to {IAProfile.ImageSizePercent} from {ISImage.Width},{ISImage.Height} to {outputWidth},{outputHeight}...");
                        ISImage.Mutate(i => i.Resize(outputWidth.ToInt(), outputHeight.ToInt()));
                    }

                    if (IAProfile.Brightness > 1 && IAProfile.Brightness < 100)
                    {
                        //A value of 0 will create an image that is completely black. A value of 1 leaves the input unchanged. 
                        //Other values are linear multipliers on the effect. Values of an amount over 1 are allowed, providing brighter results
                        //amount - The proportion of the conversion.Must be greater than or equal to 0.
                        Log($"Changing brightness by amount {IAProfile.Brightness}...");
                        ISImage.Mutate(i => i.Brightness(IAProfile.Brightness));
                    }

                    if (IAProfile.Contrast > 1 && IAProfile.Contrast < 100)
                    {
                        //A value of 0 will create an image that is completely gray. A value of 1 leaves the input unchanged. 
                        //Other values are linear multipliers on the effect. Values of an amount over 1 are allowed, providing results with more contrast.
                        //amount - The proportion of the conversion. Must be greater than or equal to 0.
                        Log($"Changing contrast by amount {IAProfile.Contrast}...");
                        ISImage.Mutate(i => i.Contrast(IAProfile.Contrast));
                    }


                    //string tfile = Path.Combine(Environment.GetEnvironmentVariable("TEMP"), "_AITOOL\tmpimage.jpg");

                    //Save the image using the specified jpeg compression
                    Log($"Compressing jpeg to {IAProfile.JPEGQualityPercent}% quality...");
                    SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder encoder = new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder();
                    //encoder.Quality = IAProfile.JPEGQualityPercent;

                    // lets switch out the default encoder for jpeg to one
                    // that saves at 90 quality
                    Configuration.Default.ImageFormatsManager.SetEncoder(JpegFormat.Instance, new JpegEncoder()
                    {
                        Quality = IAProfile.JPEGQualityPercent
                    });

                    if (SaveToFile)
                    {
                        //save to file
                        await ISImage.SaveAsJpegAsync(OutputImageFile, encoder);

                    }
                    else  //assume we just need the image for viewing and send back an image
                    {
                        //save to stream
                        await ISImage.SaveAsJpegAsync(IStream, encoder);

                        //read back from stream
                        //ISImage = await SixLabors.ImageSharp.Image.LoadAsync(IStream);

                        retimg = System.Drawing.Image.FromStream(IStream);
                    }


                }
                else
                {
                    Log("File does not exist: " + InputImageFile);
                }

            }
            catch (Exception ex)
            {

                Log("Error: " + ex.Msg());
            }
            finally
            {
                if (IStream != null)
                    IStream.Dispose();

                if (ISImage != null)
                    ISImage.Dispose();

            }

            return retimg;

        }
    }
}
