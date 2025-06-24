using System;
using System.Text;

namespace OwlTree
{
    public static partial class Encoder
    {
        // * Is Encodable

        /// <summary>
        /// Returns whether or not the given type represents an encodable object,
        /// which can be used an RPC parameter.
        /// </summary>
        public static bool IsEncodable<T>() where T : new()
        {
            var t = typeof(T);
            if (
                t == typeof(int) ||
                t == typeof(uint) ||
                t == typeof(float) ||

                t == typeof(double) ||
                t == typeof(long) ||
                t == typeof(ulong) ||

                t == typeof(ushort) ||
                t == typeof(short) ||

                t == typeof(byte) ||
                t == typeof(bool) ||

                t == typeof(string)
            )
            {
                return true;
            }
            else
            {
                var a = new T();
                return a is IEncodable;
            }
        }

        /// <summary>
        /// Returns whether or not the given type represents an encodable object,
        /// which can be used an RPC parameter.
        /// </summary>
        public static bool IsEncodable(Type t)
        {
            if (
                t == typeof(int) ||
                t == typeof(uint) ||
                t == typeof(float) ||

                t == typeof(double) ||
                t == typeof(long) ||
                t == typeof(ulong) ||

                t == typeof(ushort) ||
                t == typeof(short) ||

                t == typeof(byte) ||
                t == typeof(bool) ||

                t == typeof(string)
            )
            {
                return true;
            }
            else
            {
                return Activator.CreateInstance(t) is IEncodable;
            }
        }

        /// <summary>
        /// Returns true if the given object is an encodable object,
        /// which can be used as an RPC argument.
        /// </summary>
        public static bool IsEncodable(object obj)
        {
            var t = obj.GetType();
            if (
                t == typeof(int) ||
                t == typeof(uint) ||
                t == typeof(float) ||

                t == typeof(double) ||
                t == typeof(long) ||
                t == typeof(ulong) ||

                t == typeof(ushort) ||
                t == typeof(short) ||

                t == typeof(byte) ||
                t == typeof(bool) ||

                t == typeof(string)
            )
            {
                return true;
            }
            else
            {
                return obj is IEncodable;
            }
        }

        // * Decoding

        /// <summary>
        /// Decodes an object of the given type from the given bytes. Returns the decoded object. 
        /// If the given type isn't encodable, then an empty object will be returned.
        /// </summary>
        public static object DecodeObject(ReadOnlySpan<byte> bytes, Type t)
        {
            return DecodeObject(bytes, t, out var len);
        }

        /// <summary>
        /// Decodes an object of the given type from the given bytes. Returns the decoded object,
        /// and the number of bytes that were read. If the given type isn't encodable, then len will be set to -1,
        /// and an empty object will be returned.
        /// </summary>
        public static object DecodeObject(ReadOnlySpan<byte> bytes, Type t, out int len)
        {
            if (t == typeof(string))
                return DecodeString(bytes, out len);

            else if (t == typeof(int))
                return DecodeInt32(bytes, out len);

            else if (t == typeof(uint))
                return DecodeUInt32(bytes, out len);

            else if (t == typeof(float))
                return DecodeFloat(bytes, out len);

            else if (t == typeof(double))
                return DecodeDouble(bytes, out len);

            else if (t == typeof(long))
                return DecodeInt64(bytes, out len);

            else if (t == typeof(ulong))
                return DecodeUInt64(bytes, out len);

            else if (t == typeof(ushort))
                return DecodeUInt16(bytes, out len);

            else if (t == typeof(short))
                return DecodeInt16(bytes, out len);

            else if (t == typeof(bool))
                return DecodeBool(bytes, out len);

            else if (t == typeof(byte))
            {
                len = 1;
                return bytes[0];
            }
            else
            {
                var result = Activator.CreateInstance(t);
                bool isEncodable = result is IEncodable;
                bool isVariable = result is IVariableLength;

                if (isEncodable)
                {
                    len = isVariable ? IVariableLength.GetLength(bytes) : ((IEncodable)result).ByteLength();
                    ((IEncodable)result).FromBytes(bytes.Slice(isVariable ? IVariableLength.LengthEncoding : 0, len));
                    len += isVariable ? IVariableLength.LengthEncoding : 0;
                    return result;
                }
                else
                {
                    len = -1;
                    return null;
                }
            }
        }

