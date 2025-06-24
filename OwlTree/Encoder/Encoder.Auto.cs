using System;

namespace OwlTree
{
    public static partial class Encoder
    {
        /// <summary>
        /// Calculates the byte length for an IEncodable, based on the properties
        /// you want to have encoded.
        /// </summary>
        public static int AutoByteLength(params object[] members)
        {
            return GetExpectedArgsLength(members);
        }

        /// <summary>
        /// Calculates the max byte length for an IVariableLength, based on the properties
        /// you want to have encoded. These must be the same as what is given to <c>AutoByteLength()</c>.
        /// </summary>
        public static int AutoMaxLength(params object[] members)
        {
            int total = 0;
            for (int i = 0; i < members.Length; i++)
            {
                var size = GetMaxLength(members[i].GetType());
                if (size == -1)
                    throw new ArgumentException($"'{members[i].GetType()}' is not an encodable type.");
                total += size;
            }
            return total;
        }

        /// <summary>
        /// Encode the given properties into the given bytes.
        /// </summary>
        public static void AutoInsertBytes(Span<byte> bytes, params object[] members)
        {
            int ind = 0;
            foreach (var m in members)
            {
                InsertBytes(bytes.Slice(ind), m);
                ind += GetByteLength(m);
            }
        }

        /// <summary>
        /// Decode the value from the given bytes, and set the given property to that value.
        /// Returns an <c>AutoDecoder</c> which can be used to chain <c>AutoDecode()</c> calls
        /// together to decode all of the encoded properties.
        /// </summary>
        public static AutoDecoder AutoFromBytes(ReadOnlySpan<byte> bytes, ref int member)
        {
            var decoder = new AutoDecoder();
            decoder.AutoFromBytes(bytes, ref member);
            return decoder;
        }

        /// <summary>
        /// Decode the value from the given bytes, and set the given property to that value.
        /// Returns an <c>AutoDecoder</c> which can be used to chain <c>AutoDecode()</c> calls
        /// together to decode all of the encoded properties.
        /// </summary>
        public static AutoDecoder AutoFromBytes(ReadOnlySpan<byte> bytes, ref uint member)
        {
            var decoder = new AutoDecoder();
            decoder.AutoFromBytes(bytes, ref member);
            return decoder;
        }

        /// <summary>
        /// Decode the value from the given bytes, and set the given property to that value.
        /// Returns an <c>AutoDecoder</c> which can be used to chain <c>AutoDecode()</c> calls
        /// together to decode all of the encoded properties.
        /// </summary>
        public static AutoDecoder AutoFromBytes(ReadOnlySpan<byte> bytes, ref float member)
        {
            var decoder = new AutoDecoder();
            decoder.AutoFromBytes(bytes, ref member);
            return decoder;
        }

        /// <summary>
        /// Decode the value from the given bytes, and set the given property to that value.
        /// Returns an <c>AutoDecoder</c> which can be used to chain <c>AutoDecode()</c> calls
        /// together to decode all of the encoded properties.
        /// </summary>
        public static AutoDecoder AutoFromBytes(ReadOnlySpan<byte> bytes, ref double member)
        {
            var decoder = new AutoDecoder();
            decoder.AutoFromBytes(bytes, ref member);
            return decoder;
        }

        /// <summary>
        /// Decode the value from the given bytes, and set the given property to that value.
        /// Returns an <c>AutoDecoder</c> which can be used to chain <c>AutoDecode()</c> calls
        /// together to decode all of the encoded properties.
        /// </summary>
        public static AutoDecoder AutoFromBytes(ReadOnlySpan<byte> bytes, ref long member)
        {
            var decoder = new AutoDecoder();
            decoder.AutoFromBytes(bytes, ref member);
            return decoder;
        }

        /// <summary>
        /// Decode the value from the given bytes, and set the given property to that value.
        /// Returns an <c>AutoDecoder</c> which can be used to chain <c>AutoDecode()</c> calls
        /// together to decode all of the encoded properties.
        /// </summary>
        public static AutoDecoder AutoFromBytes(ReadOnlySpan<byte> bytes, ref ulong member)
        {
            var decoder = new AutoDecoder();
            decoder.AutoFromBytes(bytes, ref member);
            return decoder;
        }

        /// <summary>
        /// Decode the value from the given bytes, and set the given property to that value.
        /// Returns an <c>AutoDecoder</c> which can be used to chain <c>AutoDecode()</c> calls
        /// together to decode all of the encoded properties.
        /// </summary>
        public static AutoDecoder AutoFromBytes(ReadOnlySpan<byte> bytes, ref short member)
        {
            var decoder = new AutoDecoder();
            decoder.AutoFromBytes(bytes, ref member);
            return decoder;
        }

