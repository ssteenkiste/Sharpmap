using System;
using System.Drawing;
using SharpMap.Layers;
using Common.Logging;

namespace SharpMap.Rendering
{
    /// <summary>
    /// A layer collection renderer
    /// </summary>
    public class LayerCollectionRenderer
    {

        private static readonly ILog Logger = LogManager.GetLogger<LayerCollectionRenderer>();
        
        /// <summary>
        /// Invokes the rendering of the layer, a red X is drawn if it fails.
        /// </summary>
        /// <param name="layer"></param>
        /// <param name="g"></param>
        /// <param name="map"></param>
        public static void RenderLayer(ILayer layer, Graphics g, IMapViewPort map)
        {
            try
            {
                layer.Render(g, map);
            }
            catch (Exception e)
            {
                Logger.Error(e.Message, e);

                using (var pen = new Pen(Color.Red, 4f))
                {
                    var size = map.Size;

                    g.DrawLine(pen, 0, 0, size.Width, size.Height);
                    g.DrawLine(pen, size.Width, 0, 0, size.Height);
                    g.DrawRectangle(pen, 0, 0, size.Width, size.Height);
                }

            }
        }
       
    }
}