        public static int DecodeInt32(ReadOnlySpan<byte> bytes)
        {
            return BitConverter.ToInt32(bytes);
        }

        public static int DecodeInt32(ReadOnlySpan<byte> bytes, out int len)
        {
            len = 4;
            return BitConverter.ToInt32(bytes);
        }

        public static uint DecodeUInt32(ReadOnlySpan<byte> bytes)
        {
            return BitConverter.ToUInt32(bytes);
        }

        public static uint DecodeUInt32(ReadOnlySpan<byte> bytes, out int len)
        {
            len = 4;
            return BitConverter.ToUInt32(bytes);
        }

        public static float DecodeFloat(ReadOnlySpan<byte> bytes)
        {
            return BitConverter.ToSingle(bytes);
        }

        public static float DecodeFloat(ReadOnlySpan<byte> bytes, out int len)
        {
            len = 4;
            return BitConverter.ToSingle(bytes);
        }

        public static long DecodeInt64(ReadOnlySpan<byte> bytes)
        {
            return BitConverter.ToInt64(bytes);
        }

        public static long DecodeInt64(ReadOnlySpan<byte> bytes, out int len)
        {
            len = 8;
            return BitConverter.ToInt64(bytes);
        }

        public static ulong DecodeUInt64(ReadOnlySpan<byte> bytes)
        {
            return BitConverter.ToUInt64(bytes);
        }

        public static ulong DecodeUInt64(ReadOnlySpan<byte> bytes, out int len)
        {
            len = 8;
            return BitConverter.ToUInt64(bytes);
        }

        public static double DecodeDouble(ReadOnlySpan<byte> bytes)
        {
            return BitConverter.ToDouble(bytes);
        }

        public static double DecodeDouble(ReadOnlySpan<byte> bytes, out int len)
        {
            len = 8;
            return BitConverter.ToDouble(bytes);
        }

        public static short DecodeInt16(ReadOnlySpan<byte> bytes)
        {
            return BitConverter.ToInt16(bytes);
        }

        public static short DecodeInt16(ReadOnlySpan<byte> bytes, out int len)
        {
            len = 2;
            return BitConverter.ToInt16(bytes);
        }

        public static ushort DecodeUInt16(ReadOnlySpan<byte> bytes)
        {
            return BitConverter.ToUInt16(bytes);
        }

        public static ushort DecodeUInt16(ReadOnlySpan<byte> bytes, out int len)
        {
            len = 2;
            return BitConverter.ToUInt16(bytes);
        }

        public static bool DecodeBool(ReadOnlySpan<byte> bytes)
        {
            return bytes[0] == 1;
        }

        public static bool DecodeBool(ReadOnlySpan<byte> bytes, out int len)
        {
            len = 1;
            return bytes[0] == 1;
        }

        public static string DecodeString(ReadOnlySpan<byte> bytes)
        {
            var length = bytes[0];
            var str = Encoding.UTF8.GetString(bytes.ToArray(), 1, length);
            return str;
        }

        public static string DecodeString(ReadOnlySpan<byte> bytes, out int len)
        {
            var length = bytes[0];
            var str = Encoding.UTF8.GetString(bytes.ToArray(), 1, length);
            len = length + 1;
            return str;
        }

        public static T DecodeEncodable<T>(ReadOnlySpan<byte> bytes) where T : IEncodable, new()
        {
            T result = new T();
            var isVariable = result is IVariableLength;

            var len = isVariable ? IVariableLength.GetLength(bytes) : result.ByteLength();
            result.FromBytes(bytes.Slice(isVariable ? IVariableLength.LengthEncoding : 0, len));

            return result;
        }

        public static T DecodeEncodable<T>(ReadOnlySpan<byte> bytes, out int len) where T : IEncodable, new()
        {
            T result = new T();
            var isVariable = result is IVariableLength;

            len = isVariable ? IVariableLength.GetLength(bytes) : result.ByteLength();
            result.FromBytes(bytes.Slice(isVariable ? IVariableLength.LengthEncoding : 0, len));
            len += isVariable ? IVariableLength.LengthEncoding : 0;

            return result;
        }

        // * Encoding