        /// <summary>
        /// Decode the value from the given bytes, and set the given property to that value.
        /// Returns an <c>AutoDecoder</c> which can be used to chain <c>AutoDecode()</c> calls
        /// together to decode all of the encoded properties.
        /// </summary>
        public static AutoDecoder AutoFromBytes(ReadOnlySpan<byte> bytes, ref ushort member)
        {
            var decoder = new AutoDecoder();
            decoder.AutoFromBytes(bytes, ref member);
            return decoder;
        }

        /// <summary>
        /// Decode the value from the given bytes, and set the given property to that value.
        /// Returns an <c>AutoDecoder</c> which can be used to chain <c>AutoDecode()</c> calls
        /// together to decode all of the encoded properties.
        /// </summary>
        public static AutoDecoder AutoFromBytes(ReadOnlySpan<byte> bytes, ref byte member)
        {
            var decoder = new AutoDecoder();
            decoder.AutoFromBytes(bytes, ref member);
            return decoder;
        }

        /// <summary>
        /// Decode the value from the given bytes, and set the given property to that value.
        /// Returns an <c>AutoDecoder</c> which can be used to chain <c>AutoDecode()</c> calls
        /// together to decode all of the encoded properties.
        /// </summary>
        public static AutoDecoder AutoFromBytes(ReadOnlySpan<byte> bytes, ref bool member)
        {
            var decoder = new AutoDecoder();
            decoder.AutoFromBytes(bytes, ref member);
            return decoder;
        }

        /// <summary>
        /// Decode the value from the given bytes, and set the given property to that value.
        /// Returns an <c>AutoDecoder</c> which can be used to chain <c>AutoDecode()</c> calls
        /// together to decode all of the encoded properties.
        /// </summary>
        public static AutoDecoder AutoFromBytes(ReadOnlySpan<byte> bytes, ref string member)
        {
            var decoder = new AutoDecoder();
            decoder.AutoFromBytes(bytes, ref member);
            return decoder;
        }

        /// <summary>
        /// Decode the value from the given bytes, and set the given property to that value.
        /// Returns an <c>AutoDecoder</c> which can be used to chain <c>AutoDecode()</c> calls
        /// together to decode all of the encoded properties.
        /// </summary>
        public static AutoDecoder AutoFromBytes(ReadOnlySpan<byte> bytes, ref IEncodable member)
        {
            var decoder = new AutoDecoder();
            decoder.AutoFromBytes(bytes, ref member);
            return decoder;
        }

        /// <summary>
        /// Decode the value from the given bytes, and set the given property to that value.
        /// Returns an <c>AutoDecoder</c> which can be used to chain <c>AutoDecode()</c> calls
        /// together to decode all of the encoded properties.
        /// </summary>
        public static AutoDecoder AutoFromBytes<T>(ReadOnlySpan<byte> bytes, ref T member) where T : IEncodable
        {
            var decoder = new AutoDecoder();
            decoder.AutoFromBytes(bytes, ref member);
            return decoder;
        }

        /// <summary>
        /// Tracks index to walk a span of bytes. Increments based on the size of the
        /// objects decoded.
        /// </summary>
        public struct AutoDecoder
        {
            private int _ind;

            public AutoDecoder(int ind = 0)
            {
                _ind = ind;
            }

            /// <summary>
            /// Decode the value from the given bytes, and set the given property to that value.
            /// Returns an <c>AutoDecoder</c> which can be used to chain <c>AutoDecode()</c> calls
            /// together to decode all of the encoded properties.
            /// </summary>
            public AutoDecoder AutoFromBytes(ReadOnlySpan<byte> bytes, ref int member)
            {
                member = (int)DecodeObject(bytes.Slice(_ind), member.GetType(), out var len);
                _ind += len;
                return this;
            }

            /// <summary>
            /// Decode the value from the given bytes, and set the given property to that value.
            /// Returns an <c>AutoDecoder</c> which can be used to chain <c>AutoDecode()</c> calls
            /// together to decode all of the encoded properties.
            /// </summary>
            public AutoDecoder AutoFromBytes(ReadOnlySpan<byte> bytes, ref uint member)
            {
                member = (uint)DecodeObject(bytes.Slice(_ind), member.GetType(), out var len);
                _ind += len;
                return this;
            }

            /// <summary>
            /// Decode the value from the given bytes, and set the given property to that value.
            /// Returns an <c>AutoDecoder</c> which can be used to chain <c>AutoDecode()</c> calls
            /// together to decode all of the encoded properties.
            /// </summary>
            public AutoDecoder AutoFromBytes(ReadOnlySpan<byte> bytes, ref float member)
            {
                member = (float)DecodeObject(bytes.Slice(_ind), member.GetType(), out var len);
                _ind += len;
                return this;
            }

