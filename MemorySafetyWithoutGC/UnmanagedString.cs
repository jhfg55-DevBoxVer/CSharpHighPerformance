using System;
using System.Runtime.InteropServices;

public unsafe struct UnmanagedString
{
    public byte* Ptr;    // 非托管内存中存储 UTF-8 数据的起始地址
    public int Length;   // 当前字符串的字节长度
    public int Capacity; // 当前已分配的总字节数

    public static UnmanagedString New() => new UnmanagedString { Ptr = null, Length = 0, Capacity = 0 };

    public UnmanagedString(string s)
    {
        if (s == null)
            throw new ArgumentNullException(nameof(s));

        // 使用托管字符串仅作为初始输入，之后均在非托管内存上操作
        int byteCount = Utf8ByteCount(s);
        Length = byteCount;
        Capacity = byteCount;
        Ptr = (byte*)Marshal.AllocHGlobal(byteCount);
        fixed (char* p = s)
        {
            int offset = 0;
            for (int i = 0; i < s.Length; i++)
            {
                offset += EncodeUtf8Char(p[i], Ptr + offset);
            }
        }
    }

    public static UnmanagedString WithCapacity(int capacity)
    {
        if (capacity < 0)
            throw new ArgumentOutOfRangeException(nameof(capacity));

        UnmanagedString us;
        us.Capacity = capacity;
        us.Length = 0;
        us.Ptr = capacity > 0 ? (byte*)Marshal.AllocHGlobal(capacity) : null;
        return us;
    }

    public void Free()
    {
        if (Ptr != null)
        {
            Marshal.FreeHGlobal((IntPtr)Ptr);
            Ptr = null;
            Length = 0;
            Capacity = 0;
        }
    }

    private void EnsureCapacity(int required)
    {
        if (required > Capacity)
        {
            int newCapacity = (Capacity == 0) ? 8 : Capacity * 2;
            if (newCapacity < required)
                newCapacity = required;
            byte* newPtr = (byte*)Marshal.AllocHGlobal(newCapacity);
            for (int i = 0; i < Length; i++)
                newPtr[i] = Ptr[i];
            if (Ptr != null)
                Marshal.FreeHGlobal((IntPtr)Ptr);
            Ptr = newPtr;
            Capacity = newCapacity;
        }
    }

    /// <summary>
    /// 判断给定的字节索引是否为 UTF-8 字符的起始边界。
    /// </summary>
    public bool IsCharBoundary(int index)
    {
        if (index == 0 || index == Length)
            return true;
        // UTF-8连续字节以 10xxxxxx 开头
        return (Ptr[index] & 0xC0) != 0x80;
    }

