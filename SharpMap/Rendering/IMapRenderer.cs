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
        event EventHandler<LayerRenderingEventArgs> LayerRenderedEx;

        ///<summary>
        /// Event fired when a layer has been rendered
        ///</summary>
        [Obsolete("Use LayerRenderedEx")]
        event EventHandler LayerRendered;


        /// <summary>
        /// Renders the map.
        /// </summary>
        /// <param name="map">The map to render.</param>
        void RenderMap(Map map);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="map"></param>
        /// <param name="layerCollectionType"></param>
        void Render(Map map, LayerCollectionType layerCollectionType);

        /// <summary>
        /// Renders map layers.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="layers">The layers to render.</param>
        void Render(Map map, IEnumerable<ILayer> layers);

    }
}