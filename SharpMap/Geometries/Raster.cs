using GeoAPI.Geometries;
using System;
using System.IO;

namespace SharpMap.Geometries
{
    public class Raster : IRaster
    {
        readonly Envelope _boundingBox;
        public MemoryStream Data { get; private set; }
        public long TickFetched { get; private set; }

        public Raster(MemoryStream data, Envelope box)
        {
            Data = data;
            _boundingBox = box;
            TickFetched = DateTime.Now.Ticks;
        }

        public Envelope GetBoundingBox()
        {
            return _boundingBox;
        }

    }
}