    /// <summary>
    /// 计算 UTF-8 编码下，一个托管字符串所需的字节数（手动实现，参照 RFC 3629）。
    /// </summary>
    private static int Utf8ByteCount(string s)
    {
        int count = 0;
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c < 0x80)
                count += 1;
            else if (c < 0x800)
                count += 2;
            else
                count += 3; // 简化处理，忽略代理对（不支持 BMP 之外）
        }
        return count;
    }

    /// <summary>
    /// 将单个 Unicode 字符（C# char）编码为 UTF-8，写入 dest 返回写入字节数。
    /// 对于代理对或超出 BMP 的字符，为简单起见，此处不支持，直接按 3 字节编码。
    /// </summary>
    private static int EncodeUtf8Char(char c, byte* dest)
    {
        if (c < 0x80)
        {
            dest[0] = (byte)c;
            return 1;
        }
        else if (c < 0x800)
        {
            dest[0] = (byte)(0xC0 | (c >> 6));
            dest[1] = (byte)(0x80 | (c & 0x3F));
            return 2;
        }
        else
        {
            dest[0] = (byte)(0xE0 | (c >> 12));
            dest[1] = (byte)(0x80 | ((c >> 6) & 0x3F));
            dest[2] = (byte)(0x80 | (c & 0x3F));
            return 3;
        }
    }

    /// <summary>
    /// 解码一个 UTF-8 字符，最多读取 4 字节，返回读取字节数；codepoint 存储解码结果。
    /// 不支持非法编码（返回 0 表示错误）。
    /// </summary>
    private static int DecodeUtf8Char(byte* ptr, int remaining, out int codepoint)
    {
        if (remaining == 0)
        {
            codepoint = 0;
            return 0;
        }
        byte b0 = ptr[0];
        if (b0 < 0x80)
        {
            codepoint = b0;
            return 1;
        }
        else if ((b0 & 0xE0) == 0xC0)
        {
            if (remaining < 2) { codepoint = 0; return 0; }
            byte b1 = ptr[1];
            if ((b1 & 0xC0) != 0x80) { codepoint = 0; return 0; }
            codepoint = ((b0 & 0x1F) << 6) | (b1 & 0x3F);
            return 2;
        }
        else if ((b0 & 0xF0) == 0xE0)
        {
            if (remaining < 3) { codepoint = 0; return 0; }
            byte b1 = ptr[1], b2 = ptr[2];
            if ((b1 & 0xC0) != 0x80 || (b2 & 0xC0) != 0x80) { codepoint = 0; return 0; }
            codepoint = ((b0 & 0x0F) << 12) |
                        ((b1 & 0x3F) << 6) |
                        (b2 & 0x3F);
            return 3;
        }
        else if ((b0 & 0xF8) == 0xF0)
        {
            if (remaining < 4) { codepoint = 0; return 0; }
            byte b1 = ptr[1], b2 = ptr[2], b3 = ptr[3];
            if ((b1 & 0xC0) != 0x80 || (b2 & 0xC0) != 0x80 || (b3 & 0xC0) != 0x80)
            { codepoint = 0; return 0; }
            codepoint = ((b0 & 0x07) << 18) |
                        ((b1 & 0x3F) << 12) |
                        ((b2 & 0x3F) << 6) |
                        (b3 & 0x3F);
            return 4;
        }
        else
        {
            codepoint = 0;
            return 0;
        }
    }

    /// <summary>
    /// 遍历整个 UnmanagedString 中所有字符（不进行托管转换）。
    /// </summary>
    public System.Collections.Generic.IEnumerable<int> Chars()
    {
        int pos = 0;
        while (pos < Length)
        {
            int cp;
            int size = DecodeUtf8Char(Ptr + pos, Length - pos, out cp);
            if (size == 0)
                throw new InvalidOperationException("Invalid UTF-8 sequence");
            yield return cp;
            pos += size;
        }
    }

    /// <summary>
    /// 返回逻辑字符数（即解码后的 Unicode 标量数量）。
    /// </summary>
    public int CharCount()
    {
        int count = 0;
        int pos = 0;
        while (pos < Length)
        {
            int cp;
            int size = DecodeUtf8Char(Ptr + pos, Length - pos, out cp);
            if (size == 0)
                throw new InvalidOperationException("Invalid UTF-8 sequence");
            count++;
            pos += size;
        }
        return count;
    }

    /// <summary>
    /// 根据逻辑字符索引删除一个字符，并返回其 Unicode 码点。
    /// 完全在非托管内存中操作，不通过托管字符串转换。
    /// </summary>
    public int RemoveAt(int charIndex)
    {
        if (charIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(charIndex));
        int pos = 0, current = 0;
        // 定位要删除字符的开始字节位置
        while (pos < Length && current < charIndex)
        {
            int cp;
            int size = DecodeUtf8Char(Ptr + pos, Length - pos, out cp);
            if (size == 0)
                throw new InvalidOperationException("Invalid UTF-8 sequence");
            pos += size;
            current++;
        }
        if (pos >= Length)
            throw new ArgumentOutOfRangeException(nameof(charIndex));

        int deleteStart = pos;
        int deleteSize = DecodeUtf8Char(Ptr + pos, Length - pos, out int removedCp);
        if (deleteSize == 0)
            throw new InvalidOperationException("Invalid UTF-8 sequence");

        // 挪动剩余数据
        int remaining = Length - (deleteStart + deleteSize);
        Buffer.MemoryCopy(Ptr + deleteStart + deleteSize, Ptr + deleteStart, Capacity - deleteStart, remaining);
        Length -= deleteSize;
        return removedCp;
    }

    /// <summary>
    /// 根据逻辑字符索引插入单个字符（手动 UTF-8 编码）。
    /// </summary>
    public void InsertAt(int charIndex, char c)
    {
        int insertPos = GetByteOffsetForCharIndex(charIndex);
        // 先计算此 char 编码为 UTF-8 后所占字节数
        byte tempBufferSpan = 0; // 占位
        byte* tmp = stackalloc byte[4];
        int encoded = EncodeUtf8Char(c, tmp);

        int newLength = Length + encoded;
        EnsureCapacity(newLength);
        // 后移数据
        Buffer.MemoryCopy(Ptr + insertPos, Ptr + insertPos + encoded, Capacity - (insertPos + encoded), Length - insertPos);
        // 写入新字符
        for (int i = 0; i < encoded; i++)
            Ptr[insertPos + i] = tmp[i];
        Length = newLength;
    }

    /// <summary>
    /// 根据逻辑字符索引插入字符串。手动对输入字符串进行 UTF-8 编码（逐字符编码）。
    /// </summary>
    public void InsertAt(int charIndex, string s)
    {
        if (s == null)
            throw new ArgumentNullException(nameof(s));
        int insertPos = GetByteOffsetForCharIndex(charIndex);
        int addBytes = Utf8ByteCount(s);
        int newLength = Length + addBytes;
        EnsureCapacity(newLength);
        Buffer.MemoryCopy(Ptr + insertPos, Ptr + insertPos + addBytes, Capacity - (insertPos + addBytes), Length - insertPos);
        // 插入新的 UTF-8 数据
        int offset = insertPos;
        fixed (char* ps = s)
        {
            for (int i = 0; i < s.Length; i++)
            {
                offset += EncodeUtf8Char(ps[i], Ptr + offset);
            }
        }
        Length = newLength;
    }

    /// <summary>
    /// 辅助方法：通过遍历计算逻辑字符索引对应的字节偏移量。
    /// 如果 charIndex 超出范围，则返回 Length。
    /// </summary>
    private int GetByteOffsetForCharIndex(int charIndex)
    {
        if (charIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(charIndex));
        int pos = 0, current = 0;
        while (pos < Length && current < charIndex)
        {
            int cp;
            int size = DecodeUtf8Char(Ptr + pos, Length - pos, out cp);
            if (size == 0)
                throw new InvalidOperationException("Invalid UTF-8 sequence");
            pos += size;
            current++;
        }
        return pos;
    }

    /// 将当前 UnmanagedString 以 UTF-8 编码转换为托管字符串。
    /// 本方法仅用于调试或与托管交互时使用，不参与高级操作的内部实现。
    public override string ToString()
    {
        if (Length == 0)
            return string.Empty;
        // 注意：此处调用构造函数将托管转换，仅作展示用途
        byte[] data = new byte[Length];
        for (int i = 0; i < Length; i++)
            data[i] = Ptr[i];
        return System.Text.Encoding.UTF8.GetString(data);
    }
}
