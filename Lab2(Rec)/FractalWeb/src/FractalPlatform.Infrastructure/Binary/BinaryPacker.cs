using System.Buffers.Binary;

namespace FractalPlatform.Infrastructure.Binary;

public static class BinaryPacker
{
    /// <summary>ushort[] → byte[] (little-endian). Используем BlockCopy там, где это возможно.</summary>
    public static byte[] PackUInt16(ushort[] values)
    {
        var bytes = new byte[values.Length * sizeof(ushort)];
        if (BitConverter.IsLittleEndian)
        {
            Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
        }
        else
        {
            for (int i = 0; i < values.Length; i++)
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(i * 2), values[i]);
        }
        return bytes;
    }

    public static ushort[] UnpackUInt16(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length % 2 != 0)
            throw new ArgumentException("Byte length must be even.", nameof(bytes));

        var result = new ushort[bytes.Length / 2];
        if (BitConverter.IsLittleEndian)
        {
            Buffer.BlockCopy(bytes.ToArray(), 0, result, 0, bytes.Length);
        }
        else
        {
            for (int i = 0; i < result.Length; i++)
                result[i] = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(i * 2));
        }
        return result;
    }
}