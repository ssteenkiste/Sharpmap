using System;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Threading;
using BruTile;
using BruTile.Cache;
using System.IO;
using GeoAPI.Geometries;
using System.Threading.Tasks;
using Common.Logging;
using SharpMap.Fetching;
using System.ComponentModel;
using System.Globalization;

namespace SharpMap.Layers
{
    /// <summary>
    /// Tile layer class that gets and serves tiles asynchonously
    /// </summary>
    [Serializable]
    public class TileAsyncLayer : TileLayer, ITileAsyncLayer, IAsyncDataFetcher
    {
        class DownloadTask
        {
            public CancellationTokenSource CancellationToken;
            public Task Task;
        }

        static readonly ILog Logger = LogManager.GetLogger(typeof(TileAsyncLayer));
        private readonly List<DownloadTask> _currentTasks = new List<DownloadTask>();
        private TileFetcher _tileFetcher;
        private readonly int _maxRetries = TileFetcher.DEFAULT_MAX_ATTEMPTS;
        private readonly int _maxThreads = TileFetcher.DEFAULT_MAX_THREADS;
        private readonly IFetchStrategy _fetchStrategy = new FetchStrategy();
        private readonly int _minExtraTiles = 0;
        private readonly int _maxExtraTiles = 0;
        private int _numberTilesNeeded;
        private bool _busy;
        int _numPendingDownloads = 0;
        bool _onlyRedrawWhenComplete = false;

        /// <summary>
        /// Gets or setsa value indicating if the layer is busy.
        /// </summary>
        public bool Busy
        {
            get { return _busy; }
            set
            {
                if (_busy == value) return; // prevent notify              
                _busy = value;
            }
        }


        /// <summary>
        /// Gets or Sets a value indicating if to redraw the map only when all tiles are downloaded
        /// </summary>
        public bool OnlyRedrawWhenComplete
        {
            get { return _onlyRedrawWhenComplete; }
            set { _onlyRedrawWhenComplete = value; }
        }

        /// <summary>
        /// Returns the number of tiles that are in queue for download
        /// </summary>
        public int NumPendingDownloads { get { return _numPendingDownloads; } }

        /// <summary>
        /// Event raised when tiles are downloaded
        /// </summary>
        public event DownloadProgressHandler DownloadProgressChanged;

        /// <summary>
        /// Creates an instance of this class
        /// </summary>
        /// <param name="tileSource">The tile source</param>
        /// <param name="layerName">The layers name</param>
        public TileAsyncLayer(ITileSource tileSource, string layerName)
            : base(tileSource, layerName, new Color(), true, null)
        {
            SetTileSource(tileSource);
        }

        /// <summary>
        /// Creates an instance of this class
        /// </summary>
        /// <param name="tileSource">The tile source</param>
        /// <param name="layerName">The layers name</param>
        /// <param name="transparentColor">The color that should be treated as <see cref="Color.Transparent"/></param>
        /// <param name="showErrorInTile">Value indicating that an error tile should be generated for non-existent tiles</param>
        public TileAsyncLayer(ITileSource tileSource, string layerName, Color transparentColor, bool showErrorInTile)
            : base(tileSource, layerName, transparentColor, showErrorInTile, null)
        {
            SetTileSource(tileSource);

        }

        /// <summary>
        /// Creates an instance of this class
        /// </summary>
        /// <param name="tileSource">The tile source</param>
        /// <param name="layerName">The layers name</param>
        /// <param name="transparentColor">The color that should be treated as <see cref="Color.Transparent"/></param>
        /// <param name="showErrorInTile">Value indicating that an error tile should be generated for non-existent tiles</param>
        /// <param name="fileCacheDir">The directories where tiles should be stored</param>
        public TileAsyncLayer(ITileSource tileSource, string layerName, Color transparentColor, bool showErrorInTile, string fileCacheDir)
            : base(tileSource, layerName, transparentColor, showErrorInTile, fileCacheDir)
        {
            SetTileSource(tileSource);
        }

        /// <summary>
        /// Creates an instance of this class
        /// </summary>
        /// <param name="tileSource">The tile source</param>
        /// <param name="layerName">The layers name</param>
        /// <param name="transparentColor">The color that should be treated as <see cref="Color.Transparent"/></param>
        /// <param name="showErrorInTile">Value indicating that an error tile should be generated for non-existent tiles</param>
        /// <param name="fileCache">If the layer should use a file-cache so store tiles, set this to a fileCacheProvider. Set to null to avoid filecache</param>
        /// <param name="imgFormat">Set the format of the tiles to be used</param>
        public TileAsyncLayer(ITileSource tileSource, string layerName, Color transparentColor, bool showErrorInTile, FileCache fileCache, ImageFormat imgFormat)
            : base(tileSource, layerName, transparentColor, showErrorInTile, fileCache, imgFormat)
        {
            SetTileSource(tileSource);
        }

