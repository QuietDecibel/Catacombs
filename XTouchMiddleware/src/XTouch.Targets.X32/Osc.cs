using System.Buffers.Binary;
using System.Text;

namespace XTouch.Targets.X32;

/// <summary>Minimal OSC 1.0 message codec (types i, f, s, b). Bundles are not used by the X32.</summary>
public static class Osc
{
    public static byte[] Message(string address, params object[] args)
    {
        using var ms = new MemoryStream(64);
        WriteString(ms, address);
        var tags = new StringBuilder(",");
        foreach (var a in args)
        {
            tags.Append(a switch
            {
                float or double => 'f',
                int => 'i',
                string => 's',
                byte[] => 'b',
                _ => throw new ArgumentException($"Unsupported OSC argument type {a.GetType().Name}"),
            });
        }
        WriteString(ms, tags.ToString());
        Span<byte> buf = stackalloc byte[4];
        foreach (var a in args)
        {
            switch (a)
            {
                case float f: BinaryPrimitives.WriteSingleBigEndian(buf, f); ms.Write(buf); break;
                case double d: BinaryPrimitives.WriteSingleBigEndian(buf, (float)d); ms.Write(buf); break;
                case int i: BinaryPrimitives.WriteInt32BigEndian(buf, i); ms.Write(buf); break;
                case string s: WriteString(ms, s); break;
                case byte[] b:
                    BinaryPrimitives.WriteInt32BigEndian(buf, b.Length); ms.Write(buf);
                    ms.Write(b);
                    ms.Write(new byte[(4 - b.Length % 4) % 4]);
                    break;
            }
        }
        return ms.ToArray();
    }

    /// <summary>Parses one OSC message. Returns false for bundles or malformed packets.</summary>
    public static bool TryParse(ReadOnlySpan<byte> p, out string address, out object[] args)
    {
        address = "";
        args = [];
        try
        {
            int pos = 0;
            address = ReadString(p, ref pos);
            if (!address.StartsWith('/')) return false;
            if (pos >= p.Length) return true; // no type tag string: no arguments
            string tags = ReadString(p, ref pos);
            if (!tags.StartsWith(',')) return false;
            var list = new List<object>(tags.Length - 1);
            foreach (char t in tags.AsSpan(1))
            {
                switch (t)
                {
                    case 'f': list.Add(BinaryPrimitives.ReadSingleBigEndian(p[pos..])); pos += 4; break;
                    case 'i': list.Add(BinaryPrimitives.ReadInt32BigEndian(p[pos..])); pos += 4; break;
                    case 's': list.Add(ReadString(p, ref pos)); break;
                    case 'b':
                        int len = BinaryPrimitives.ReadInt32BigEndian(p[pos..]); pos += 4;
                        list.Add(p.Slice(pos, len).ToArray());
                        pos += (len + 3) & ~3;
                        break;
                    default: args = [.. list]; return true; // unknown tag: keep what we have
                }
            }
            args = [.. list];
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    private static void WriteString(Stream s, string v)
    {
        var bytes = Encoding.ASCII.GetBytes(v);
        s.Write(bytes);
        s.Write(new byte[4 - bytes.Length % 4]); // null terminator + pad to a multiple of 4
    }

    private static string ReadString(ReadOnlySpan<byte> p, ref int pos)
    {
        int end = p[pos..].IndexOf((byte)0);
        if (end < 0) throw new ArgumentOutOfRangeException(nameof(p), "Unterminated OSC string");
        string s = Encoding.ASCII.GetString(p.Slice(pos, end));
        pos += (end + 4) & ~3;
        return s;
    }
}
