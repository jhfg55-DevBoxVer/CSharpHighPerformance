using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
namespace MoreUnmanagedTypes
{
    // 定义非托管字符谓词，返回 true 表示保留此字符
    public unsafe delegate bool UnmanagedCharPredicate(int codepoint, int charIndex);

    public unsafe struct UnmanagedString : IComparable<UnmanagedString>, IEquatable<UnmanagedString>
    {
        public byte* Ptr;    // 非托管内存中存储 UTF-8 数据的起始地址
        public int Length;   // 当前字符串的字节长度
        public int Capacity; // 当前已分配的总字节数

        public static UnmanagedString New() => new UnmanagedString { Ptr = null, Length = 0, Capacity = 0 };

        public UnmanagedString(string s)
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

        public bool IsCharBoundary(int index)
        {
            if (index == 0 || index == Length)
                return true;
            return (Ptr[index] & 0xC0) != 0x80;
        }

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
                    count += 3; // 简化处理，忽略代理对
            }
            return count;
        }

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
            private UnmanagedString _str;
            public CharEnumerable(UnmanagedString s) => _str = s;

            public IEnumerator<int> GetEnumerator() => new CharEnumerator(_str);

            IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<int>)this).GetEnumerator();
        }

        public unsafe struct CharEnumerator : IEnumerator<int>
        {
            private UnmanagedString _str;
            private int _pos;
            private int _current;
            public CharEnumerator(UnmanagedString s)
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
            int encoded = EncodeUtf8Char(c, tmp);

            int newLength = Length + encoded;
            EnsureCapacity(newLength);
            Buffer.MemoryCopy(Ptr + insertPos, Ptr + insertPos + encoded, Capacity - (insertPos + encoded), Length - insertPos);
            for (int i = 0; i < encoded; i++)
                Ptr[insertPos + i] = tmp[i];
            Length = newLength;
        }

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
            fixed (char* ps = s)
            {
                for (int i = 0; i < s.Length; i++)
                {
                    offset += EncodeUtf8Char(ps[i], Ptr + offset);
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
            // 此实现假设 UnmanagedString 仅存储 BMP 字符（最多 3 字节编码）
            int count = 0;
            int pos = 0;
            while (pos < Length)
            {
                int cp;
                int size = DecodeUtf8Char(Ptr + pos, Length - pos, out cp);
                if (size == 0)
                    throw new InvalidOperationException("Invalid UTF-8 sequence");
                pos += size;
                count++;
            }
            charCount = count;
            IntPtr utf16Ptr = Marshal.AllocHGlobal(count * sizeof(char));
            char* dest = (char*)utf16Ptr;
            pos = 0;
            int index = 0;
            while (pos < Length && index < count)
            {
                int cp;
                int size = DecodeUtf8Char(Ptr + pos, Length - pos, out cp);
                pos += size;
                // 此处不处理需要代理对的情况
                dest[index++] = (char)cp;
            }
            return utf16Ptr;
        }

        // IEquatable 实现：逐字节比较
        public bool Equals(UnmanagedString other)
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
            if (obj is UnmanagedString other)
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
        public int CompareTo(UnmanagedString other)
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
        public static bool operator ==(UnmanagedString left, UnmanagedString right) => left.Equals(right);
        public static bool operator !=(UnmanagedString left, UnmanagedString right) => !left.Equals(right);
        public static bool operator <(UnmanagedString left, UnmanagedString right) => left.CompareTo(right) < 0;
        public static bool operator >(UnmanagedString left, UnmanagedString right) => left.CompareTo(right) > 0;
        public static bool operator <=(UnmanagedString left, UnmanagedString right) => left.CompareTo(right) <= 0;
        public static bool operator >=(UnmanagedString left, UnmanagedString right) => left.CompareTo(right) >= 0;

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
        public UnmanagedString SplitOff(int charIndex)
        {
            int offset = GetByteOffsetForCharIndex(charIndex);
            int newLen = Length - offset;
            UnmanagedString result = WithCapacity(newLen);
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
        /// 删除 [charIndexStart, charIndexEnd) 范围内的字符，并返回被删除部分构成的 UnmanagedString。
        /// </summary>
        public UnmanagedString Drain(int charIndexStart, int charIndexEnd)
        {
            int start = GetByteOffsetForCharIndex(charIndexStart);
            int end = GetByteOffsetForCharIndex(charIndexEnd);
            if (end < start)
                throw new ArgumentException("结束位置不能小于起始位置");

            int drainLen = end - start;
            UnmanagedString result = WithCapacity(drainLen);
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
        public void ReplaceRange(int charIndexStart, int charIndexEnd, UnmanagedString replacement)
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
    }
}
