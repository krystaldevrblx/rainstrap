using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;

namespace Bloxstrap.Integrations.Clips
{
    public static class AviWriter
    {
        public static void WriteFramesToAvi(string outputPath, List<ReplayBuffer.FrameData> frames, int fps)
        {
            if (frames.Count == 0)
                return;

            using var firstStream = new MemoryStream(frames[0].Data);
            using var bitmap = Image.FromStream(firstStream) as Bitmap;
            if (bitmap is null)
                return;

            int width = bitmap.Width;
            int height = bitmap.Height;

            using var stream = File.Create(outputPath);
            using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);

            long riffStart = stream.Position;
            WriteFourCC(writer, "RIFF");
            writer.Write(0);
            WriteFourCC(writer, "AVI ");

            WriteMainAviHeader(writer, frames.Count, fps, width, height);
            WriteStreamHeader(writer, width, height);
            WriteMoviChunk(writer, frames, fps);
            WriteIdx1Chunk(writer, frames.Count);

            long endPos = stream.Position;
            stream.Seek(riffStart + 4, SeekOrigin.Begin);
            writer.Write((int)(endPos - riffStart - 8));
        }

        private static void WriteFourCC(BinaryWriter writer, string fourCC)
        {
            writer.Write(Encoding.ASCII.GetBytes(fourCC));
        }

        private static void WriteMainAviHeader(BinaryWriter writer, int frameCount, int fps, int width, int height)
        {
            long startPos = writer.BaseStream.Position;
            WriteFourCC(writer, "hdrs");
            writer.Write(0);

            writer.Write(56);
            writer.Write(width);
            writer.Write(height);
            writer.Write((short)1);
            writer.Write((short)24);
            writer.Write(0);
            writer.Write(0);
            writer.Write(0);
            writer.Write(0);
            writer.Write(fps);
            writer.Write(0);

            long endPos = writer.BaseStream.Position;
            writer.BaseStream.Seek(startPos + 4, SeekOrigin.Begin);
            writer.Write((int)(endPos - startPos - 8));
            writer.BaseStream.Seek(endPos, SeekOrigin.Begin);
        }

        private static void WriteStreamHeader(BinaryWriter writer, int width, int height)
        {
            long listStart = writer.BaseStream.Position;
            WriteFourCC(writer, "LIST");
            writer.Write(0);
            WriteFourCC(writer, "strl");

            long strhStart = writer.BaseStream.Position;
            WriteFourCC(writer, "strh");
            writer.Write(0);

            WriteFourCC(writer, "vids");
            writer.Write(0);
            writer.Write(0);
            writer.Write(1);
            writer.Write(0);
            writer.Write(0);
            writer.Write(1);
            writer.Write(0);
            writer.Write(0);
            writer.Write(0);
            writer.Write(0);

            long strhEnd = writer.BaseStream.Position;
            writer.BaseStream.Seek(strhStart + 4, SeekOrigin.Begin);
            writer.Write((int)(strhEnd - strhStart - 8));
            writer.BaseStream.Seek(strhEnd, SeekOrigin.Begin);

            long strfStart = writer.BaseStream.Position;
            WriteFourCC(writer, "strf");
            writer.Write(0);

            writer.Write(40);
            writer.Write(40);
            writer.Write(0);
            writer.Write(0);
            writer.Write(1);
            writer.Write(24);
            writer.Write(0);
            writer.Write(0);
            writer.Write(0);
            writer.Write(0);
            writer.Write(0);
            writer.Write(0);

            long strfEnd = writer.BaseStream.Position;
            writer.BaseStream.Seek(strfStart + 4, SeekOrigin.Begin);
            writer.Write((int)(strfEnd - strfStart - 8));
            writer.BaseStream.Seek(strfEnd, SeekOrigin.Begin);

            long listEnd = writer.BaseStream.Position;
            writer.BaseStream.Seek(listStart + 4, SeekOrigin.Begin);
            writer.Write((int)(listEnd - listStart - 8));
            writer.BaseStream.Seek(listEnd, SeekOrigin.Begin);
        }

        private static void WriteMoviChunk(BinaryWriter writer, List<ReplayBuffer.FrameData> frames, int fps)
        {
            long moviStart = writer.BaseStream.Position;
            WriteFourCC(writer, "LIST");
            writer.Write(0);
            WriteFourCC(writer, "movi");

            foreach (var frame in frames)
            {
                using var frameStream = new MemoryStream(frame.Data);
                using var frameBitmap = Image.FromStream(frameStream) as Bitmap;
                if (frameBitmap is null)
                    continue;

                byte[] jpegBytes = EncodeToJpeg(frameBitmap, 85);

                WriteFourCC(writer, "00dc");
                writer.Write(jpegBytes.Length);
                writer.Write(jpegBytes);

                if (jpegBytes.Length % 2 != 0)
                    writer.Write((byte)0);
            }

            long moviEnd = writer.BaseStream.Position;
            writer.BaseStream.Seek(moviStart + 4, SeekOrigin.Begin);
            writer.Write((int)(moviEnd - moviStart - 8));
            writer.BaseStream.Seek(moviEnd, SeekOrigin.Begin);
        }

        private static void WriteIdx1Chunk(BinaryWriter writer, int frameCount)
        {
            WriteFourCC(writer, "idx1");
            writer.Write(frameCount * 16);

            for (int i = 0; i < frameCount; i++)
            {
                WriteFourCC(writer, "00dc");
                writer.Write(16);
                writer.Write(i * 16);
                writer.Write(0);
            }
        }

        private static byte[] EncodeToJpeg(Bitmap bitmap, int quality)
        {
            using var memoryStream = new MemoryStream();

            var jpegEncoder = ImageCodecInfo.GetImageEncoders()
                .FirstOrDefault(e => e.FormatID == ImageFormat.Jpeg.Guid);

            if (jpegEncoder is null)
            {
                bitmap.Save(memoryStream, ImageFormat.Png);
                return memoryStream.ToArray();
            }

            var encoderParameters = new EncoderParameters(1);
            encoderParameters.Param[0] = new EncoderParameter(
                System.Drawing.Imaging.Encoder.Quality, (long)quality);

            bitmap.Save(memoryStream, jpegEncoder, encoderParameters);
            return memoryStream.ToArray();
        }
    }
}
