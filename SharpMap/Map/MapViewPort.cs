using System;
using System.Drawing;
using GeoAPI.Geometries;

namespace SharpMap
{

    public class MapViewPort : IMapViewPort
    {
        public Coordinate Center
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public bool EnforceMaximumExtents
        {
            get
            {
                throw new NotImplementedException();
            }

            set
            {
                throw new NotImplementedException();
            }
        }

        public Envelope Envelope
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public double MapHeight
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public double MapScale
        {
            get
            {
                throw new NotImplementedException();
            }

            set
            {
                throw new NotImplementedException();
            }
        }

        public Envelope MaximumExtents
        {
            get
            {
                throw new NotImplementedException();
            }

            set
            {
                throw new NotImplementedException();
            }
        }

        public double MaximumZoom
        {
            get
            {
                throw new NotImplementedException();
            }

            set
            {
                throw new NotImplementedException();
            }
        }

        public double MinimumZoom
        {
            get
            {
                throw new NotImplementedException();
            }

            set
            {
                throw new NotImplementedException();
            }
        }

        public double PixelAspectRatio
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public double PixelHeight
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public double PixelSize
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public double PixelWidth
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public Size Size
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public double Zoom
        {
            get
            {
                throw new NotImplementedException();
            }

            set
            {
                throw new NotImplementedException();
            }
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
