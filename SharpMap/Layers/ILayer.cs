// Copyright 2005, 2006 - Morten Nielsen (www.iter.dk)
//
// This file is part of SharpMap.
// SharpMap is free software; you can redistribute it and/or modify
// it under the terms of the GNU Lesser General Public License as published by
// the Free Software Foundation; either version 2 of the License, or
// (at your option) any later version.
// 
// SharpMap is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Lesser General Public License for more details.

// You should have received a copy of the GNU Lesser General Public License
// along with SharpMap; if not, write to the Free Software
// Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA 

using System.Drawing;
using GeoAPI.Geometries;
using SharpMap.Styles;

namespace SharpMap.Layers
{
    /// <summary>
    /// Interface for map layers
    /// </summary>
    public interface ILayer
    {
        /// <summary>
        /// Minimum visible zoom level
        /// </summary>
        double MinVisible { get; set; }

        /// <summary>
        /// Minimum visible zoom level
        /// </summary>
        double MaxVisible { get; set; }

        /// <summary>
        /// Gets or Sets what level-reference the Min/Max values are defined in
        /// </summary>
        VisibilityUnits VisibilityUnits { get; set; }

        /// <summary>
        /// Specifies whether this layer should be rendered or not
        /// </summary>
        bool Enabled { get; set; }

        /// <summary>
        /// Name of layer
        /// </summary>
        string LayerName { get; set; }

        /// <summary>
        /// Gets the boundingbox of the entire layer
        /// </summary>
        Envelope Envelope { get; }

        /// <summary>
        /// The spatial reference ID (CRS)
        /// </summary>
        int SRID { get; set; }

        /// <summary>
        /// The spatial reference ID (CRS) that can be exposed externally.
        /// </summary>
        int TargetSRID { get; }

        /// <summary>
        /// Proj4 String Projection
        /// </summary>
        string Proj4Projection { get; set; }

        /// <summary>
        /// Renders the layer
        /// </summary>
        /// <param name="g">Graphics object reference</param>
        /// <param name="map">Map which is rendered</param>
        void Render(Graphics g, IMapViewPort map);

        /// <summary>
        /// Cleanup rendering elements.
        /// </summary>
        void CleanupRendering();

    }

    /// <summary>
    /// Ilayer extensions.
    /// </summary>
    public static class LayerExtensions
    {
        /// <summary>
        /// Check if layer is visible.
        /// </summary>
        /// <param name="layer"></param>        
        /// <param name="view"></param>
        /// <returns></returns>
        public static bool IsLayerVisible(this ILayer layer, IMapViewPort view)
        {
            if (layer == null || view == null)
            {
                return false;
            }

            if (!layer.Enabled)
            {
                return false;
            }

            var compare = layer.VisibilityUnits == VisibilityUnits.ZoomLevel ? view.Zoom : view.MapScale;

            if (compare < layer.MinVisible && !double.IsNaN(layer.MinVisible) || compare > layer.MaxVisible && !double.IsNaN(layer.MaxVisible))
            {
                return false;
            }
            
            //var envelope = layer.Envelope;
            //if (envelope == null)
            //    return false;

            //// Does the layer intersect the viewport at all
            //if (!envelope.Intersects(view.Envelope))
            //    return false;

            return true;
        }
    }
}