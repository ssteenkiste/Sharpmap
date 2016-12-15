using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using BruTile;
using BruTile.Cache;
using Common.Logging;
using GeoAPI.Geometries;
using SharpMap.Fetching;
using System.ComponentModel;

namespace SharpMap.Layers
{
    //ReSharper disable InconsistentNaming
    ///<summary>
    /// Tile layer class
    ///</summary>
    [Serializable]
    public class TileLayer : Layer, System.Runtime.Serialization.IDeserializationCallback
    {
        private static readonly ILog Logger = LogManager.GetCurrentClassLogger();
       
        #region Fields

        /// <summary>
        /// The tile source for this layer
        /// </summary>
        protected ITileSource _source;

        /// <summary>
        /// An in-memory tile cache
        /// </summary>
        [NonSerialized]
        protected MemoryCache<Stream> _bitmaps = new MemoryCache<Stream>(200, 300);

        /// <summary>
        /// A file cache
        /// </summary>
        protected FileCache _fileCache = null;

        /// <summary>
        /// The format of the images
        /// </summary>
        protected readonly ImageFormat _ImageFormat = null;

        /// <summary>
        /// Value indicating if "error" tiles should be generated or not.
        /// </summary>
        protected readonly bool _showErrorInTile = true;

        InterpolationMode _interpolationMode = InterpolationMode.HighQualityBicubic;

        /// <summary>
        /// The transparent color.
        /// </summary>
        protected Color _transparentColor;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the boundingbox of the entire layer
        /// </summary>
        public override Envelope Envelope
        {
            get
            {
                var extent = _source.Schema.Extent;
                return new Envelope(
                    extent.MinX, extent.MaxX,
                    extent.MinY, extent.MaxY);
            }
        }

        /// <summary>
        /// The algorithm used when images are scaled or rotated 
        /// </summary>
        public InterpolationMode InterpolationMode
        {
            get { return _interpolationMode; }
            set { _interpolationMode = value; }
        }

        /// <summary>
        /// Gets or sets the opacity.
        /// </summary>
        public float Opacity
        {
            get;
            set;
        }

        #endregion

        #region Constructors 