        /// <summary>
        /// Sets the tile source.
        /// </summary>
        /// <param name="source"></param>
        protected void SetTileSource(ITileSource source)
        {
            if (_tileFetcher != null)
            {
                _tileFetcher.AbortFetch();
                _tileFetcher.DataChanged -= TileFetcherDataChanged;
                _tileFetcher.PropertyChanged -= TileFetcherOnPropertyChanged;
                _tileFetcher = null;
                _bitmaps.Clear();
            }
            _source = source;
            if (source == null) return;
            _tileFetcher = new TileFetcher(source, _bitmaps, _maxRetries, _maxThreads, _fetchStrategy, _fileCache);
            _tileFetcher.DataChanged += TileFetcherDataChanged;
            _tileFetcher.PropertyChanged += TileFetcherOnPropertyChanged;

        }

        /// <summary>
        /// Cleanup resources.
        /// </summary>
        protected override void ReleaseUnmanagedResources()
        {
            AbortFetch();
            SetTileSource(null);
            base.ReleaseUnmanagedResources();
        }

        private void TileFetcherOnPropertyChanged(object sender, PropertyChangedEventArgs propertyChangedEventArgs)
        {
            if (propertyChangedEventArgs.PropertyName == "Busy")
            {
                if (_tileFetcher != null) Busy = _tileFetcher.Busy;
            }
        }

        private void TileFetcherDataChanged(object sender, DataChangedEventArgs e)
        {

            //OnMapNewTileAvaliable(e.TileInfo, e.Image);
            OnLayerDataLoaded();

            e.Layer = this;
            if (DataChanged != null)
            {
                DataChanged(this, e);
            }
        }

        private void UpdateMemoryCacheMinAndMax()
        {
            if (_minExtraTiles < 0 || _maxExtraTiles < 0
                || _numberTilesNeeded == _tileFetcher.NumberTilesNeeded) return;
            _numberTilesNeeded = _tileFetcher.NumberTilesNeeded;
            _bitmaps.MinTiles = _numberTilesNeeded + _minExtraTiles;
            _bitmaps.MaxTiles = _numberTilesNeeded + _maxExtraTiles;
        }


        /// <summary>
        /// EventHandler for event fired when a new Tile is available for rendering
        /// </summary>
        public event MapNewTileAvaliabledHandler MapNewTileAvaliable;


        public event DataChangedEventHandler DataChanged;


        /// <summary>
        /// Renders the layer
        /// </summary>
        /// <param name="graphics">Graphics object reference</param>
        /// <param name="map">Map which is rendered</param>
        public override void Render(Graphics graphics, IMapViewPort map)
        {
            var bbox = map.Envelope;
            var extent = new Extent(bbox.MinX, bbox.MinY, bbox.MaxX, bbox.MaxY);

            UpdateMemoryCacheMinAndMax();

            var dictionary = new Dictionary<TileIndex, Tuple<TileInfo, Stream>>();
            var levelId = BruTile.Utilities.GetNearestLevel(_source.Schema.Resolutions, map.PixelSize);

            GetTilesWanted(dictionary, _source.Schema, _bitmaps, _fileCache, extent, levelId);
            var sortedFeatures = dictionary.OrderByDescending(t => _source.Schema.Resolutions[t.Key.Level].UnitsPerPixel);
            var tiles = sortedFeatures.ToDictionary(pair => pair.Key, pair => pair.Value).Values.ToList();

            using (var imageAttributes = new ImageAttributes())
            {

                if (!_transparentColor.IsEmpty)
                    imageAttributes.SetColorKey(_transparentColor, _transparentColor);
#if !PocketPC
                imageAttributes.SetWrapMode(WrapMode.TileFlipXY);
#endif

                if (Opacity < 1.0f)
                {
                    var matrix = new ColorMatrix { Matrix33 = Math.Max(Math.Min(1f, Opacity), 0f) };
                    imageAttributes.SetColorMatrix(matrix);
                }
               
                var tileWidth = _source.Schema.GetTileWidth(levelId);
                var tileHeight = _source.Schema.GetTileHeight(levelId);

                foreach (var tile in tiles)
                {
                    var bmp = tile.Item2;
                    if (bmp != null && bmp.CanRead && bmp.CanSeek)
                    {
                        //draws directly the bitmap
                        var box = new Envelope(new Coordinate(tile.Item1.Extent.MinX, tile.Item1.Extent.MinY),
                                              new Coordinate(tile.Item1.Extent.MaxX, tile.Item1.Extent.MaxY));

                        var min = map.WorldToImage(box.Min());
                        var max = map.WorldToImage(box.Max());

                        min = new PointF((float)Math.Round(min.X), (float)Math.Round(min.Y));
                        max = new PointF((float)Math.Round(max.X), (float)Math.Round(max.Y));

                        try
                        {
                            bmp.Position = 0;
                            using (var bitmap = new Bitmap(bmp))
                            {
                                graphics.DrawImage(bitmap,
                                    new Rectangle((int)min.X, (int)max.Y, (int)(max.X - min.X), (int)(min.Y - max.Y)),
                                    0, 0,
                                    tileWidth, tileHeight,
                                    GraphicsUnit.Pixel,
                                    imageAttributes);

                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Warn(ex.Message, ex);
                            //this can be a GDI+ Hell Exception...
                        }
                    }
                }
            }

        }

        private void OnMapNewTileAvaliable(TileInfo tileInfo, byte[] bitmap)
        {
            if (_onlyRedrawWhenComplete)
                return;

            if (MapNewTileAvaliable != null)
            {
                var bb = new Envelope(new Coordinate(tileInfo.Extent.MinX, tileInfo.Extent.MinY), new Coordinate(tileInfo.Extent.MaxX, tileInfo.Extent.MaxY));
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

                    var tileWidth = _source.Schema.GetTileWidth(tileInfo.Index.Level);
                    var tileHeight = _source.Schema.GetTileHeight(tileInfo.Index.Level);
                    MapNewTileAvaliable(this, bb, bitmap, tileWidth, tileHeight, ia);

                }
            }
        }

