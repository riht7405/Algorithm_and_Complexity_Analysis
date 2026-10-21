using FractalPlatform.Infrastructure.Binary;
using Xunit;

namespace FractalPlatform.Tests;

public class BinaryPackerTests
{
    [Fact]
    public void PackUnpack_Roundtrip_PreservesData()
    {
        ushort[] original = [0, 1, 255, 256, 65535, 42];
        var bytes = BinaryPacker.PackUInt16(original);
        var restored = BinaryPacker.UnpackUInt16(bytes);

        Assert.Equal(original, restored);
        Assert.Equal(original.Length * 2, bytes.Length);
    }

    [Fact]
    public void Pack_LittleEndian()
    {
        // 0x0102 должно стать [0x02, 0x01] в little-endian
        var bytes = BinaryPacker.PackUInt16([0x0102]);
        Assert.Equal(0x02, bytes[0]);
        Assert.Equal(0x01, bytes[1]);
    }

    [Fact]
    public void Unpack_OddLength_Throws()
    {
        Assert.Throws<ArgumentException>(() => BinaryPacker.UnpackUInt16(new byte[3]));
    }
}