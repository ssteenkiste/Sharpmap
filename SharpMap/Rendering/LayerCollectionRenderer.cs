using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using SharpMap.Layers;
using SharpMap.Styles;
using Common.Logging;

namespace SharpMap.Rendering
{
    /// <summary>
    /// A layer collection renderer
    /// </summary>
    public class LayerCollectionRenderer : IDisposable
    {

        private static readonly ILog Logger = LogManager.GetLogger<LayerCollectionRenderer>();
        private readonly ILayer[] _layers;
        private Map _map;

        private Image[] _images;
        private static Func<Size, float, int, bool> _parallelHeuristic;
        private Matrix _transform;

        /// <summary>
        /// Creates an instance of this class
        /// </summary>
        /// <param name="layers">The layer collection to render</param>
        /// <param name="transform"></param>
        public LayerCollectionRenderer(ICollection<ILayer> layers, Matrix transform)
        {
            _layers = new ILayer[layers.Count];
            layers.CopyTo(_layers, 0);
            _transform = transform;
        }

        /// <summary>
        /// Method to render the layer collection
        /// </summary>
        /// <param name="g">The graphics object</param>
        /// <param name="map">The map</param>
        /// <param name="allowParallel"></param>
        //[MethodImpl(MethodImplOptions.Synchronized)]
        public void Render(Graphics g, Map map, bool allowParallel)
        {
            _map = map;

            g.PageUnit = GraphicsUnit.Pixel;

            if (AllowParallel && allowParallel && ParallelHeuristic(map.Size, g.DpiX, _layers.Length))
            {
                RenderParellel(g);
            }
            else
            {
                RenderSequential(g);
            }
        }

        /// <summary>
        /// Method to determine the synchronization method
        /// </summary>
        private static Func<Size, float, int, bool> ParallelHeuristic
        {
            get { return _parallelHeuristic ?? StdHeuristic; }
            set { _parallelHeuristic = value; }
        }

        /// <summary>
        /// Method to enable parallel rendering of layers
        /// </summary>
        public static bool AllowParallel { get; set; }

        /// <summary>
        /// Standard implementation for <see cref="ParallelHeuristic"/>
        /// </summary>
        /// <param name="size">The size of the map</param>
        /// <param name="dpi">The dots per inch</param>
        /// <param name="numLayers">The number of layers</param>
        /// <returns><c>true</c> if the map's width and height are less or equal 1920 and the collection has less than 10 entries</returns>
        private static bool StdHeuristic(Size size, float dpi, int numLayers)
        {
            return (size.Width < 1920 && size.Height <= 1920 && numLayers < 100);
        }

        private void RenderSequential(Graphics g)
        {
            foreach (var layer in _layers)
            {
                if (layer.IsLayerVisible(_map))
                {
                    RenderLayer(layer, g, _map);
                }
            }
        }

        private void RenderParellel(Graphics g)
        {
            _images = new Image[_layers.Length];

            var res = Parallel.For(0, _layers.Length, RenderToImage);

            var tmpTransform = g.Transform;
            //g.Transform = new Matrix();
            ///ApplyTransform(_transform, g);
           // g.Transform = _transform;
            if (res.IsCompleted)
            {
                foreach (var img in _images.Where(img => img != null))
                {
                    //ApplyTransform(_transform, g);
                    g.DrawImageUnscaled(img, 0, 0);
                }
            }
            g.Transform = tmpTransform;
        }

        private void RenderToImage(int layerIndex, ParallelLoopState pls)
        {
            if (pls.ShouldExitCurrentIteration)
                return;

            var layer = _layers[layerIndex];

            if (layer != null && layer.IsLayerVisible(_map))
            {
                var image = _images[layerIndex] = new Bitmap(_map.Size.Width, _map.Size.Height, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(image))
                {
                    g.PageUnit = GraphicsUnit.Pixel;
                    //ApplyTransform(_transform, g);

                    g.Clear(Color.Transparent);
                    RenderLayer(layer, g, _map);
                }

            }
        }

        /// <summary>
        /// Invokes the rendering of the layer, a red X is drawn if it fails.
        /// </summary>
        /// <param name="layer"></param>
        /// <param name="g"></param>
        /// <param name="map"></param>
        public static void RenderLayer(ILayer layer, Graphics g, Map map)
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



        [MethodImpl(MethodImplOptions.Synchronized)]
        private static void ApplyTransform(Matrix transform, Graphics g)
        {
            g.Transform = transform.Clone();
        }

        /// <summary>
        /// Clean up resources.
        /// </summary>
        public void Dispose()
        {
            if (_images != null)
            {
                foreach (var image in _images.Where(image => image != null))
                {
                    image.Dispose();
                }
            }
        }
    }
}