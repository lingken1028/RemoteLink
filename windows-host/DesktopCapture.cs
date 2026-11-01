using System.Drawing;
using System.IO;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace RemoteLink.Host;

public static class DesktopCapture
{
    private static readonly object Gate = new();

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    // Prototype capture: primary desktop only. Secure desktop/UAC prompts are not captured.
    public static byte[] CaptureJpeg(int maxWidth = 1280, long quality = 55)
    {
        lock (Gate)
        {
            var sourceWidth = GetSystemMetrics(0);  // SM_CXSCREEN
            var sourceHeight = GetSystemMetrics(1); // SM_CYSCREEN
            if (sourceWidth <= 0 || sourceHeight <= 0)
                throw new InvalidOperationException("無法取得主要螢幕尺寸。");

            var scale = Math.Min(1.0, (double)maxWidth / sourceWidth);
            var width = Math.Max(1, (int)Math.Round(sourceWidth * scale));
            var height = Math.Max(1, (int)Math.Round(sourceHeight * scale));

            using var source = new Bitmap(sourceWidth, sourceHeight, PixelFormat.Format24bppRgb);
            using (var graphics = Graphics.FromImage(source))
                graphics.CopyFromScreen(0, 0, 0, 0, source.Size, CopyPixelOperation.SourceCopy);

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
