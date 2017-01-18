using System;
using System.Collections.Generic;
using Common.Logging;
using SharpMap.Layers;

namespace SharpMap.Rendering
{

    /// <summary>
    /// Base class for map renderer.
    /// </summary>
    public abstract class MapRendererBase : IMapRenderer
    {
        private static readonly ILog Logger = LogManager.GetLogger<MapRendererBase>();

        /// <summary>
        /// Initialize a new instance of <see cref="MapRendererBase"/>.
        /// </summary>
        protected MapRendererBase()
        {
        }

        /// <summary>
        /// Initialize a new instance of <see cref="MapRendererBase"/>.
        /// </summary>
        /// <param name="map">the map to render.</param>
        protected MapRendererBase(Map map)
        {
            Map = map;
        }

        /// <summary>
        /// Gets the map.
        /// </summary>
        public Map Map { get; set; }

        #region Events

        /// <summary>
        /// Event fired when all layers are about to be rendered
        /// </summary>
        public event EventHandler<MapRenderingEventArgs> MapRendering;

        /// <summary>
        /// Event fired when all layers have been rendered
        /// </summary>
        public event EventHandler<MapRenderingEventArgs> MapRendered;

        /// <summary>
        /// Event fired when one layer have been rendered
        /// </summary>
        public event EventHandler<LayerRenderingEventArgs> LayerRendering;

        /// <summary>
        /// Event fired when one layer have been rendered
        /// </summary>
        public event EventHandler<LayerRenderingEventArgs> LayerRendered;

        /// <summary>
        /// Fired when map is rendering
        /// </summary>
        /// <param name="g"></param>
        protected virtual void OnMapRendering()
        {
            Logger.Debug("OnMapRendering");
            MapRendering?.Invoke(this, new MapRenderingEventArgs());
        }

        /// <summary>
        /// Fired when Map is rendered
        /// </summary>
        /// <param name="g"></param>
        protected virtual void OnMapRendered()
        {
            Logger.Debug("OnMapRendered");
            MapRendered?.Invoke(this, new MapRenderingEventArgs()); //Fire render event
        }

        /// <summary>
        /// Method called when starting to render <paramref name="layer"/>. This fires the
        /// <see cref="LayerRendering"/> event.
        /// </summary>
        /// <param name="layer">The layer to render</param>
        protected virtual void OnLayerRendering(ILayer layer)
        {
            Logger.Debug("OnLayerRendering");
            LayerRendering?.Invoke(this, new LayerRenderingEventArgs(layer));
        }

        /// <summary>
        /// Method called when <paramref name="layer"/> has been rendered. This fires the
        /// <see cref="LayerRendered"/> and <see cref="E:SharpMap.Map.LayerRenderedEx"/> event.
        /// </summary>
        /// <param name="layer">The layer to render</param>
        protected virtual void OnLayerRendered(ILayer layer)
        {
            Logger.Debug("OnLayerRendered");
            LayerRendered?.Invoke(this, new LayerRenderingEventArgs(layer));
        }




        #endregion


        /// <summary>
        /// Renders the map.
        /// </summary>
        /// <param name="target"></param>
        /// <param name="map"></param>
        public virtual void Render(object target, IMapViewPort map)
        {
            throw new NotImplementedException();
        }
    }
}
