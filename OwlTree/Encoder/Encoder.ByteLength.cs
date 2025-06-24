using System;
using System.Text;

namespace OwlTree
{
    public static partial class Encoder
    {
        /// <summary>
        /// Gets the expected byte length of the given encodable object.
        /// If the object is not encodable, return -1.
        /// </summary>
        public static int GetByteLength(object arg)
        {
            var t = arg.GetType();
            if (
                t == typeof(int) ||
                t == typeof(uint) ||
                t == typeof(float)
            )
            {
                return 4;
            }
            else if (
                t == typeof(double) ||
                t == typeof(long) ||
                t == typeof(ulong)
            )
            {
                return 8;
            }
            else if (
                t == typeof(ushort) ||
                t == typeof(short)
            )
            {
                return 2;
            }
            else if (
                t == typeof(byte) ||
                t == typeof(bool)
            )
            {
                return 1;
            }
            else if (t == typeof(string))
            {
                return 1 + Encoding.UTF8.GetByteCount((string)arg);
            }
            else if (arg is IEncodable)
            {
                if (arg is IVariableLength)
                    return ((IEncodable)arg).ByteLength() + IVariableLength.LengthEncoding;
                return ((IEncodable)arg).ByteLength();
            }
            return -1;
        }

        public static int GetByteLength(int arg) => 4;
        public static int GetByteLength(uint arg) => 4;
        public static int GetByteLength(float arg) => 4;

        public static int GetByteLength(long arg) => 8;
        public static int GetByteLength(ulong arg) => 8;
        public static int GetByteLength(double arg) => 8;

        public static int GetByteLength(short arg) => 2;
        public static int GetByteLength(ushort arg) => 2;

        public static int GetByteLength(byte arg) => 1;
        public static int GetByteLength(bool arg) => 1;

        public static int GetByteLength(string arg) => 1 + Encoding.UTF8.GetByteCount(arg);

        public static int GetByteLength(IEncodable arg)
        {
            if (arg is IVariableLength)
                return arg.ByteLength() + IVariableLength.LengthEncoding;
            return arg.ByteLength();
        }

        /// <summary>
        /// Returns the maximum number of bytes the given type of encodable object can take.
        /// If the type is not encodable, returns -1.
        /// </summary>
        public static int GetMaxLength(Type t)
        {
            if (
                t == typeof(int) ||
                t == typeof(uint) ||
                t == typeof(float)
            )
            {
                return 4;
            }
            else if (
                t == typeof(double) ||
                t == typeof(long) ||
                t == typeof(ulong)
            )
            {
                return 8;
            }
            else if (
                t == typeof(ushort) ||
                t == typeof(short)
            )
            {
                return 2;
            }
            else if (
                t == typeof(byte) ||
                t == typeof(bool)
            )
            {
                return 1;
            }
            else if (t == typeof(string))
            {
                return 256;
            }
            else
            {
                var encodable = typeof(IEncodable);
                var variableLen = typeof(IVariableLength);
                var encodableTypes = t.GetInterfaces();
                int len = -1;
                foreach (var a in encodableTypes)
                {
                    if (a == encodable)
                    {
                        IEncodable obj = (IEncodable)Activator.CreateInstance(t);
                        len = obj.ByteLength();
                    }
                    else if (a == variableLen)
                    {
                        IVariableLength obj = (IVariableLength)Activator.CreateInstance(t);
                        return obj.MaxLength();
                    }
                }
                return len;
            }
        }
    }
}