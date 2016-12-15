using System;
using System.Drawing;
using GeoAPI.Geometries;

namespace SharpMap
{
    /// <summary>
    /// The map view port.
    /// </summary>
    public class MapViewPort : IMapViewPort
    {

        /// <summary>
        /// Gets or sets the viewport center.
        /// </summary>
        public Coordinate Center
        {
            get;set;
        }

        public bool EnforceMaximumExtents
        {
            get;set;
            
        }

        public Envelope Envelope
        {
            get;set;
        }

        public double MapHeight
        {
            get;set;
           
        }

        public double MapScale
        {
            get;set;
        }

        public Envelope MaximumExtents
        {
            get;set;
        }

        public double MaximumZoom
        {
            get;set;
        }

        public double MinimumZoom
        {
            get;set;
        }

        public double PixelAspectRatio
        {
            get;set;
        }

        public double PixelHeight
        {
            get;set;
        }

        public double PixelSize
        {
            get;set;
        }

        public double PixelWidth
        {
            get;set;
        }

        public Size Size
        {
            get;set;
        }

        public double Zoom
        {
            get;set;
        }

        public void ChangeView(Coordinate center, double zoom)
        {
            throw new NotImplementedException();
        }

        public Envelope GetExtents()
        {
            throw new NotImplementedException();
        }

        public Point ImageToWorld(PointF p)
        {
            throw new NotImplementedException();
        }

        public Point ImageToWorld(PointF p, bool careAboutMapTransform)
        {
            throw new NotImplementedException();
        }

        public PointF WorldToImage(Coordinate p)
        {
            throw new NotImplementedException();
        }

        public PointF WorldToImage(Coordinate p, bool careAboutMapTransform)
        {
            throw new NotImplementedException();
        }

        Coordinate IMapViewPort.ImageToWorld(PointF p)
        {
            throw new NotImplementedException();
        }

        Coordinate IMapViewPort.ImageToWorld(PointF p, bool careAboutMapTransform)
        {
            throw new NotImplementedException();
        }
    }
}
