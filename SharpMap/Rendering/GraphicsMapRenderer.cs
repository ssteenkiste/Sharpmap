using System;
using System.Collections.Generic;
using System.Linq;
using SharpMap.Layers;
using System.Drawing;
using Common.Logging;

namespace SharpMap.Rendering
{

    /// <summary>
    /// A map renderer based on gdi graphics.
    /// </summary>
    public class GraphicsMapRenderer : MapRendererBase
    {
        private static readonly ILog Logger = LogManager.GetLogger<GraphicsMapRenderer>();

        /// <summary>
        /// Initialize a new instance of <see cref="GraphicsMapRenderer"/>.
        /// </summary>
        public GraphicsMapRenderer()
        {
        }

        /// <summary>
        /// Initialize a new instance of <see cref="GraphicsMapRenderer"/>.
        /// </summary>
        /// <param name="map">The map to render.</param>
        public GraphicsMapRenderer(Map map)
            : base(map)
        {
        }

        /// <summary>
        /// Renders the map.
        /// </summary>
        /// <param name="context">The rendering context.</param>
        /// <param name="map"></param>
        public override void Render(object context, IMapViewPort map)
        {
            var g = context as Graphics;

            if (g == null)
                throw new ArgumentNullException(nameof(g), "Cannot render map with null graphics object!");

            if (Map == null)
            {
                throw new InvalidOperationException("Map cannot be null.");
            }

            OnMapRendering();

            if (!map.Size.IsEmpty)
            {
                g.Transform = Map.MapTransform;
                g.PageUnit = GraphicsUnit.Pixel;

                g.Clear(Map.BackColor);

                //BackgroundLayer
                if (Map.BackgroundLayer != null)
                {
                    Render(context, map, Map.BackgroundLayer);
                }

                //Masque
                if (Map.BackgroundMaskBrush != null)
                {
                    g.FillRectangle(Map.BackgroundMaskBrush, g.ClipBounds);
                }

                //Layers
                if (Map.Layers != null)
                {
                    Render(context, map, Map.Layers);
                }

                //Décorations
                if (Map.Decorations != null)
                {
                    // Render all map decorations
                    foreach (var mapDecoration in Map.Decorations)
                    {
                        mapDecoration.Render(g, map);
                    }
                }
            }
            OnMapRendered();
        }

        /// <summary>
        /// Renders the map layers.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="map"></param>
        /// <param name="layers"></param>
        private void Render(object context, IMapViewPort map, IEnumerable<ILayer> layers)
        {
            if (layers == null)
                throw new InvalidOperationException("No layers to render");

            var graphics = context as Graphics;

            if (graphics == null)
                throw new InvalidOperationException("Invalid target.");

            var layerList = layers.ToList();
            foreach (var layer in layerList)
            {
                OnLayerRendering(layer);

                if (layer.IsLayerVisible(map))
                {
                    try
                    {
                        layer.Render(graphics, map);
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e.Message, e);

                        using (var pen = new Pen(Color.Red, 4f))
                        {
                            var size = map.Size;

                            graphics.DrawLine(pen, 0, 0, size.Width, size.Height);
                            graphics.DrawLine(pen, size.Width, 0, 0, size.Height);
                            graphics.DrawRectangle(pen, 0, 0, size.Width, size.Height);
                        }

                    }
                }
                else
                {
                    layer.CleanupRendering();
                }

                OnLayerRendered(layer);
            }
        }
    }
}
