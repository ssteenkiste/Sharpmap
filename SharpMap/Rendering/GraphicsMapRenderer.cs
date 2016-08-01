using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpMap.Layers;
using System.Drawing;
using Common.Logging;

namespace SharpMap.Rendering
{

    /// <summary>
    /// A map renderer based on gdi graphics.
    /// </summary>
    public class GraphicsMapRenderer : IMapRenderer
    {
        private static readonly ILog Logger = LogManager.GetLogger<GraphicsMapRenderer>();

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
        protected virtual void OnMapRendering(Graphics g)
        {
            MapRendering?.Invoke(this,new MapRenderingEventArgs());
        }

        /// <summary>
        /// Fired when Map is rendered
        /// </summary>
        /// <param name="g"></param>
        protected virtual void OnMapRendered(Graphics g)
        {
            MapRendered?.Invoke(this, new MapRenderingEventArgs()); //Fire render event
        }

        /// <summary>
        /// Method called when starting to render <paramref name="layer"/> of <paramref name="layerCollectionType"/>. This fires the
        /// <see cref="E:SharpMap.Map.LayerRendering"/> event.
        /// </summary>
        /// <param name="layer">The layer to render</param>
        /// <param name="layerCollectionType">The collection type</param>
        protected virtual void OnLayerRendering(ILayer layer, LayerCollectionType layerCollectionType)
        {
            LayerRendering?.Invoke(this, new LayerRenderingEventArgs(layer, layerCollectionType));
        }

        /// <summary>
        /// Method called when <paramref name="layer"/> of <paramref name="layerCollectionType"/> has been rendered. This fires the
        /// <see cref="E:SharpMap.Map.LayerRendered"/> and <see cref="E:SharpMap.Map.LayerRenderedEx"/> event.
        /// </summary>
        /// <param name="layer">The layer to render</param>
        /// <param name="layerCollectionType">The collection type</param>
        protected virtual void OnLayerRendered(ILayer layer, LayerCollectionType layerCollectionType)
        {
            LayerRendered?.Invoke(this, new LayerRenderingEventArgs(layer, layerCollectionType));
        }


        #endregion

        private readonly Graphics _graphics;

        /// <summary>
        /// Initialize a new instance of <see cref="GraphicsMapRenderer"/>.
        /// </summary>
        /// <param name="graphics">The graphic surface.</param>
        public GraphicsMapRenderer(Graphics graphics)
        {
            if (graphics == null)
                throw new ArgumentNullException(nameof(graphics));

            _graphics = graphics;
        }


        /// <summary>
        /// Renders the map layers.
        /// </summary>
        /// <param name="map"></param>
        /// <param name="layers"></param>
        public void Render(IMapViewPort map, IEnumerable<ILayer> layers, LayerCollectionType type)
        {
            if (layers == null)
                throw new InvalidOperationException("No layers to render");

            var layerList = layers.ToList();
            foreach (var layer in layerList)
            {
                OnLayerRendering(layer, type);

                if (layer.IsLayerVisible(map))
                {
                    try
                    {
                        layer.Render(_graphics, map);
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e.Message, e);

                        using (var pen = new Pen(Color.Red, 4f))
                        {
                            var size = map.Size;

                            _graphics.DrawLine(pen, 0, 0, size.Width, size.Height);
                            _graphics.DrawLine(pen, size.Width, 0, 0, size.Height);
                            _graphics.DrawRectangle(pen, 0, 0, size.Width, size.Height);
                        }

                    }
                }
                else
                {
                    layer.CleanupRendering();
                }

                OnLayerRendered(layer, type);
            }
        }

        public void Render(IMapViewPort map, IEnumerable<ILayer> layers)
        {
            throw new NotImplementedException();
        }
    }
}
