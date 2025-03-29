using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
namespace MoreUnmanagedTypes
{
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

        public override string ToString()
        {
            if (Length == 0)
                return string.Empty;
            byte[] data = new byte[Length];
            for (int i = 0; i < Length; i++)
                data[i] = Ptr[i];
            return System.Text.Encoding.UTF8.GetString(data);
        }
    }
}
