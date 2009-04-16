using System;

namespace GT.Utils
{
    /// <summary>
    /// <see cref="BitConverter"/> is sadly not endiannes-agnostic
    /// (e.g., http://blogs.msdn.com/robunoki/archive/2006/04/05/568737.aspx).
    /// Although the Mono project does provide a very featureful Mono.DataConverter
    /// (http://mono-project.com/Mono_DataConvert), it's a bit too featureful,
    /// bordering on unwieldy, and also requires compilation with /unsafe.
    /// </summary>
    public abstract class DataConverter
    {
        // Make the converter always convert to LittleEndian
        // NB: assumes this system is either little- or big-endian and ignore
        // anything else.
        public static readonly DataConverter LittleEndian;
        public static readonly DataConverter BigEndian;
        public static readonly DataConverter Converter;

        static DataConverter()
        {
            if(BitConverter.IsLittleEndian)
            {
                LittleEndian = new CopyConverter();
                BigEndian = new SwapConverter();
            } else {
                LittleEndian = new SwapConverter();
                BigEndian = new CopyConverter();
            }
            Converter = LittleEndian;
        }

        public abstract byte[] GetBytes(double value);
        public abstract byte[] GetBytes(float value);
        public abstract byte[] GetBytes(ulong value);
        public abstract byte[] GetBytes(long value);
        public abstract byte[] GetBytes(uint value);
        public abstract byte[] GetBytes(int value);
        public abstract byte[] GetBytes(ushort value);
        public abstract byte[] GetBytes(short value);
        public abstract byte[] GetBytes(char value);
        public abstract byte[] GetBytes(bool value);

        public abstract double ToDouble(byte[] bytes, int offset);
        public abstract float ToSingle(byte[] bytes, int offset);
        public abstract ulong ToUInt64(byte[] bytes, int offset);
        public abstract long ToInt64(byte[] bytes, int offset);
        public abstract uint ToUInt32(byte[] bytes, int offset);
        public abstract int ToInt32(byte[] bytes, int offset);
        public abstract ushort ToUInt16(byte[] bytes, int offset);
        public abstract short ToInt16(byte[] bytes, int offset);
        public abstract char ToChar(byte[] bytes, int offset);
        public abstract bool ToBoolean(byte[] bytes, int offset);

        /// <summary>
        /// The CopyConverter simply defers to <see cref="BitConverter"/>.
        /// </summary>
        sealed class CopyConverter : DataConverter {
            public override byte[] GetBytes(double value)
            {
                return BitConverter.GetBytes(value);
            }

            public override byte[] GetBytes(float value)
            {
                return BitConverter.GetBytes(value);
            }

            public override byte[] GetBytes(ulong value)
            {
                return BitConverter.GetBytes(value);
            }

            public override byte[] GetBytes(long value)
            {
                return BitConverter.GetBytes(value);
            }

            public override byte[] GetBytes(uint value)
            {
                return BitConverter.GetBytes(value);
            }

            public override byte[] GetBytes(int value)
            {
                return BitConverter.GetBytes(value);
            }

            public override byte[] GetBytes(ushort value)
            {
                return BitConverter.GetBytes(value);
            }

            public override byte[] GetBytes(short value)
            {
                return BitConverter.GetBytes(value);
            }

            public override byte[] GetBytes(char value)
            {
                return BitConverter.GetBytes(value);
            }

            public override byte[] GetBytes(bool value)
            {
                return BitConverter.GetBytes(value);
            }

            public override double ToDouble(byte[] bytes, int offset)
            {
                return BitConverter.ToDouble(bytes, offset);
            }

            public override float ToSingle(byte[] bytes, int offset)
            {
                return BitConverter.ToSingle(bytes, offset);
            }

            public override ulong ToUInt64(byte[] bytes, int offset)
            {
                return BitConverter.ToUInt64(bytes, offset);
            }