        /// <summary>
        /// Creates an instance of this class
        /// </summary>
        /// <param name="tileSource">the source to get the tiles from</param>
        /// <param name="layerName">name of the layer</param>
        public TileLayer(ITileSource tileSource, string layerName)
            : this(tileSource, layerName, new Color(), true, null)
        {
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="tileSource">The source to get the tiles from</param>
        /// <param name="layerName">The name of the layer</param>
        /// <param name="transparentColor">The color to be treated as transparent color</param>
        /// <param name="showErrorInTile">Flag indicating that an error tile should be generated for <see cref="WebException"/>s</param>
        public TileLayer(ITileSource tileSource, string layerName, Color transparentColor, bool showErrorInTile)
            : this(tileSource, layerName, transparentColor, showErrorInTile, null)
        {
        }

        /// <summary>
        /// Creates an instance of this class
        /// </summary>
        /// <param name="tileSource">the source to get the tiles from</param>
        /// <param name="layerName">name of the layer</param>
        /// <param name="transparentColor">transparent color off</param>
        /// <param name="showErrorInTile">generate an error tile if it could not be retrieved from source</param>
        /// <param name="fileCacheDir">If the layer should use a file-cache so store tiles, set this to that directory. Set to null to avoid filecache</param>
        /// <remarks>If <paramref name="showErrorInTile"/> is set to false, tile source keeps trying to get the tile in every request</remarks>
        public TileLayer(ITileSource tileSource, string layerName, Color transparentColor, bool showErrorInTile, string fileCacheDir)
        {
            _source = tileSource;
            LayerName = layerName;
            _transparentColor = transparentColor;
            _showErrorInTile = showErrorInTile;
            Opacity = 1.0f;
            if (!string.IsNullOrEmpty(fileCacheDir))
            {
                _fileCache = new FileCache(fileCacheDir, "png");
                _ImageFormat = ImageFormat.Png;
            }
        }

        /// <summary>
        /// Creates an instance of this class
        /// </summary>
        /// <param name="tileSource">the source to get the tiles from</param>
        /// <param name="layerName">name of the layer</param>
        /// <param name="transparentColor">transparent color off</param>
        /// <param name="showErrorInTile">generate an error tile if it could not be retrieved from source</param>
        /// <param name="fileCache">If the layer should use a file-cache so store tiles, set this to a fileCacheProvider. Set to null to avoid filecache</param>
        /// <param name="imgFormat">Set the format of the tiles to be used</param>
        public TileLayer(ITileSource tileSource, string layerName, Color transparentColor, bool showErrorInTile, FileCache fileCache, ImageFormat imgFormat)
        {
            Opacity = 1.0f;

            _source = tileSource;
            LayerName = layerName;
            _transparentColor = transparentColor;
            _showErrorInTile = showErrorInTile;

            _fileCache = fileCache;
            _ImageFormat = imgFormat;
        }

        #endregion


        #region Public methods

        /// <summary>
        /// Renders the layer
        /// </summary>
        /// <param name="graphics">Graphics object reference</param>
        /// <param name="map">Map which is rendered</param>
        public override void Render(Graphics graphics, IMapViewPort map)
        {
            if (!map.Size.IsEmpty && map.Size.Width > 0 && map.Size.Height > 0)
            {
                var bmp = new Bitmap(map.Size.Width, map.Size.Height, PixelFormat.Format32bppArgb);

                using (var g = Graphics.FromImage(bmp))
                {
                    g.InterpolationMode = InterpolationMode;
                    g.Transform = graphics.Transform;

                    var extent = new Extent(map.Envelope.MinX, map.Envelope.MinY,
                        map.Envelope.MaxX, map.Envelope.MaxY);
                    var level = BruTile.Utilities.GetNearestLevel(_source.Schema.Resolutions, map.PixelSize);
                    var tiles = new List<TileInfo>(_source.Schema.GetTileInfos(extent, level));
                    var tileWidth = _source.Schema.GetTileWidth(level);
                    var tileHeight = _source.Schema.GetTileWidth(level);

                    IList<WaitHandle> waitHandles = new List<WaitHandle>();
                    var toRender = new ConcurrentDictionary<TileIndex, Stream>();
                    var takenFromCache = new ConcurrentDictionary<TileIndex, bool>();
                    foreach (var info in tiles)
                    {
                        var image = _bitmaps.Find(info.Index);
                        if (image != null)
                        {
                            toRender.TryAdd(info.Index, image);
                            takenFromCache.TryAdd(info.Index, true);
                            continue;
                        }
                        if (_fileCache != null && _fileCache.Exists(info.Index))
                        {
                            var tileBitmap = GetImageFromFileCache(info) as byte[];
                            var stream = new MemoryStream(tileBitmap);
                            _bitmaps.Add(info.Index, stream);
                            toRender.TryAdd(info.Index, stream);
                            takenFromCache.TryAdd(info.Index, true);
                            continue;
                        }

                        var waitHandle = new AutoResetEvent(false);
                        waitHandles.Add(waitHandle);
                        ThreadPool.QueueUserWorkItem(GetTileOnThread,
                            new object[] { _source, info, toRender, waitHandle, true, takenFromCache });
                    }

                    foreach (var handle in waitHandles)
                        handle.WaitOne();

                    using (var ia = new ImageAttributes())
                    {
                        if (!_transparentColor.IsEmpty)
                            ia.SetColorKey(_transparentColor, _transparentColor);
#if !PocketPC
                        ia.SetWrapMode(WrapMode.TileFlipXY);
#endif
                        if (Opacity < 1.0f)
                        {
                            var matrix = new ColorMatrix { Matrix33 = Math.Max(Math.Min(1f, Opacity), 0f) };
                            ia.SetColorMatrix(matrix);
                        }

                        foreach (var info in tiles)
                        {
                            if (!toRender.ContainsKey(info.Index))
                                continue;
                            var stream = toRender[info.Index]; //_bitmaps.Find(info.Index);
                            if (stream == null) continue;

                           //using (var stream = new MemoryStream(buffer))
                            {
                                stream.Position = 0;
                                using (var bitmap = new Bitmap(stream))
                                {

                                    var min = map.WorldToImage(new Coordinate(info.Extent.MinX, info.Extent.MinY));
                                    var max = map.WorldToImage(new Coordinate(info.Extent.MaxX, info.Extent.MaxY));

                                    min = new PointF((float)Math.Round(min.X), (float)Math.Round(min.Y));
                                    max = new PointF((float)Math.Round(max.X), (float)Math.Round(max.Y));

                                    try
                                    {
                                        g.DrawImage(bitmap,
                                            new Rectangle((int)min.X, (int)max.Y, (int)(max.X - min.X),
                                                (int)(min.Y - max.Y)),
                                            0, 0, tileWidth, tileHeight,
                                            GraphicsUnit.Pixel,
                                            ia);
                                    }
                                    catch (Exception ee)
                                    {
                                        Logger.Error(ee.Message);
                                    }
                                }

                            }
                        }

                        //Add rendered tiles to cache
                        foreach (
                            var kvp in
                                toRender.Where(kvp => takenFromCache.ContainsKey(kvp.Key) && !takenFromCache[kvp.Key]))
                        {
                            _bitmaps.Add(kvp.Key, kvp.Value);
                        }

                        using (var transform = new Matrix())
                        {
                            graphics.Transform = transform;
                            graphics.DrawImageUnscaled(bmp, 0, 0);
                            graphics.Transform = g.Transform;
                        }
                    }
                }
            }
        }

        #endregion

        #region Private methods



        private void GetTileOnThread(object parameter)
        {
            var parameters = (object[])parameter;
            if (parameters.Length != 6) throw new ArgumentException("Six parameters expected");
            var tileProvider = (ITileProvider)parameters[0];
            var tileInfo = (TileInfo)parameters[1];
            var bitmaps = (ConcurrentDictionary<TileIndex, byte[]>)parameters[2];
            var autoResetEvent = (AutoResetEvent)parameters[3];
            var retry = (bool)parameters[4];
            var takenFromCache = (IDictionary<TileIndex, bool>)parameters[5];

            var setEvent = true;
            try
            {
                var bytes = tileProvider.GetTile(tileInfo);
                bitmaps.TryAdd(tileInfo.Index, bytes);

                // this bitmap will later be memory cached
                takenFromCache.Add(tileInfo.Index, false);

                // add to persistent cache if enabled
                if (_fileCache != null)
                {
                    AddImageToFileCache(tileInfo, bytes);
                }

            }
            catch (WebException ex)
            {
                if (retry)
                {
                    GetTileOnThread(new object[] { tileProvider, tileInfo, bitmaps, autoResetEvent, false, takenFromCache });
                    setEvent = false;
                    return;
                }
                //TODO : gérer tuile 
                //if (_showErrorInTile)
                //{
                //    try
                //    {
                //        //an issue with this method is that one an error tile is in the memory cache it will stay even
                //        //if the error is resolved. PDD.
                //        var tileWidth = _source.Schema.GetTileWidth(tileInfo.Index.Level);
                //        var tileHeight = _source.Schema.GetTileHeight(tileInfo.Index.Level);
                //        var bitmap = new Bitmap(tileWidth, tileHeight);
                //        using (var graphics = Graphics.FromImage(bitmap))
                //        {
                //            graphics.DrawString(ex.Message, new Font(FontFamily.GenericSansSerif, 12),
                //                                new SolidBrush(Color.Black),
                //                                new RectangleF(0, 0, tileWidth, tileHeight));
                //        }
                //        bitmaps.TryAdd(tileInfo.Index, bitmap);
                //    }
                //    catch (Exception e)
                //    {
                //        // we don't want fatal exceptions here!
                //        Logger.Error(e);
                //    }

                //}
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message);
            }
            finally
            {
                if (setEvent) autoResetEvent.Set();
            }
        }

