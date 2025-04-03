using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
namespace MoreUnmanagedTypes
{
    // 定义非托管字符谓词，返回 true 表示保留此字符
    public unsafe delegate bool UnmanagedCharPredicate(int codepoint, int charIndex);

    public unsafe struct UString : IComparable<UString>, IEquatable<UString>, ICloneable
    {
        public byte* Ptr;    // 非托管内存中存储 UTF-8 数据的起始地址
        public int Length;   // 当前字符串的字节长度
        public int Capacity; // 当前已分配的总字节数

        public static UString New() => new UString { Ptr = null, Length = 0, Capacity = 0 };

        public UString(string s)
        {
            if (s == null)
                throw new ArgumentNullException(nameof(s));

            int byteCount = Utf8ByteCount(s);
            Length = byteCount;
            Capacity = byteCount;
            Ptr = (byte*)Marshal.AllocHGlobal(byteCount);
            fixed (char* p = s)
            {
                int offset = 0;
                int i = 0;
                while (i < s.Length)
                {
                    char c = p[i];
                    int codepoint;
                    int encoded = 0;
                    // 检查是否为代理对。虽然 UString 最终存储的是 UTF-8 编码的数据，但 .NET 的字符串（System.String）本身是以 UTF-16 格式存储的，这意味着对于超过 BMP 的 Unicode 码点，会使用代理对（surrogate pairs）表示。在将 UTF-16 字符串转换为 UTF-8 时，必须正确处理这些代理对，以确保转换后生成的 UTF-8 数据是正确和完整的。如果不考虑代理对，可能会将高代理项和低代理项错误地分别当作单个字符处理，从而导致 UTF-8 编码数据不正确。

                    if (char.IsHighSurrogate(c) && i + 1 < s.Length && char.IsLowSurrogate(p[i + 1]))
                    {
                        codepoint = char.ConvertToUtf32(c, p[i + 1]);
                        encoded = EncodeUtf8Codepoint(codepoint, Ptr + offset);
                        i += 2;
                    }
                    else
                    {
                        codepoint = c;
                        encoded = EncodeUtf8Codepoint(codepoint, Ptr + offset);
                        i++;
                    }
                    offset += encoded;
                }
            }
        }

        public static UString WithCapacity(int capacity)
        {
            if (capacity < 0)
                throw new ArgumentOutOfRangeException(nameof(capacity));

            UString us;
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
        /// 为当前字符串预留额外的空间，使总容量达到至少 Length + additional。
        /// 如果当前容量不足，则采用增长策略扩容。
        /// </summary>
        public void Reserve(int additional)
        {
            int required = Length + additional;
            if (required > Capacity)
            {
                EnsureCapacity(required);
            }
        }

        /// <summary>
        /// 精确地为当前字符串预留额外的空间，使总容量恰好为 Length + additional。
        /// 不采用过度分配策略，而是直接分配所需大小的内存块。
        /// </summary>
        public void ReserveExact(int additional)
        {
            int required = Length + additional;
            if (required > Capacity)
            {
                byte* newPtr = (byte*)Marshal.AllocHGlobal(required);
                // 复制现有数据到新内存；这里 required 作为新缓冲区大小，
                // 只复制 Length 个字节即可
                Buffer.MemoryCopy(Ptr, newPtr, required, Length);
                if (Ptr != null)
                    Marshal.FreeHGlobal((IntPtr)Ptr);
                Ptr = newPtr;
                Capacity = required;
            }
        }

        /// <summary>
        /// 收缩当前字符串的容量至实际数据长度，释放多余的空间。
        /// 如果字符串为空，则释放内存并将 Ptr 置为 null。
        /// </summary>
        public void ShrinkToFit()
        {
            if (Length < Capacity)
            {
                if (Length == 0)
                {
                    if (Ptr != null)
                    {
                        Marshal.FreeHGlobal((IntPtr)Ptr);
                        Ptr = null;
                    }
                    Capacity = 0;
                }
                else
                {
                    byte* newPtr = (byte*)Marshal.AllocHGlobal(Length);
                    Buffer.MemoryCopy(Ptr, newPtr, Length, Length);
                    Marshal.FreeHGlobal((IntPtr)Ptr);
                    Ptr = newPtr;
                    Capacity = Length;
                }
            }
        }


        public bool IsCharBoundary(int index)
        {
            if (index == 0 || index == Length)
                return true;
            return (Ptr[index] & 0xC0) != 0x80;
        }

        // 更新后的计算 UTF-8 字节数（支持代理对）
        private static int Utf8ByteCount(string s)
        {
            int count = 0;
            int i = 0;
            while (i < s.Length)
            {
                char c = s[i];
                int codepoint;
                if (char.IsHighSurrogate(c) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1]))
                {
                    codepoint = char.ConvertToUtf32(c, s[i + 1]);
                    i += 2;
                }
                else
                {
                    codepoint = c;
                    i++;
                }
                if (codepoint < 0x80)
                    count += 1;
                else if (codepoint < 0x800)
                    count += 2;
                else if (codepoint < 0x10000)
                    count += 3;
                else
                    count += 4;
            }
            return count;
        }

