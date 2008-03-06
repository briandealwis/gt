using System;
using System.IO;

namespace GT.Common
{
    /// <remarks>
    /// A stream with a fixed capacity.  Useful for debugging.
    /// </remarks>
    public class WrappedStream : Stream
    {
        protected Stream wrapped;
        protected long startPosition;
        protected long capacity;

        public WrappedStream(Stream s, uint cap)
        {
            wrapped = s;
            startPosition = s.Position;
            capacity = cap;
        }

        public override bool CanRead
        {
            get { return wrapped.CanRead; }
        }

        public override bool CanSeek
        {
            get { return wrapped.CanSeek; }
        }

        public override bool CanWrite
        {
            get { return wrapped.CanWrite; }
        }

        public override void Flush()
        {
            wrapped.Flush();
        }

        public override long Length
        {
            get { return capacity; }
        }

        public override long Position
        {
            get { return wrapped.Position - startPosition; }
            set
            {
                if (value < 0 || value > capacity)
                {
                    throw new ArgumentException("position exceeds stream capacity");
                }
                wrapped.Position = startPosition + value;
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (offset < 0 || count < 0) { throw new ArgumentOutOfRangeException(); }
            int actual = Math.Max(0, Math.Min(count, (int)(capacity - Position)));
            if (actual == 0) { return 0; }
            return wrapped.Read(buffer, offset, actual);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            long newPosition = 0;
            switch (origin)
            {
            case SeekOrigin.Begin:
                newPosition = offset;
                break;
            case SeekOrigin.Current:
                newPosition = Position + offset;
                break;
            case SeekOrigin.End:
                newPosition = capacity + offset;
                break;
            }
            if (newPosition < 0 || newPosition > capacity)
            {
                throw new ArgumentException("position exceeds stream capacity");
            }
            Position = newPosition;
            return newPosition;
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException("cannot resize wrapped streams");
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (offset < 0 || count < 0) { throw new ArgumentOutOfRangeException(); }
            if (Position + count > capacity)
            {
                throw new ArgumentException("exceeds stream capacity");
            }
            wrapped.Write(buffer, offset, count);
        }
    }
}