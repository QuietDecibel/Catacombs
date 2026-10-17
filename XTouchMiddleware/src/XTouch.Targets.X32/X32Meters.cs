using System.Buffers.Binary;

namespace XTouch.Targets.X32;

/// <summary>X32 meter blobs: little-endian int32 count followed by that many little-endian float32 levels (linear, 1.0 = 0 dBFS).</summary>
public static class X32Meters
{
    public static float[] Decode(byte[] blob)
    {
        if (blob.Length < 4) return [];
        int count = BinaryPrimitives.ReadInt32LittleEndian(blob);
        count = Math.Clamp(count, 0, (blob.Length - 4) / 4);
        var levels = new float[count];
        for (int i = 0; i < count; i++) levels[i] = BinaryPrimitives.ReadSingleLittleEndian(blob.AsSpan(4 + i * 4));
        return levels;
    }

    public static byte[] Encode(ReadOnlySpan<float> levels)
    {
        var blob = new byte[4 + levels.Length * 4];
        BinaryPrimitives.WriteInt32LittleEndian(blob, levels.Length);
        for (int i = 0; i < levels.Length; i++) BinaryPrimitives.WriteSingleLittleEndian(blob.AsSpan(4 + i * 4), levels[i]);
        return blob;
    }
}