        // 新增：根据 Unicode 码点编码为 UTF-8（支持代理对）
        private static int EncodeUtf8Codepoint(int codepoint, byte* dest)
        {
            if (codepoint < 0x80)
            {
                dest[0] = (byte)codepoint;
                return 1;
            }
            else if (codepoint < 0x800)
            {
                dest[0] = (byte)(0xC0 | (codepoint >> 6));
                dest[1] = (byte)(0x80 | (codepoint & 0x3F));
                return 2;
            }
            else if (codepoint < 0x10000)
            {
                dest[0] = (byte)(0xE0 | (codepoint >> 12));
                dest[1] = (byte)(0x80 | ((codepoint >> 6) & 0x3F));
                dest[2] = (byte)(0x80 | (codepoint & 0x3F));
                return 3;
            }
            else
            {
                dest[0] = (byte)(0xF0 | (codepoint >> 18));
                dest[1] = (byte)(0x80 | ((codepoint >> 12) & 0x3F));
                dest[2] = (byte)(0x80 | ((codepoint >> 6) & 0x3F));
                dest[3] = (byte)(0x80 | (codepoint & 0x3F));
                return 4;
            }
        }

        // 旧的编码方法直接委托到新的方法（保证向后兼容）
        private static int EncodeUtf8Char(char c, byte* dest) => EncodeUtf8Codepoint(c, dest);

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

        //************** 自定义枚举器实现 **************

        public CharEnumerable CharValues => new CharEnumerable(this);

        public unsafe struct CharEnumerable : IEnumerable<int>
        {
            private UString _str;
            public CharEnumerable(UString s) => _str = s;

            public IEnumerator<int> GetEnumerator() => new CharEnumerator(_str);

            IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<int>)this).GetEnumerator();
        }

        public unsafe struct CharEnumerator : IEnumerator<int>
        {
            private UString _str;
            private int _pos;
            private int _current;
            public CharEnumerator(UString s)
            {
                _str = s;
                _pos = 0;
                _current = default;
            }
            public int Current => _current;

            object IEnumerator.Current => _current;

            public bool MoveNext()
            {
                if (_pos >= _str.Length)
                    return false;
                int cp;
                int size = DecodeUtf8Char(_str.Ptr + _pos, _str.Length - _pos, out cp);
                if (size == 0)
                    throw new InvalidOperationException("Invalid UTF-8 sequence");
                _current = cp;
                _pos += size;
                return true;
            }
            public void Reset() { _pos = 0; }
            public void Dispose() { }
        }

        //************************************************

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

        public int RemoveAt(int charIndex)
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
            if (pos >= Length)
                throw new ArgumentOutOfRangeException(nameof(charIndex));

            int deleteStart = pos;
            int deleteSize = DecodeUtf8Char(Ptr + pos, Length - pos, out int removedCp);
            if (deleteSize == 0)
                throw new InvalidOperationException("Invalid UTF-8 sequence");

            int remaining = Length - (deleteStart + deleteSize);
            Buffer.MemoryCopy(Ptr + deleteStart + deleteSize, Ptr + deleteStart, Capacity - deleteStart, remaining);
            Length -= deleteSize;
            return removedCp;
        }

        public void InsertAt(int charIndex, char c)
        {
            int insertPos = GetByteOffsetForCharIndex(charIndex);
            byte* tmp = stackalloc byte[4];
            int encoded = EncodeUtf8Codepoint(c, tmp);

            int newLength = Length + encoded;
            EnsureCapacity(newLength);
            Buffer.MemoryCopy(Ptr + insertPos, Ptr + insertPos + encoded, Capacity - (insertPos + encoded), Length - insertPos);
            for (int i = 0; i < encoded; i++)
                Ptr[insertPos + i] = tmp[i];
            Length = newLength;
        }

        // 更新后的 InsertAt 处理字符串中的代理对
        public void InsertAt(int charIndex, string s)
        {
            if (s == null)
                throw new ArgumentNullException(nameof(s));
            int insertPos = GetByteOffsetForCharIndex(charIndex);
            int addBytes = Utf8ByteCount(s);
            int newLength = Length + addBytes;
            EnsureCapacity(newLength);
            Buffer.MemoryCopy(Ptr + insertPos, Ptr + insertPos + addBytes, Capacity - (insertPos + addBytes), Length - insertPos);
            int offset = insertPos;
            int i = 0;
            fixed (char* ps = s)
            {
                while (i < s.Length)
                {
                    char c = ps[i];
                    int codepoint;
                    int encoded;
                    if (char.IsHighSurrogate(c) && i + 1 < s.Length && char.IsLowSurrogate(ps[i + 1]))
                    {
                        codepoint = char.ConvertToUtf32(c, ps[i + 1]);
                        encoded = EncodeUtf8Codepoint(codepoint, Ptr + offset);
                        i += 2;
                    }
                    else
                    {
                        codepoint = c;
                        encoded = EncodeUtf8Codepoint(codepoint, Ptr + offset);
                        i++;
                    }
                    offset += encoded;
                }
            }
            Length = newLength;
        }

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

        /// 重写 ToString 方法（仅用于调试），内部调用 ToUtf16() 方法，
        /// 通过构造新的托管字符串返回，但此处仅为测试，不建议在高性能场景中使用。
        public override string ToString()
        {
            // 此处仅作参考，建议在高性能场景中直接使用 ToUtf16(out int count)
            int count;
            IntPtr ptr = ToUtf16(out count);
            string s = new string((char*)ptr, 0, count);
            Marshal.FreeHGlobal(ptr);
            return s;
        }

        //===========================================================
        // 以下实现格式化与比较等运算符支持，不调用托管转换实现核心逻辑

        /// <summary>
        /// 将内部 UTF-8 数据转换为 UTF-16（非托管内存）表示。
        /// 调用者负责释放返回的内存（使用 Marshal.FreeHGlobal）。
        /// </summary>
        /// <param name="charCount">转换后的字符数</param>
        /// <returns>指向 UTF-16 字符数据的指针</returns>
        public IntPtr ToUtf16(out int charCount)
        {
            // 第一遍：计算转换后所需的 UTF-16 字符总个数
            int required = 0;
            int pos = 0;
            while (pos < Length)
            {
                int cp;
                int size = DecodeUtf8Char(Ptr + pos, Length - pos, out cp);
                if (size == 0)
                    throw new InvalidOperationException("Invalid UTF-8 sequence");
                pos += size;
                // 对于 BMP 内的字符，一个字符；对于补充字符，需要两个 UTF-16 字符（代理对）
                required += (cp < 0x10000) ? 1 : 2;
            }
            charCount = required;
            // 分配转换后的 UTF-16 缓冲区
            IntPtr utf16Ptr = Marshal.AllocHGlobal(required * sizeof(char));
            char* dest = (char*)utf16Ptr;

            // 第二遍：进行实际转换
            pos = 0;
            int index = 0;
            while (pos < Length)
            {
                int cp;
                int size = DecodeUtf8Char(Ptr + pos, Length - pos, out cp);
                if (size == 0)
                    throw new InvalidOperationException("Invalid UTF-8 sequence");
                pos += size;
                if (cp < 0x10000)
                {
                    dest[index++] = (char)cp;
                }
                else
                {
                    // 需要生成代理对
                    cp -= 0x10000;
                    dest[index++] = (char)(0xD800 | (cp >> 10));     // 高代理项
                    dest[index++] = (char)(0xDC00 | (cp & 0x3FF));     // 低代理项
                }
            }
            return utf16Ptr;
        }

        // IEquatable 实现：逐字节比较
        public bool Equals(UString other)
        {
            if (Length != other.Length)
                return false;
            for (int i = 0; i < Length; i++)
            {
                if (Ptr[i] != other.Ptr[i])
                    return false;
            }
            return true;
        }

        public override bool Equals(object? obj)
        {
            if (obj is UString other)
                return Equals(other);
            return false;
        }

        // GetHashCode 使用 FNV-1a 算法
        public override int GetHashCode()
        {
            const int fnvPrime = 0x01000193;
            int hash = unchecked((int)0x811C9DC5);
            for (int i = 0; i < Length; i++)
            {
                hash ^= Ptr[i];
                hash *= fnvPrime;
            }
            return hash;
        }

        // IComparable 实现：按字典序逐字节比较
        public int CompareTo(UString other)
        {
            int min = Length < other.Length ? Length : other.Length;
            for (int i = 0; i < min; i++)
            {
                int diff = Ptr[i] - other.Ptr[i];
                if (diff != 0)
                    return diff;
            }
            return Length - other.Length;
        }

        // 重载比较运算符
        public static bool operator ==(UString left, UString right) => left.Equals(right);
        public static bool operator !=(UString left, UString right) => !left.Equals(right);
        public static bool operator <(UString left, UString right) => left.CompareTo(right) < 0;
        public static bool operator >(UString left, UString right) => left.CompareTo(right) > 0;
        public static bool operator <=(UString left, UString right) => left.CompareTo(right) <= 0;
        public static bool operator >=(UString left, UString right) => left.CompareTo(right) >= 0;

        /// <summary>
        /// 在字符串尾部追加一个字符.
        /// </summary>
        public void Push(char c)
        {
            // 等同于在末尾插入，此处使用已有 InsertAt 方法
            InsertAt(CharCount(), c);
        }

        /// <summary>
        /// 在字符串尾部追加一个字符串.
        /// </summary>
        public void PushStr(string s)
        {
            InsertAt(CharCount(), s);
        }

        /// <summary>
        /// 以字符索引为单位，返回并分离出从指定索引开始的后半部分字符串。
        /// 原字符串长度更新为分离前半部分的长度。
        /// </summary>
        public UString SplitOff(int charIndex)
        {
            int offset = GetByteOffsetForCharIndex(charIndex);
            int newLen = Length - offset;
            UString result = WithCapacity(newLen);
            // 拷贝剩余的字节
            for (int i = 0; i < newLen; i++)
            {
                result.Ptr[i] = Ptr[offset + i];
            }
            result.Length = newLen;
            // 更新当前字符串：截断到 offset
            Length = offset;
            return result;
        }

        /// <summary>
        /// 删除 [charIndexStart, charIndexEnd) 范围内的字符，并返回被删除部分构成的 UString。
        /// </summary>
        public UString Drain(int charIndexStart, int charIndexEnd)
        {
            int start = GetByteOffsetForCharIndex(charIndexStart);
            int end = GetByteOffsetForCharIndex(charIndexEnd);
            if (end < start)
                throw new ArgumentException("结束位置不能小于起始位置");

            int drainLen = end - start;
            UString result = WithCapacity(drainLen);
            for (int i = 0; i < drainLen; i++)
            {
                result.Ptr[i] = Ptr[start + i];
            }
            result.Length = drainLen;
            // 移动后续数据填补空缺
            int remaining = Length - end;
            for (int i = 0; i < remaining; i++)
            {
                Ptr[start + i] = Ptr[end + i];
            }
            Length -= drainLen;
            return result;
        }

        /// <summary>
        /// 用 replacement 替换 [charIndexStart, charIndexEnd) 范围的字符。
        /// </summary>
        public void ReplaceRange(int charIndexStart, int charIndexEnd, UString replacement)
        {
            int start = GetByteOffsetForCharIndex(charIndexStart);
            int end = GetByteOffsetForCharIndex(charIndexEnd);
            if (end < start)
                throw new ArgumentException("结束位置不能小于起始位置");

            int oldRangeLen = end - start;
            int newRangeLen = replacement.Length;
            int newTotal = Length - oldRangeLen + newRangeLen;

            EnsureCapacity(newTotal);

            // 当新内容长度大于旧区间，则向后移动尾部数据
            if (newRangeLen != oldRangeLen)
            {
                int tailLen = Length - end;
                if (newRangeLen > oldRangeLen)
                {
                    int shift = newRangeLen - oldRangeLen;
                    // 从尾部开始向后复制 shift 个字节
                    for (int i = tailLen - 1; i >= 0; i--)
                    {
                        Ptr[end + i + shift] = Ptr[end + i];
                    }
                }
                else // newRangeLen < oldRangeLen，则向前移动数据
                {
                    int shift = oldRangeLen - newRangeLen;
                    for (int i = 0; i < (Length - end); i++)
                    {
                        Ptr[end - shift + i] = Ptr[end + i];
                    }
                }
            }
            // 将 replacement 数据复制到指定区间
            for (int i = 0; i < newRangeLen; i++)
            {
                Ptr[start + i] = replacement.Ptr[i];
            }

            Length = newTotal;
        }

        /// <summary>
        /// 保留字符串中满足 predicate 谓词的字符，其它字符删除。
        /// predicate 的第一个参数为字符 Unicode 码点，第二个参数为字符索引（按字符计）。
        /// </summary>
        public void Retain(UnmanagedCharPredicate predicate)
        {
            int src = 0, dest = 0, charIndex = 0;
            while (src < Length)
            {
                int cp;
                int size = DecodeUtf8Char(Ptr + src, Length - src, out cp);
                if (size == 0)
                    throw new InvalidOperationException("Invalid UTF-8 sequence");
                // 若 predicate 返回 true，则保留此字符（可能需要移动数据到 dest 位置）
                if (predicate(cp, charIndex))
                {
                    if (src != dest)
                    {
                        // 拷贝 size 个字节
                        for (int i = 0; i < size; i++)
                        {
                            Ptr[dest + i] = Ptr[src + i];
                        }
                    }
                    dest += size;
                }
                // 无论保留与否均计数
                src += size;
                charIndex++;
            }
            Length = dest;
        }
        public object Clone()
        {
            UString clone = WithCapacity(Capacity);
            Buffer.MemoryCopy(Ptr, clone.Ptr, Capacity, Length);
            clone.Length = Length;
            return clone;
        }

    }
}
