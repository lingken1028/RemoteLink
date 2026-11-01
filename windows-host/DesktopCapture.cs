using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace RemoteLink.Host;

public static class DesktopCapture
{
    private static readonly object Gate = new();

    // Prototype capture: primary interactive desktop only. Windows secure desktop/UAC prompts
    // are intentionally not captured. This is not a replacement for a production WGC pipeline.
    public static byte[] CaptureJpeg(int maxWidth = 1280, long quality = 55)
    {
        lock (Gate)
        {
            var bounds = System.Windows.Forms.Screen.PrimaryScreen?.Bounds
                ?? throw new InvalidOperationException("找不到主要顯示器。");
            var sourceWidth = bounds.Width;
            var sourceHeight = bounds.Height;
            if (sourceWidth <= 0 || sourceHeight <= 0)
                throw new InvalidOperationException("主要顯示器尺寸無效。");

            var scale = Math.Min(1.0, (double)maxWidth / sourceWidth);
            var width = Math.Max(1, (int)Math.Round(sourceWidth * scale));
            var height = Math.Max(1, (int)Math.Round(sourceHeight * scale));

            using var source = new Bitmap(sourceWidth, sourceHeight, PixelFormat.Format24bppRgb);
            using (var graphics = Graphics.FromImage(source))
                graphics.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, source.Size, CopyPixelOperation.SourceCopy);

            using var output = new Bitmap(width, height, PixelFormat.Format24bppRgb);
            using (var graphics = Graphics.FromImage(output))
            {
                graphics.CompositingQuality = CompositingQuality.HighSpeed;
                graphics.InterpolationMode = InterpolationMode.Bilinear;
                graphics.SmoothingMode = SmoothingMode.HighSpeed;
                graphics.DrawImage(source, 0, 0, width, height);
            }

            using var stream = new MemoryStream();
            var encoder = ImageCodecInfo.GetImageEncoders().First(x => x.MimeType == "image/jpeg");
            using var parameters = new EncoderParameters(1);
            parameters.Param[0] = new EncoderParameter(Encoder.Quality, Math.Clamp(quality, 20, 85));
            output.Save(stream, encoder, parameters);
            return stream.ToArray();
        }
    }
}
