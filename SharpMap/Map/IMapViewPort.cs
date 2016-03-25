using GeoAPI.Geometries;
using System.Drawing;

namespace SharpMap
{

    /// <summary>
    /// The map view port interface
    /// </summary>
    public interface IMapViewPort
    {

        /// <summary>
        /// Gets or sets the map scale.
        /// </summary>
        double MapScale { get; set; }

        /// <summary>
        /// Gets the map size.
        /// </summary>
        Size Size { get; }

        /// <summary>
        /// Height of map in world units
        /// </summary>
        /// <returns></returns>
        double MapHeight { get; }

        double PixelWidth { get; }
        double PixelSize { get; }
        double PixelHeight { get; }
        double PixelAspectRatio { get; }

        /// <summary>
        /// Gets or sets the zoom.
        /// </summary>
        double Zoom { get; set; }

        /// <summary>
        /// Gets the minimum zoom value.
        /// </summary>
        double MinimumZoom { get; set; }

        /// <summary>
        /// Gets the maximum zoom value.
        /// </summary>
        double MaximumZoom { get; set; }

        /// <summary>
        /// Center of map in WCS
        /// </summary>
        Coordinate Center { get; }

        /// <summary>
        /// Changes the view.
        /// </summary>
        /// <param name="center"></param>
        /// <param name="zoom"></param>
        void ChangeView( Coordinate center, double zoom);

        /// <summary>
        /// Gets the extend;
        /// </summary>
        /// <returns></returns>
        Envelope GetExtents();

        /// <summary>
        /// Gets the extents of the current map based on the current zoom, center and mapsize
        /// </summary>
        Envelope Envelope { get; }

        /// <summary>
        /// Gets or sets the maximum extents.
        /// </summary>
        Envelope MaximumExtents { get; set; }

        /// <summary>
        /// Gets or sets a value indicating if <see cref="MaximumExtents"/> should be enforced or not.
        /// </summary>
        bool EnforceMaximumExtents { get; set; }

        #region Transformations

        PointF WorldToImage(Coordinate p);

        Coordinate ImageToWorld(PointF p);

        Coordinate ImageToWorld(PointF p, bool careAboutMapTransform);

        PointF WorldToImage(Coordinate p, bool careAboutMapTransform);

        #endregion
    }
}