        /// <summary>
        /// Method to add a tile image to the <see cref="FileCache"/>
        /// </summary>
        /// <param name="tileInfo">The tile info</param>
        /// <param name="data">The tile image</param>
        protected void AddImageToFileCache(TileInfo tileInfo, byte[] data)
        {
            _fileCache.Add(tileInfo.Index, data);
        }

        /// <summary>
        /// Function to get a tile image from the <see cref="FileCache"/>.
        /// </summary>
        /// <param name="info">The tile info</param>
        /// <returns>The tile-image, if already cached</returns>
        protected byte[] GetImageFromFileCache(TileInfo info)
        {
            return _fileCache.Find(info.Index);
        }
        #endregion



        /// <summary>
        /// Deserialisation.
        /// </summary>
        /// <param name="sender"></param>
        public void OnDeserialization(object sender)
        {
            if (_bitmaps == null)
            {
                _bitmaps = new MemoryCache<Stream>(200, 300);
            }
        }

        /// <summary>
        /// Cleanup resources.
        /// </summary>
        protected override void ReleaseManagedResources()
        {
            base.ReleaseManagedResources();

            _fileCache = null;
            
            if (_bitmaps != null)
            {
                _bitmaps.Dispose();
                _bitmaps = null;
            }

            var source = _source as IDisposable;
            if (source != null)
            {
                source.Dispose();
                source = null;
            }

        }
    }
}