            public override long ToInt64(byte[] bytes, int offset)
            {
                return BitConverter.ToInt64(bytes, offset);
            }

            public override uint ToUInt32(byte[] bytes, int offset)
            {
                return BitConverter.ToUInt32(bytes, offset);
            }

            public override int ToInt32(byte[] bytes, int offset)
            {
                return BitConverter.ToInt32(bytes, offset);
            }

            public override ushort ToUInt16(byte[] bytes, int offset)
            {
                return BitConverter.ToUInt16(bytes, offset);
            }

            public override short ToInt16(byte[] bytes, int offset)
            {
                return BitConverter.ToInt16(bytes, offset);
            }

            public override char ToChar(byte[] bytes, int offset)
            {
                return BitConverter.ToChar(bytes, offset);
            }

            public override bool ToBoolean(byte[] bytes, int offset)
            {
                return BitConverter.ToBoolean(bytes, offset);
            }
        }

        sealed class SwapConverter : DataConverter {
            private byte[] Reverse(byte[] array)
            {
                Array.Reverse(array);
                return array;
            }

            private byte[] ReverseCopy(byte[] array)
            {
                array = (byte[])array.Clone();
                Array.Reverse(array);
                return array;
            }

            public override byte[] GetBytes(double value)
            {
                return Reverse(BitConverter.GetBytes(value));
            }

            public override byte[] GetBytes(float value)
            {
                return Reverse(BitConverter.GetBytes(value));
            }

            public override byte[] GetBytes(ulong value)
            {
                return Reverse(BitConverter.GetBytes(value));
            }

            public override byte[] GetBytes(long value)
            {
                return Reverse(BitConverter.GetBytes(value));
            }

            public override byte[] GetBytes(uint value)
            {
                return Reverse(BitConverter.GetBytes(value));
            }

            public override byte[] GetBytes(int value)
            {
                return Reverse(BitConverter.GetBytes(value));
            }

            public override byte[] GetBytes(ushort value)
            {
                return Reverse(BitConverter.GetBytes(value));
            }

            public override byte[] GetBytes(short value)
            {
                return Reverse(BitConverter.GetBytes(value));
            }

            public override byte[] GetBytes(char value)
            {
                return Reverse(BitConverter.GetBytes(value));
            }

            public override byte[] GetBytes(bool value)
            {
                return Reverse(BitConverter.GetBytes(value));
            }

            public override double ToDouble(byte[] bytes, int offset)
            {
                return BitConverter.ToDouble(ReverseCopy(bytes), offset);
            }

            public override float ToSingle(byte[] bytes, int offset)
            {
                return BitConverter.ToSingle(ReverseCopy(bytes), offset);
            }

            public override ulong ToUInt64(byte[] bytes, int offset)
            {
                return BitConverter.ToUInt64(ReverseCopy(bytes), offset);
            }

            public override long ToInt64(byte[] bytes, int offset)
            {
                return BitConverter.ToInt64(ReverseCopy(bytes), offset);
            }

            public override uint ToUInt32(byte[] bytes, int offset)
            {
                return BitConverter.ToUInt32(ReverseCopy(bytes), offset);
            }

            public override int ToInt32(byte[] bytes, int offset)
            {
                return BitConverter.ToInt32(ReverseCopy(bytes), offset);
            }

            public override ushort ToUInt16(byte[] bytes, int offset)
            {
                return BitConverter.ToUInt16(ReverseCopy(bytes), offset);
            }

            public override short ToInt16(byte[] bytes, int offset)
            {
                return BitConverter.ToInt16(ReverseCopy(bytes), offset);
            }

            public override char ToChar(byte[] bytes, int offset)
            {
                return BitConverter.ToChar(ReverseCopy(bytes), offset);
            }

            public override bool ToBoolean(byte[] bytes, int offset)
            {
                return BitConverter.ToBoolean(ReverseCopy(bytes), offset);
            }
        }

    }
}