            /// <summary>
            /// Decode the value from the given bytes, and set the given property to that value.
            /// Returns an <c>AutoDecoder</c> which can be used to chain <c>AutoDecode()</c> calls
            /// together to decode all of the encoded properties.
            /// </summary>
            public AutoDecoder AutoFromBytes(ReadOnlySpan<byte> bytes, ref double member)
            {
                member = (double)DecodeObject(bytes.Slice(_ind), member.GetType(), out var len);
                _ind += len;
                return this;
            }

            /// <summary>
            /// Decode the value from the given bytes, and set the given property to that value.
            /// Returns an <c>AutoDecoder</c> which can be used to chain <c>AutoDecode()</c> calls
            /// together to decode all of the encoded properties.
            /// </summary>
            public AutoDecoder AutoFromBytes(ReadOnlySpan<byte> bytes, ref long member)
            {
                member = (long)DecodeObject(bytes.Slice(_ind), member.GetType(), out var len);
                _ind += len;
                return this;
            }

            /// <summary>
            /// Decode the value from the given bytes, and set the given property to that value.
            /// Returns an <c>AutoDecoder</c> which can be used to chain <c>AutoDecode()</c> calls
            /// together to decode all of the encoded properties.
            /// </summary>
            public AutoDecoder AutoFromBytes(ReadOnlySpan<byte> bytes, ref ulong member)
            {
                member = (ulong)DecodeObject(bytes.Slice(_ind), member.GetType(), out var len);
                _ind += len;
                return this;
            }

            /// <summary>
            /// Decode the value from the given bytes, and set the given property to that value.
            /// Returns an <c>AutoDecoder</c> which can be used to chain <c>AutoDecode()</c> calls
            /// together to decode all of the encoded properties.
            /// </summary>
            public AutoDecoder AutoFromBytes(ReadOnlySpan<byte> bytes, ref short member)
            {
                member = (short)DecodeObject(bytes.Slice(_ind), member.GetType(), out var len);
                _ind += len;
                return this;
            }

            /// <summary>
            /// Decode the value from the given bytes, and set the given property to that value.
            /// Returns an <c>AutoDecoder</c> which can be used to chain <c>AutoDecode()</c> calls
            /// together to decode all of the encoded properties.
            /// </summary>
            public AutoDecoder AutoFromBytes(ReadOnlySpan<byte> bytes, ref ushort member)
            {
                member = (ushort)DecodeObject(bytes.Slice(_ind), member.GetType(), out var len);
                _ind += len;
                return this;
            }

            /// <summary>
            /// Decode the value from the given bytes, and set the given property to that value.
            /// Returns an <c>AutoDecoder</c> which can be used to chain <c>AutoDecode()</c> calls
            /// together to decode all of the encoded properties.
            /// </summary>
            public AutoDecoder AutoFromBytes(ReadOnlySpan<byte> bytes, ref byte member)
            {
                member = (byte)DecodeObject(bytes.Slice(_ind), member.GetType(), out var len);
                _ind += len;
                return this;
            }

            /// <summary>
            /// Decode the value from the given bytes, and set the given property to that value.
            /// Returns an <c>AutoDecoder</c> which can be used to chain <c>AutoDecode()</c> calls
            /// together to decode all of the encoded properties.
            /// </summary>
            public AutoDecoder AutoFromBytes(ReadOnlySpan<byte> bytes, ref bool member)
            {
                member = (bool)DecodeObject(bytes.Slice(_ind), member.GetType(), out var len);
                _ind += len;
                return this;
            }

            /// <summary>
            /// Decode the value from the given bytes, and set the given property to that value.
            /// Returns an <c>AutoDecoder</c> which can be used to chain <c>AutoDecode()</c> calls
            /// together to decode all of the encoded properties.
            /// </summary>
            public AutoDecoder AutoFromBytes(ReadOnlySpan<byte> bytes, ref string member)
            {
                member = (string)DecodeObject(bytes.Slice(_ind), member.GetType(), out var len);
                _ind += len;
                return this;
            }

            /// <summary>
            /// Decode the value from the given bytes, and set the given property to that value.
            /// Returns an <c>AutoDecoder</c> which can be used to chain <c>AutoDecode()</c> calls
            /// together to decode all of the encoded properties.
            /// </summary>
            public AutoDecoder AutoFromBytes(ReadOnlySpan<byte> bytes, ref IEncodable member)
            {
                member = (IEncodable)DecodeObject(bytes.Slice(_ind), member.GetType(), out var len);
                _ind += len;
                return this;
            }

            /// <summary>
            /// Decode the value from the given bytes, and set the given property to that value.
            /// Returns an <c>AutoDecoder</c> which can be used to chain <c>AutoDecode()</c> calls
            /// together to decode all of the encoded properties.
            /// </summary>
            public AutoDecoder AutoFromBytes<T>(ReadOnlySpan<byte> bytes, ref T member) where T : IEncodable
            {
                member = (T)DecodeObject(bytes.Slice(_ind), member.GetType(), out var len);
                _ind += len;
                return this;
            }
        }
    }
}