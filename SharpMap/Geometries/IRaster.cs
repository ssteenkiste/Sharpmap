using GeoAPI.Geometries;
using System.IO;

namespace SharpMap.Geometries
{

    /// <summary>
    /// Interface for raster data.
    /// </summary>
    public interface IRaster 
    {
        /// <summary>
        /// Gets the raster data.
        /// </summary>
        MemoryStream Data { get; }

        /// <summary>
        /// Gets the bounding box.
        /// </summary>
        /// <returns></returns>
        Envelope GetBoundingBox();

        /// <summary>
        /// Gets the tiel fetching ticks.
        /// </summary>
        long TickFetched { get; }
    }
}
