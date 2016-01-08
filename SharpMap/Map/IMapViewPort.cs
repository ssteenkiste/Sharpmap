using GeoAPI.Geometries;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SharpMap
{
    public interface IMapViewPort
    {
        double MapScale
        { get; set; }

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
    }
}
