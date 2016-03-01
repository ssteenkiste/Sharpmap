using GeoAPI.Geometries;
using System.Drawing;

namespace SharpMap
{
    public interface IMapViewPort
    {
        double MapScale
        { get; set; }

        Size Size
        {
            get;
        }

        /// <summary>
        /// Height of map in world units
        /// </summary>
        /// <returns></returns>
        double MapHeight
        {
            get;
        }

        double PixelWidth
        { get; }
        double PixelSize
        { get; }
        double PixelHeight
        { get; }
        double PixelAspectRatio
        {
            get;
        }

        double Zoom
        { get; set; }

        double MinimumZoom
        {
            get;
            set;
        }

        double MaximumZoom
        {
            get;
            set;
        }

        /// <summary>
        /// Center of map in WCS
        /// </summary>
        Coordinate Center
        {
            get;
        }

        /// <summary>
        /// Gets the extend;
        /// </summary>
        /// <returns></returns>
        Envelope GetExtents();

        /// <summary>
        /// Gets the extents of the current map based on the current zoom, center and mapsize
        /// </summary>
        Envelope Envelope
        {
            get;
        }

        Envelope MaximumExtents
        { get; set; }

        /// <summary>
        /// Gets or sets a value indicating if <see cref="MaximumExtents"/> should be enforced or not.
        /// </summary>
        bool EnforceMaximumExtents
        {
            get;
            set;
        }

        #region Transformations

        PointF WorldToImage(Coordinate p);

        Coordinate ImageToWorld(PointF p);

        Coordinate ImageToWorld(PointF p, bool careAboutMapTransform);

        PointF WorldToImage(Coordinate p, bool careAboutMapTransform);

        #endregion
    }
}