        /// <summary>
        /// Encodes the given encodable object into the given span of bytes.
        /// Verify the object is encodable with <c>IsEncodableParam()</c>.
        /// Verify the span provides enough bytes by comparing it's length to
        /// the result of <c>GetExpectedLength()</c>
        /// </summary>
        public static void InsertBytes(Span<byte> bytes, object arg)
        {
            var t = arg.GetType();
            if (t == typeof(int))
            {
                BitConverter.TryWriteBytes(bytes, (int)arg);
            }
            else if (t == typeof(uint))
            {
                BitConverter.TryWriteBytes(bytes, (uint)arg);
            }
            else if (t == typeof(float))
            {
                BitConverter.TryWriteBytes(bytes, (float)arg);
            }
            else if (t == typeof(double))
            {
                BitConverter.TryWriteBytes(bytes, (double)arg);
            }
            else if (t == typeof(long))
            {
                BitConverter.TryWriteBytes(bytes, (long)arg);
            }
            else if (t == typeof(ulong))
            {
                BitConverter.TryWriteBytes(bytes, (ulong)arg);
            }
            else if (t == typeof(ushort))
            {
                BitConverter.TryWriteBytes(bytes, (ushort)arg);
            }
            else if (t == typeof(short))
            {
                BitConverter.TryWriteBytes(bytes, (short)arg);
            }
            else if (t == typeof(byte))
            {
                bytes[0] = (byte)arg;
            }
            else if (t == typeof(bool))
            {
                bytes[0] = (byte)(((bool)arg) ? 1 : 0);
            }
            else if (t == typeof(string))
            {
                var encoding = Encoding.UTF8.GetBytes((string)arg);
                if (encoding.Length > 255)
                    throw new InvalidOperationException("strings cannot require more than 255 bytes to encode.");
                bytes[0] = (byte)encoding.Length;
                for (int i = 0; i < encoding.Length; i++)
                    bytes[i + 1] = encoding[i];
            }
            else
            {
                bool isEncodable = arg is IEncodable;
                bool isVariable = arg is IVariableLength;

                if (isEncodable)
                {
                    if (isVariable)
                    {
                        IVariableLength.InsertLength(bytes, ((IEncodable)arg).ByteLength());
                        bytes = bytes.Slice(IVariableLength.LengthEncoding);
                    }
                    ((IEncodable)arg).InsertBytes(bytes);
                }
            }
        }

        public static void InsertBytes(Span<byte> bytes, int arg)
        {
            BitConverter.TryWriteBytes(bytes, arg);
        }

        public static void InsertBytes(Span<byte> bytes, uint arg)
        {
            BitConverter.TryWriteBytes(bytes, arg);
        }

        public static void InsertBytes(Span<byte> bytes, float arg)
        {
            BitConverter.TryWriteBytes(bytes, arg);
        }

        public static void InsertBytes(Span<byte> bytes, long arg)
        {
            BitConverter.TryWriteBytes(bytes, arg);
        }

        public static void InsertBytes(Span<byte> bytes, ulong arg)
        {
            BitConverter.TryWriteBytes(bytes, arg);
        }
        
        public static void InsertBytes(Span<byte> bytes, double arg)
        {
            BitConverter.TryWriteBytes(bytes, arg);
        }

        public static void InsertBytes(Span<byte> bytes, short arg)
        {
            BitConverter.TryWriteBytes(bytes, arg);
        }

        public static void InsertBytes(Span<byte> bytes, ushort arg)
        {
            BitConverter.TryWriteBytes(bytes, arg);
        }

        public static void InsertBytes(Span<byte> bytes, byte arg)
        {
            bytes[0] = arg;
        }

        public static void InsertBytes(Span<byte> bytes, bool arg)
        {
            bytes[0] = (byte)(arg ? 1 : 0);
        }

        public static void InsertBytes(Span<byte> bytes, string arg)
        {
            var encoding = Encoding.UTF8.GetBytes(arg);
            if (encoding.Length > 255)
                throw new InvalidOperationException("strings cannot require more than 255 bytes to encode.");
            bytes[0] = (byte)encoding.Length;
            for (int i = 0; i < encoding.Length; i++)
                bytes[i + 1] = encoding[i];
        }

        public static void InsertBytes(Span<byte> bytes, IEncodable arg)
        {
            if (arg is IVariableLength)
            {
                IVariableLength.InsertLength(bytes, arg.ByteLength());
                bytes = bytes.Slice(IVariableLength.LengthEncoding);
            }
            arg.InsertBytes(bytes);
        }
    }
}