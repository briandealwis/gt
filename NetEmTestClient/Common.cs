using System;

namespace BBall.Common
{
    [Serializable]
    public class WindowBoundsChanged
    {
        public uint Height { get; set; }
        public uint Width { get; set; }
        public uint Radius { get; set; }
    }

    [Serializable]
    public class PositionChanged
    {
        public double X { get; set; }
        public double Y { get; set; }
    }

}