        /// <summary>
        /// Loads the datas.
        /// </summary>
        /// <param name="view"></param>
        public override void LoadDatas(IMapViewPort view)
        {
            //AbortFetch();
            var bbox = view.Envelope;
            var extent = new Extent(bbox.MinX, bbox.MinY, bbox.MaxX, bbox.MaxY);
            _tileFetcher.ViewChanged(extent, view.PixelSize);

        }

        /// <summary>
        /// Aborts the fetch of data that is currently in progress.
        /// With new ViewChanged calls the fetch will start again. 
        /// Call this method to speed up garbage collection
        /// </summary>
        public void AbortFetch()
        {
            if (_tileFetcher != null)
            {
                _tileFetcher.AbortFetch();
            }
        }

        /// <summary>
        /// Clears the cache.
        /// </summary>
        public void ClearCache()
        {
            AbortFetch();
            _bitmaps.Clear();
        }

        static void GetTilesWanted(IDictionary<TileIndex, Tuple<TileInfo, Stream>> resultTiles, ITileSchema schema, MemoryCache<Stream> bitmaps, FileCache cache, Extent extent, string levelId)
        {

            // to improve performance, convert the resolutions to a list so they can be walked up by
            // simply decrementing an index when the level index needs to change
            var resolutions = schema.Resolutions.OrderByDescending(pair => pair.Value.UnitsPerPixel).ToList();
            for (var i = 0; i < resolutions.Count; i++)
            {
                if (levelId == resolutions[i].Key)
                {
                    GetRecursiveTiles(resultTiles, schema, bitmaps, cache, extent, resolutions, i);
                    break;
                }
            }

        }

        private static void GetRecursiveTiles(IDictionary<TileIndex, Tuple<TileInfo, Stream>> resultTiles, ITileSchema schema, ITileCache<Stream> bitmaps,
            FileCache cache, Extent extent, IList<KeyValuePair<string, Resolution>> resolutions, int resolutionIndex)
        {
            if (resolutionIndex < 0 || resolutionIndex >= resolutions.Count)
                return;
            var tiles = schema.GetTileInfos(extent, resolutions[resolutionIndex].Key);

            foreach (var tileInfo in tiles)
            {
                var feature = bitmaps.Find(tileInfo.Index);

                if (feature != null)
                {
                    resultTiles[tileInfo.Index] = new Tuple<TileInfo, Stream>(tileInfo, feature);
                }
                else
                {

                    byte[] cachedImg = null;
                    if (cache != null)
                    {
                        //Find in file cache.
                        cachedImg = cache.Find(tileInfo.Index);
                    }

                    if (cachedImg != null)
                    {
                        var stream = new MemoryStream(cachedImg);
                        bitmaps.Add(tileInfo.Index, stream);
                        resultTiles[tileInfo.Index] = new Tuple<TileInfo, Stream>(tileInfo, stream);
                    }
                    else
                    {
                        // only continue the recursive search if this tile is within the extent
                        if (tileInfo.Extent.Intersects(extent))
                        {
                            GetRecursiveTiles(resultTiles, schema, bitmaps, cache, tileInfo.Extent.Intersect(extent), resolutions, resolutionIndex - 1);
                        }
                    }
                }
            }
        }
    }


}
