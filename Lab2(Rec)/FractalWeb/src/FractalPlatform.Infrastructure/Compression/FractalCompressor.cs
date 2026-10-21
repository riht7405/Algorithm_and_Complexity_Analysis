using System.IO.Compression;

namespace FractalPlatform.Infrastructure.Compression;

public static class FractalCompressor
{
    public static byte[] CompressBrotli(ReadOnlySpan<byte> data)
    {
        if (data.Length < 1024)
            return data.ToArray(); // сжатие мелких данных бессмысленно

        using var output = new MemoryStream();
        using (var brotli = new BrotliStream(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            brotli.Write(data);
        }
        return output.ToArray();
    }

    public static byte[] DecompressBrotli(ReadOnlySpan<byte> compressed)
    {
        using var input = new MemoryStream(compressed.ToArray());
        using var brotli = new BrotliStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        brotli.CopyTo(output);
        return output.ToArray();
    }
}