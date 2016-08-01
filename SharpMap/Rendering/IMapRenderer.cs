using System;
using System.Collections.Generic;
using SharpMap.Layers;

namespace SharpMap.Rendering
{
    /// <summary>
    /// Interface for map renderer.
    /// </summary>
    public interface IMapRenderer
    {

        /// <summary>
        /// Event fired when all layers are about to be rendered
        /// </summary>
        event EventHandler<MapRenderingEventArgs> MapRendering;

        /// <summary>
        /// Event fired when all layers have been rendered
        /// </summary>
        event EventHandler<MapRenderingEventArgs> MapRendered;

        /// <summary>
        /// Event fired when one layer have been rendered
        /// </summary>
        event EventHandler<LayerRenderingEventArgs> LayerRendering;

        /// <summary>
        /// Event fired when one layer have been rendered
        /// </summary>
        event EventHandler<LayerRenderingEventArgs> LayerRendered;

        

        /// <summary>
        /// Renders map layers.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="layers">The layers to render.</param>
        void Render(IMapViewPort map, IEnumerable<ILayer> layers);

    }
}