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

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using GeoAPI.Geometries;
using SharpMap.Layers;
using SharpMap.Rendering;
using SharpMap.Rendering.Decoration;
using SharpMap.Utilities;
using Point = GeoAPI.Geometries.Coordinate;
using System.Drawing.Imaging;
using Common.Logging;
using System.Reflection;
using System.Threading;
using SharpMap.Fetching;

namespace SharpMap
{
    /// <summary>
    /// Map class, the main holder for a MapObject in SharpMap
    /// </summary>
    /// <example>
    /// Creating a new map instance, adding layers and rendering the map:
    /// </example>
    [Serializable]
    public class Map : IDisposable, IMapViewPort
    {
        /// <summary>
        /// Method to invoke the static constructor of this class
        /// </summary>
        public static void Configure()
        {
            // Methods sole purpose is to get the static constructor executed
        }

        /// <summary>
        /// Static constructor. Needed to get <see cref="GeoAPI.GeometryServiceProvider.Instance"/> set.
        /// </summary>
        static Map()
        {
            try
            {
                _logger.Debug("Trying to get GeoAPI.GeometryServiceProvider.Instance");
                var instance = GeoAPI.GeometryServiceProvider.Instance;
                if (instance == null)
                {
                    _logger.Debug("Returned null");
                    throw new InvalidOperationException();
                }
            }
            catch (InvalidOperationException)
            {
                _logger.Debug("Loading NetTopologySuite");
                Assembly.Load("NetTopologySuite");
                _logger.Debug("Loaded NetTopologySuite");
                _logger.Debug("Trying to get GeoAPI.GeometryServiceProvider.Instance");
                var instance = GeoAPI.GeometryServiceProvider.Instance;
                if (instance == null)
                {
                    _logger.Debug("Returned null");
                    throw new InvalidOperationException();
                }
            }

            
        }

        static readonly ILog _logger = LogManager.GetLogger(typeof(Map));

        /// <summary>
        /// Used for converting numbers to/from strings
        /// </summary>
        public static NumberFormatInfo NumberFormatEnUs = new CultureInfo("en-US", false).NumberFormat;

        #region Fields

        private readonly List<IMapDecoration> _decorations = new List<IMapDecoration>();

        private Color _backgroundColor;
        private float _backgroundMaskOpacity;
        private Guid _id = Guid.NewGuid();
        private int _srid = -1;
        private double _zoom;
        private Point _center;
        private readonly LayerCollection _layers;
        private readonly LayerCollection _backgroundLayers;

        private Matrix _mapTransform;
        private Matrix _mapTransformInverted;

        private readonly MapViewPortGuard _mapViewportGuard;
        private readonly Dictionary<object, List<ILayer>> _layersPerGroup = new Dictionary<object, List<ILayer>>();
        private ObservableCollection<ILayer> _replacingCollection;
        #endregion

        /// <summary>
        /// Specifies whether to trigger a dispose on all layers (and their datasources) contained in this map when the map-object is disposed.
        /// The default behaviour is true unless the map is a result of a Map.Clone() operation in which case the value is false
        /// <para/>
        /// If you reuse your datasources or layers between many map-objects you should set this property to false in order for them to keep existing after a map.dispose()
        /// </summary>
        public bool DisposeLayersOnDispose = true;

        /// <summary>
        /// Initializes a new map
        /// </summary>
        public Map() : this(new Size(640, 480))
        {

        }

        /// <summary>
        /// Initializes a new map
        /// </summary>
        /// <param name="size">Size of map in pixels</param>
        public Map(Size size)
        {
            _mapViewportGuard = new MapViewPortGuard(size, 0d, double.MaxValue);

            if (LicenseManager.UsageMode != LicenseUsageMode.Designtime)
            {
                Factory = GeoAPI.GeometryServiceProvider.Instance.CreateGeometryFactory(_srid);
            }
            _layers = new LayerCollection();
            _layersPerGroup.Add(_layers, new List<ILayer>());
            _backgroundLayers = new LayerCollection();
            _layersPerGroup.Add(_backgroundLayers, new List<ILayer>());
            BackColor = Color.Transparent;
            _mapTransform = new Matrix();
            _mapTransformInverted = new Matrix();
            _center = new Point(0, 0);
            _zoom = 1;

            WireEvents();

            if (_logger.IsDebugEnabled)
                _logger.DebugFormat("Map initialized with size {0},{1}", size.Width, size.Height);
        }

        /// <summary>
        /// Wires the events
        /// </summary>
        private void WireEvents()
        {
            _backgroundLayers.CollectionChanged += OnLayersCollectionChanged;
            _layers.CollectionChanged += OnLayersCollectionChanged;
        }

        /// <summary>
        /// Event handler to intercept when a new ITileAsymclayer is added to the Layers List and associate the MapNewTile Handler Event
        /// </summary>
        private void OnLayersCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                var cloneList = _layersPerGroup[sender];
                IterUnHookEvents(sender, cloneList);
            }
            if (e.Action == NotifyCollectionChangedAction.Replace || e.Action == NotifyCollectionChangedAction.Remove)
            {
                IterUnHookEvents(sender, e.OldItems.Cast<ILayer>());
            }
            if (e.Action == NotifyCollectionChangedAction.Replace || e.Action == NotifyCollectionChangedAction.Add)
            {
                IterWireEvents(sender, e.NewItems.Cast<ILayer>());
            }

            if (!_disposing)
            {
                ViewChanged();
            }
        }

        private void IterWireEvents(object owner, IEnumerable<ILayer> layers)
        {
            foreach (var layer in layers)
            {
                _layersPerGroup[owner].Add(layer);

                var l = layer as Layer;
                if (l != null)
                {
                    l.StyleChanged += OnLayerStyleChanged;
                }

                var tileAsyncLayer = layer as ITileAsyncLayer;
                if (tileAsyncLayer != null)
                {
                    WireTileAsyncEvents(tileAsyncLayer);
                }

                var asyncLayer = layer as IAsyncDataFetcher;
                if (asyncLayer != null)
                {
                    WireAsyncEvents(asyncLayer);
                }

                var group = layer as LayerGroup;
                if (group != null)
                {
                    group.LayersChanging += OnLayerGroupCollectionReplacing;
                    group.LayersChanged += OnLayerGroupCollectionReplaced;

                    var nestedList = group.Layers;
                    if (group.Layers != null)
                    {
                        group.Layers.CollectionChanged += OnLayersCollectionChanged;
                        _layersPerGroup.Add(nestedList, new List<ILayer>());
                    }
                    else
                    {
                        _layersPerGroup.Add(nestedList, new List<ILayer>());
                    }

                    IterWireEvents(nestedList, nestedList);
                }
            }
        }

        private void OnLayerStyleChanged(object sender, EventArgs e)
        {
            var layer = sender as Layer;

            if (layer != null)
            {
                if (layer.Enabled)
                {
                    layer.LoadDatas(this);
                }
                else
                {
                    OnRefreshNeeded(EventArgs.Empty);
                }
            }
        }

        private void IterUnHookEvents(object owner, IEnumerable<ILayer> layers)
        {
            var toBeRemoved = new List<ILayer>();

            foreach (var layer in layers)
            {
                toBeRemoved.Add(layer);

                var l = layer as Layer;
                if (l != null)
                {
                    l.StyleChanged -= OnLayerStyleChanged;
                }

                var tileAsyncLayer = layer as ITileAsyncLayer;
                if (tileAsyncLayer != null)
                {
                    UnhookTileAsyncEvents(tileAsyncLayer);
                }

                var asyncLayer = layer as IAsyncDataFetcher;
                if (asyncLayer != null)
                {
                    UnhookAsyncEvents(asyncLayer);
                }

                var group = layer as LayerGroup;
                if (group != null)
                {
                    group.LayersChanging -= OnLayerGroupCollectionReplacing;
                    group.LayersChanged -= OnLayerGroupCollectionReplaced;

                    var nestedList = group.Layers;

                    if (nestedList != null)
                    {
                        nestedList.CollectionChanged -= OnLayersCollectionChanged;

                        IterUnHookEvents(nestedList, nestedList);

                        _layersPerGroup.Remove(nestedList);
                    }
                }
            }

            var clonedList = _layersPerGroup[owner];
            toBeRemoved.ForEach(layer => clonedList.Remove(layer));
        }

        private void OnLayerGroupCollectionReplaced(object sender, EventArgs eventArgs)
        {
            var layerGroup = (LayerGroup)sender;

            var newCollection = layerGroup.Layers;

            IterUnHookEvents(_replacingCollection, _replacingCollection);
            _layersPerGroup.Remove(_replacingCollection);
            _replacingCollection.CollectionChanged -= OnLayersCollectionChanged;

            if (newCollection != null)
            {
                IterWireEvents(newCollection, newCollection);

                _layersPerGroup.Add(newCollection, new List<ILayer>(newCollection));

                newCollection.CollectionChanged += OnLayersCollectionChanged;
            }
        }

        private void OnLayerGroupCollectionReplacing(object sender, EventArgs eventArgs)
        {
            var layerGroup = (LayerGroup)sender;

            _replacingCollection = layerGroup.Layers;
        }

        private void layer_DownloadProgressChanged(int tilesRemaining)
        {
            if (tilesRemaining <= 0)
            {
                OnRefreshNeeded(EventArgs.Empty);
            }
        }

        private void WireTileAsyncEvents(ITileAsyncLayer tileAsyncLayer)
        {
            if (tileAsyncLayer.OnlyRedrawWhenComplete)
            {
                tileAsyncLayer.DownloadProgressChanged += layer_DownloadProgressChanged;
            }
            else
            {
                //tileAsyncLayer.MapNewTileAvaliable += MapNewTileAvaliableHandler;
            }
        }

        private void UnhookTileAsyncEvents(ITileAsyncLayer tileAsyncLayer)
        {

            tileAsyncLayer.DownloadProgressChanged -= layer_DownloadProgressChanged;
            //tileAsyncLayer.MapNewTileAvaliable -= MapNewTileAvaliableHandler;
        }

        private void WireAsyncEvents(IAsyncDataFetcher asyncLayer)
        {
            asyncLayer.DataChanged += AsyncLayer_DataChanged;
        }

        private void UnhookAsyncEvents(IAsyncDataFetcher asyncLayer)
        {
            asyncLayer.DataChanged -= AsyncLayer_DataChanged;
        }

        private void AsyncLayer_DataChanged(object sender, DataChangedEventArgs e)
        {
            DataChanged?.Invoke(this, e);
        }

        /// <summary>
        /// Aborts fetching opération.
        /// </summary>
        void AbortFetch()
        {
            if (Layers != null)
            {
                foreach (var asyncFetcher in Layers.OfType<IAsyncDataFetcher>())
                {
                    asyncFetcher.AbortFetch();
                }
            }
            if (BackgroundLayer != null)
            {
                foreach (var asyncFetcher in BackgroundLayer.OfType<IAsyncDataFetcher>())
                {
                    asyncFetcher.AbortFetch();
                }
            }
        }

        private bool _disposing;

        #region IDisposable Members

        /// <summary>
        /// Disposes the map object
        /// </summary>
        public void Dispose()
        {
            _disposing = true;
            AbortFetch();

            if (DisposeLayersOnDispose)
            {
                if (Layers != null)
                {
                    foreach (var disposable in Layers.OfType<IDisposable>())
                    {
                        disposable.Dispose();
                    }
                }
                if (BackgroundLayer != null)
                {
                    foreach (var disposable in BackgroundLayer.OfType<IDisposable>())
                    {
                        disposable.Dispose();
                    }
                }

            }
            Layers?.Clear();
            BackgroundLayer?.Clear();
        }

        #endregion

        #region Events

        #region Delegates

        /// <summary>
        /// EventHandler for event fired when the maps layer list has been changed
        /// </summary>
        public delegate void LayersChangedEventHandler();

        /// <summary>
        /// EventHandler for event fired when all layers have been rendered
        /// </summary>
        public delegate void MapRenderedEventHandler(Graphics g);

        /// <summary>
        /// EventHandler for event fired when all layers are about to be rendered
        /// </summary>
        public delegate void MapRenderingEventHandler(Graphics g);

        /// <summary>
        /// EventHandler for event fired when the zoomlevel or the center point has been changed
        /// </summary>
        public delegate void MapViewChangedHandler();


        #endregion

        /// <summary>
        /// Event fired when the maps layer list have been changed
        /// </summary>
        [Obsolete("This event is never invoked since it has been made impossible to change the LayerCollection for a map instance.")]
#pragma warning disable 67
        public event LayersChangedEventHandler LayersChanged;
#pragma warning restore 67

        /// <summary>
        /// Event fired when the zoomlevel or the center point has been changed
        /// </summary>
        public event MapViewChangedHandler MapViewOnChange;

        /// <summary>
        /// Event fired when all layers are about to be rendered
        /// </summary>
        public event MapRenderedEventHandler MapRendering;

        /// <summary>
        /// Event fired when all layers have been rendered
        /// </summary>
        public event MapRenderedEventHandler MapRendered;

        /// <summary>
        /// Event fired when one layer have been rendered
        /// </summary>
        public event EventHandler<LayerRenderingEventArgs> LayerRendering;

        /// <summary>
        /// Event fired when one layer have been rendered
        /// </summary>
        public event EventHandler<LayerRenderingEventArgs> LayerRenderedEx;

        ///<summary>
        /// Event fired when a layer has been rendered
        ///</summary>
        [Obsolete("Use LayerRenderedEx")]
        public event EventHandler LayerRendered;

        /// <summary>
        /// Event fired when datas has changed.
        /// </summary>
        public event EventHandler<DataChangedEventArgs> DataChanged;


        /// <summary>
        /// Event fired when a new Tile is available in a TileAsyncLayer
        /// </summary>
        public event MapNewTileAvaliabledHandler MapNewTileAvaliable;

        /// <summary>
        /// Event that is called when a layer have changed and the map need to redraw
        /// </summary>
        public event EventHandler RefreshNeeded;

        #endregion

        #region Methods

        #region Map output
        /*
        /// <summary>
        /// Renders the map to an image
        /// </summary>
        /// <returns>the map image</returns>
        public Image GetMap()
        {
            Image img = new Bitmap(Size.Width, Size.Height);
            var g = Graphics.FromImage(img);
            RenderMap(g);
            g.Dispose();
            return img;
        }

        /// <summary>
        /// Renders the map to an image with the supplied resolution
        /// </summary>
        /// <param name="resolution">The resolution of the image</param>
        /// <returns>The map image</returns>
        public Image GetMap(int resolution)
        {
            Image img = new Bitmap(Size.Width, Size.Height);
            ((Bitmap)img).SetResolution(resolution, resolution);
            var g = Graphics.FromImage(img);
            RenderMap(g);
            g.Dispose();
            return img;

        }

        /// <summary>
        /// Renders the map to a Metafile (Vectorimage).
        /// </summary>
        /// <remarks>
        /// A Metafile can be saved as WMF,EMF etc. or put onto the clipboard for paste in other applications such av Word-processors which will give
        /// a high quality vector image in that application.
        /// </remarks>
        /// <returns>The current map rendered as to a Metafile</returns>
        public Metafile GetMapAsMetafile()
        {
            return GetMapAsMetafile(string.Empty);
        }

        /// <summary>
        /// Renders the map to a Metafile (Vectorimage).
        /// </summary>
        /// <param name="metafileName">The filename of the metafile. If this is null or empty the metafile is not saved.</param>
        /// <remarks>
        /// A Metafile can be saved as WMF,EMF etc. or put onto the clipboard for paste in other applications such av Word-processors which will give
        /// a high quality vector image in that application.
        /// </remarks>
        /// <returns>The current map rendered as to a Metafile</returns>
        public Metafile GetMapAsMetafile(string metafileName)
        {
            Metafile metafile;
            var bm = new Bitmap(1, 1);
            using (var g = Graphics.FromImage(bm))
            {
                var hdc = g.GetHdc();
                using (var stream = new MemoryStream())
                {
                    metafile = new Metafile(stream, hdc, new RectangleF(0, 0, Size.Width, Size.Height),
                                            MetafileFrameUnit.Pixel, EmfType.EmfPlusDual);

                    using (var metafileGraphics = Graphics.FromImage(metafile))
                    {
                        metafileGraphics.PageUnit = GraphicsUnit.Pixel;
                        metafileGraphics.TransformPoints(CoordinateSpace.Page, CoordinateSpace.Device,
                                                         new[] { new PointF(Size.Width, Size.Height) });

                        //Render map to metafile
                        RenderMap(metafileGraphics);
                    }

                    //Save metafile if desired
                    if (!string.IsNullOrEmpty(metafileName))
                        File.WriteAllBytes(metafileName, stream.ToArray());
                }
                g.ReleaseHdc(hdc);
            }
            return metafile;
        }
        */
        #endregion


        /// <summary>
        /// Fires the RefreshNeeded event.
        /// </summary>
        /// <param name="e">EventArgs argument.</param>
        public virtual void OnRefreshNeeded(EventArgs e)
        {
            RefreshNeeded?.Invoke(this, e);
        }

        /*
        #region Rendering

        /// <summary>
        /// Renders the map using the provided <see cref="Graphics"/> object.
        /// </summary>
        /// <param name="g">the <see cref="Graphics"/> object to use</param>
        /// <exception cref="ArgumentNullException">if <see cref="Graphics"/> object is null.</exception>
        /// <exception cref="InvalidOperationException">if there are no layers to render.</exception>
        public void RenderMap(Graphics g)
        {

            if (g == null)
                throw new ArgumentNullException(nameof(g), "Cannot render map with null graphics object!");


            if ((Layers == null || Layers.Count == 0) && (BackgroundLayer == null || BackgroundLayer.Count == 0))
                throw new InvalidOperationException("No layers to render");


            if (Size.IsEmpty)
            {
                return;
            }

            OnMapRendering(g);

            g.Transform = MapTransform;
            g.Clear(BackColor);
            g.PageUnit = GraphicsUnit.Pixel;

            IList<ILayer> layerList;
            if (_backgroundLayers != null && _backgroundLayers.Count > 0)
            {
                layerList = _backgroundLayers.ToList();
                foreach (var layer in layerList)
                {
                    OnLayerRendering(layer, LayerCollectionType.Background);

                    if (layer.IsLayerVisible(this))
                    {
                        LayerCollectionRenderer.RenderLayer(layer, g, this);
                    }
                    else
                    {
                        layer.CleanupRendering();
                    }

                    OnLayerRendered(layer, LayerCollectionType.Background);
                }
            }

            //mask.
            if (BackgroundMaskOpacity > 0)
            {

                var opacity = (int) (BackgroundMaskOpacity*  255);
                if (opacity > 255)
                {
                    opacity = 255;
                }

                g.FillRectangle(new SolidBrush(Color.FromArgb(opacity, BackColor.R, BackColor.G, BackColor.B)), g.ClipBounds);
            }

            //Layers drawing
            if (_layers != null && _layers.Count > 0)
            {
                layerList = _layers.ToList();

                //int srid = (Layers.Count > 0 ? Layers[0].SRID : -1); //Get the SRID of the first layer
                foreach (var layer in layerList)
                {
                    OnLayerRendering(layer, LayerCollectionType.Static);

                    if (layer.IsLayerVisible(this))
                    {
                        LayerCollectionRenderer.RenderLayer(layer, g, this);
                    }
                    else
                    {
                        layer.CleanupRendering();
                    }

                    OnLayerRendered(layer, LayerCollectionType.Static);
                }
            }

            // Render all map decorations
            foreach (var mapDecoration in _decorations)
            {
                mapDecoration.Render(g, this);
            }

            OnMapRendered(g);
        }

        /// <summary>
        /// Fired when map is rendering
        /// </summary>
        /// <param name="g"></param>
        protected virtual void OnMapRendering(Graphics g)
        {
            var e = MapRendering;
            e?.Invoke(g);
        }

        /// <summary>
        /// Fired when Map is rendered
        /// </summary>
        /// <param name="g"></param>
        protected virtual void OnMapRendered(Graphics g)
        {
            var e = MapRendered;
            e?.Invoke(g); //Fire render event
        }

        /// <summary>
        /// Method called when starting to render <paramref name="layer"/> of <paramref name="layerCollectionType"/>. This fires the
        /// <see cref="E:SharpMap.Map.LayerRendering"/> event.
        /// </summary>
        /// <param name="layer">The layer to render</param>
        /// <param name="layerCollectionType">The collection type</param>
        protected virtual void OnLayerRendering(ILayer layer, LayerCollectionType layerCollectionType)
        {
            var e = LayerRendering;
            e?.Invoke(this, new LayerRenderingEventArgs(layer, layerCollectionType));
        }

#pragma warning disable 612,618
        /// <summary>
        /// Method called when <paramref name="layer"/> of <paramref name="layerCollectionType"/> has been rendered. This fires the
        /// <see cref="E:SharpMap.Map.LayerRendered"/> and <see cref="E:SharpMap.Map.LayerRenderedEx"/> event.
        /// </summary>
        /// <param name="layer">The layer to render</param>
        /// <param name="layerCollectionType">The collection type</param>
        protected virtual void OnLayerRendered(ILayer layer, LayerCollectionType layerCollectionType)
        {
            var e = LayerRendered;
#pragma warning restore 612,618
            e?.Invoke(this, EventArgs.Empty);

            LayerRenderedEx?.Invoke(this, new LayerRenderingEventArgs(layer, layerCollectionType));
        }

        #endregion
        */

        #region Layer
        /// <summary>
        /// Returns an enumerable for all layers containing the search parameter in the LayerName property
        /// </summary>
        /// <param name="layername">Search parameter</param>
        /// <returns>IEnumerable</returns>
        public IEnumerable<ILayer> FindLayer(string layername)
        {
            return Layers.Where(l => l.LayerName.Contains(layername));
        }

        /// <summary>
        /// Returns a layer by its name
        /// </summary>
        /// <param name="name">Name of layer</param>
        /// <returns>Layer</returns>
        public ILayer GetLayerByName(string name)
        {
            ILayer lay = null;
            if (Layers != null)
            {
                lay = Layers.GetLayerByName(name);
            }
            if (lay == null && BackgroundLayer != null)
            {
                lay = BackgroundLayer.GetLayerByName(name);
            }

            return lay;
        }

        #endregion

        #region ViewPort

        /// <summary>
        /// Zooms to the extents of all layers
        /// </summary>
        public void ZoomToExtents()
        {
            ZoomToBox(GetExtents());
        }

        /// <summary>
        /// Zooms the map to fit a bounding box
        /// </summary>
        /// <remarks>
        /// NOTE: If the aspect ratio of the box and the aspect ratio of the mapsize
        /// isn't the same, the resulting map-envelope will be adjusted so that it contains
        /// the bounding box, thus making the resulting envelope larger!
        /// </remarks>
        /// <param name="bbox"></param>
        public void ZoomToBox(Envelope bbox)
        {
            if (bbox != null && !bbox.IsNull)
            {
                //Ensure aspect ratio
                var resX = Size.Width == 0 ? double.MaxValue : bbox.Width / Size.Width;
                var resY = Size.Height == 0 ? double.MaxValue : bbox.Height / Size.Height;
                var zoom = bbox.Width;
                if (resY > resX && resX > 0)
                {
                    zoom *= resY / resX;
                }

                var center = new Coordinate(bbox.Centre);

                zoom = _mapViewportGuard.VerifyZoom(zoom, center);
                var changed = false;
                if (zoom != _zoom)
                {
                    _zoom = zoom;
                    changed = true;
                }

                if (!center.Equals2D(_center))
                {
                    _center = center;
                    changed = true;
                }

                if (changed)
                    OnMapViewChanged();
            }
        }

        /// <summary>
        /// Converts a point from world coordinates to image coordinates based on the current
        /// zoom, center and mapsize.
        /// </summary>
        /// <param name="p">Point in world coordinates</param>
        /// <param name="careAboutMapTransform">Indicates whether MapTransform should be taken into account</param>
        /// <returns>Point in image coordinates</returns>
        public PointF WorldToImage(Coordinate p, bool careAboutMapTransform)
        {
            var pTmp = Transform.WorldtoMap(p, this);
            if (!careAboutMapTransform)
                return pTmp;

            lock (MapTransform)
            {
                if (!MapTransform.IsIdentity)
                {
                    var pts = new[] { pTmp };
                    MapTransform.TransformPoints(pts);
                    pTmp = pts[0];
                }
            }

            return pTmp;
        }

        /// <summary>
        /// Converts a point from world coordinates to image coordinates based on the current
        /// zoom, center and mapsize.
        /// </summary>
        /// <param name="p">Point in world coordinates</param>
        /// <returns>Point in image coordinates</returns>
        public PointF WorldToImage(Coordinate p)
        {
            return WorldToImage(p, false);
        }

        /// <summary>
        /// Converts a point from image coordinates to world coordinates based on the current
        /// zoom, center and mapsize.
        /// </summary>
        /// <param name="p">Point in image coordinates</param>
        /// <returns>Point in world coordinates</returns>
        public Coordinate ImageToWorld(PointF p)
        {
            return ImageToWorld(p, false);
        }
        /// <summary>
        /// Converts a point from image coordinates to world coordinates based on the current
        /// zoom, center and mapsize.
        /// </summary>
        /// <param name="p">Point in image coordinates</param>
        /// <param name="careAboutMapTransform">Indicates whether MapTransform should be taken into account</param>
        /// <returns>Point in world coordinates</returns>
        public Coordinate ImageToWorld(PointF p, bool careAboutMapTransform)
        {
            if (careAboutMapTransform)
            {
                lock (MapTransform)
                {
                    if (!MapTransform.IsIdentity)
                    {
                        var pts = new[] { p };
                        _mapTransformInverted.TransformPoints(pts);
                        p = pts[0];
                    }
                }
            }
            return Transform.MapToWorld(p, this);
        }

        #endregion

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the unique identifier of the map.
        /// </summary>
        public Guid ID
        {
            get { return _id; }
            set { _id = value; }
        }

        /// <summary>
        /// Gets or sets the SRID of the map
        /// </summary>
        public int SRID
        {
            get { return _srid; }
            set
            {
                if (_srid == value)
                    return;
                _srid = value;
                Factory = GeoAPI.GeometryServiceProvider.Instance.CreateGeometryFactory(_srid);
            }
        }

        /// <summary>
        /// Factory used to create geometries
        /// </summary>
        public IGeometryFactory Factory { get; private set; }

        /// <summary>
        /// List of all map decorations
        /// </summary>
        public IList<IMapDecoration> Decorations => _decorations;

        /// <summary>
        /// Gets the extents of the current map based on the current zoom, center and mapsize
        /// </summary>
        public Envelope Envelope
        {
            get
            {
                if (double.IsNaN(MapHeight) || double.IsInfinity(MapHeight))
                    return new Envelope(0, 0, 0, 0);

                var ll = new Coordinate(Center.X - Zoom * .5, Center.Y - MapHeight * .5);
                var ur = new Coordinate(Center.X + Zoom * .5, Center.Y + MapHeight * .5);

                var ptfll = WorldToImage(ll, true);
                ptfll = new PointF(Math.Abs(ptfll.X), Math.Abs(Size.Height - ptfll.Y));
                if (!ptfll.IsEmpty)
                {
                    ll.X = ll.X - ptfll.X * PixelWidth;
                    ll.Y = ll.Y - ptfll.Y * PixelHeight;
                    ur.X = ur.X + ptfll.X * PixelWidth;
                    ur.Y = ur.Y + ptfll.Y * PixelHeight;
                }
                return new Envelope(ll, ur);
            }
        }

        /// <summary>
        /// Using the <see cref="MapTransform"/> you can alter the coordinate system of the map rendering.
        /// This makes it possible to rotate or rescale the image, for instance to have another direction than north upwards.
        /// </summary>
        /// <example>
        /// Rotate the map output 45 degrees around its center:
        /// <code lang="C#">
        /// System.Drawing.Drawing2D.Matrix maptransform = new System.Drawing.Drawing2D.Matrix(); //Create transformation matrix
        ///	maptransform.RotateAt(45,new PointF(myMap.Size.Width/2,myMap.Size.Height/2)); //Apply 45 degrees rotation around the center of the map
        ///	myMap.MapTransform = maptransform; //Apply transformation to map
        /// </code>
        /// </example>
        public Matrix MapTransform
        {
            get { return _mapTransform; }
            set
            {
                _mapTransform = value;
                if (_mapTransform.IsInvertible)
                {
                    _mapTransformInverted = value.Clone();
                    _mapTransformInverted.Invert();
                }
                else
                    _mapTransformInverted.Reset();
            }
        }

        /// <summary>
        /// A collection of layers. The first layer in the list is drawn first, the last one on top.
        /// </summary>
        public LayerCollection Layers => _layers;

        /// <summary>
        /// Collection of background Layers
        /// </summary>
        public LayerCollection BackgroundLayer => _backgroundLayers;

        /// <summary>
        /// Map background color (defaults to transparent)
        /// </summary>
        public Color BackColor
        {
            get { return _backgroundColor; }
            set
            {
                _backgroundColor = value;
                SetBackgroundBrush();

                OnMapViewChanged();
            }
        }
        /// <summary>
        /// Gets or sets the background mask opacity
        /// </summary>
        public float BackgroundMaskOpacity
        {
            get { return _backgroundMaskOpacity; }
            set
            {
                if (value < 0) value = 0;
                if (value > 1) value = 1;

                if (_backgroundMaskOpacity != value)
                {
                    _backgroundMaskOpacity = value;
                    SetBackgroundBrush();
                }
                OnMapViewChanged();
            }

        }

        private void SetBackgroundBrush()
        {
            var opacity = (int)(_backgroundMaskOpacity * 255);
            var oldBrush = BackgroundMaskBrush;
            BackgroundMaskBrush = BackColor.IsEmpty ? null : new SolidBrush(Color.FromArgb(opacity, BackColor.R, BackColor.G, BackColor.B));
            oldBrush?.Dispose();

        }

        internal Brush BackgroundMaskBrush { get; set; }


        private const double PRECISION_TOLERANCE = 0.00000001;


        /// <summary>
        /// Changes the view center and zoom level.
        /// </summary>
        /// <param name="center">The center.</param>
        /// <param name="zoom">The zoom level.</param>
        public void ChangeView(Point center, double zoom)
        {
            var newZoom = zoom;
            var newCenter = center;

            newZoom = _mapViewportGuard.VerifyZoom(newZoom, newCenter);

            var changed = false;
            if (Math.Abs(newZoom - _zoom) > PRECISION_TOLERANCE)
            {
                _zoom = newZoom;
                changed = true;
            }

            if (!newCenter.Equals2D(_center, PRECISION_TOLERANCE))
            {
                _center = newCenter;
                changed = true;
            }

            if (changed)
            {
                ViewChanged();
                OnMapViewChanged();
            }
        }

        #region Zoom

        /// <summary>
        /// Minimum zoom amount allowed
        /// </summary>
        public double MinimumZoom
        {
            get { return _mapViewportGuard.MinimumZoom; }
            set
            {
                _mapViewportGuard.MinimumZoom = value;
            }
        }

        /// <summary>
        /// Maximum zoom amount allowed
        /// </summary>
        public double MaximumZoom
        {
            get { return _mapViewportGuard.MaximumZoom; }
            set
            {
                _mapViewportGuard.MaximumZoom = value;
            }
        }

        /// <summary>
        /// Gets or sets the zoom level of map.
        /// </summary>
        /// <remarks>
        /// <para>The zoom level corresponds to the width of the map in WCS units.</para>
        /// <para>A zoomlevel of 0 will result in an empty map being rendered, but will not throw an exception</para>
        /// </remarks>
        public double Zoom
        {
            get { return _zoom; }
            set
            {
                var newCenter = new Coordinate(_center);
                value = _mapViewportGuard.VerifyZoom(value, newCenter);

                if (value.Equals(_zoom))
                    return;

                _zoom = value;
                if (!newCenter.Equals2D(_center))
                    _center = newCenter;

                OnMapViewChanged();
            }
        }

        #endregion

        /// <summary>
        /// Center of map in WCS
        /// </summary>
        public Coordinate Center
        {
            get { return _center; }
            set
            {
                if (value == null)
                    throw new ArgumentNullException(nameof(value));

                var newZoom = _zoom;
                var newCenter = new Coordinate(value);

                newZoom = _mapViewportGuard.VerifyZoom(newZoom, newCenter);

                var changed = false;
                if (newZoom != _zoom)
                {
                    _zoom = newZoom;
                    changed = true;
                }

                if (!newCenter.Equals2D(_center))
                {
                    _center = newCenter;
                    changed = true;
                }

                if (changed)
                    OnMapViewChanged();
            }
        }

        /// <summary>
        /// The view on this map has changed. Maybe the layers need to load new data, they need to be notified.
        /// </summary>
        public void ViewChanged()
        {
            LoadDatas();
        }

        /// <summary>
        /// Raises the map view change event.
        /// </summary>
        protected void OnMapViewChanged()
        {
            MapViewOnChange?.Invoke();
        }


        private void LoadDatas()
        {
            foreach (var bl in BackgroundLayer.ToList().OfType<Layer>())
            {
                if (bl.IsLayerVisible(this))
                {
                    bl.LoadDatas(this);
                }
                else
                {
                    bl.CleanupRendering();
                }
            }
            foreach (var l in Layers.ToList().OfType<Layer>())
            {
                if (l.IsLayerVisible(this))
                {
                    l.LoadDatas(this);
                }
                else
                {
                    l.CleanupRendering();
                }
            }
        }

        protected bool IsFetching;
        protected bool NeedsUpdate = true;
        protected Envelope NewEnvelope;
        public int FetchingPostponedInMilliseconds => 500;
        protected Timer StartFetchTimer;

        void FetchDatas()
        {
            if (_center == null)
                return;

            NewEnvelope = Envelope;
            if (IsFetching)
            {
                NeedsUpdate = true;
                return;
            }

            StartFetchTimer?.Dispose();
            StartFetchTimer = new Timer(StartFetchTimerElapsed, null, FetchingPostponedInMilliseconds, int.MaxValue);
        }

        void StartFetchTimerElapsed(object state)
        {
            if (NewEnvelope == null) return;
            LoadDatas();
            StartFetchTimer.Dispose();
        }


        private static int? _dpiX;

        /// <summary>
        /// Gets or Sets the Scale of the map (related to current DPI-settings of rendering)
        /// </summary>
        public double MapScale
        {
            get
            {
                if (!_dpiX.HasValue)
                {
                    using (var g = Graphics.FromHwnd(IntPtr.Zero))
                    {
                        _dpiX = (int)g.DpiX;
                    }
                }

                return GetMapScale(_dpiX.Value);
            }
            set
            {
                if (!_dpiX.HasValue)
                {
                    using (var g = Graphics.FromHwnd(IntPtr.Zero))
                    {
                        _dpiX = (int)g.DpiX;
                    }
                }
                Zoom = GetMapZoomFromScale(value, _dpiX.Value);

            }
        }

        /// <summary>
        /// Calculated the Zoom value for a given Scale-value
        /// </summary>
        /// <param name="scale"></param>
        /// <param name="dpi"></param>
        /// <returns></returns>
        public double GetMapZoomFromScale(double scale, int dpi)
        {
            return ScaleCalculations.GetMapZoomFromScaleNonLatLong(scale, 1, dpi, Size.Width);
        }

        /// <summary>
        /// Returns the mapscale if the map was to be rendered with the specified DPI-settings
        /// </summary>
        /// <param name="dpi"></param>
        /// <returns></returns>
        public double GetMapScale(int dpi)
        {
            return ScaleCalculations.CalculateScaleNonLatLong(Envelope.Width, Size.Width, 1, dpi);
        }


        /// <summary>
        /// Get Returns the size of a pixel in world coordinate units
        /// </summary>
        public double PixelSize => Zoom / Size.Width;

        /// <summary>
        /// Returns the width of a pixel in world coordinate units.
        /// </summary>
        /// <remarks>The value returned is the same as <see cref="PixelSize"/>.</remarks>
        public double PixelWidth => PixelSize;

        /// <summary>
        /// Returns the height of a pixel in world coordinate units.
        /// </summary>
        /// <remarks>The value returned is the same as <see cref="PixelSize"/> unless <see cref="PixelAspectRatio"/> is different from 1.</remarks>
        public double PixelHeight => PixelSize * _mapViewportGuard.PixelAspectRatio;

        /// <summary>
        /// Gets or sets the aspect-ratio of the pixel scales. A value less than 
        /// 1 will make the map stretch upwards, and larger than 1 will make it smaller.
        /// </summary>
        /// <exception cref="ArgumentException">Throws an argument exception when value is 0 or less.</exception>
        public double PixelAspectRatio
        {
            get { return _mapViewportGuard.PixelAspectRatio; }
            set
            {
                _mapViewportGuard.PixelAspectRatio = value;
            }
        }

        /// <summary>
        /// Height of map in world units
        /// </summary>
        /// <returns></returns>
        public double MapHeight => (Zoom * Size.Height) / Size.Width * PixelAspectRatio;

        /// <summary>
        /// Size of output map
        /// </summary>
        public Size Size
        {
            get { return _mapViewportGuard.Size; }
            set { _mapViewportGuard.Size = value; }
        }

        /// <summary>
        /// Gets the extents of the map based on the extents of all the layers in the layers collection
        /// </summary>
        /// <returns>Full map extents</returns>
        public Envelope GetExtents()
        {
            if (!_mapViewportGuard.MaximumExtents.IsNull)
                return MaximumExtents;

            if ((Layers == null || Layers.Count == 0) &&
                (BackgroundLayer == null || BackgroundLayer.Count == 0))
                throw (new InvalidOperationException("No layers to zoom to"));

            Envelope bbox = null;

            ExtendBoxForCollection(Layers, ref bbox);
            ExtendBoxForCollection(BackgroundLayer, ref bbox);

            return bbox;
        }

        /// <summary>
        /// Gets or sets a value indicating the maximum visible extent
        /// </summary>
        public Envelope MaximumExtents
        {
            get { return _mapViewportGuard.MaximumExtents; }
            set { _mapViewportGuard.MaximumExtents = value; }
        }

        /// <summary>
        /// Gets or sets a value indicating if <see cref="MaximumExtents"/> should be enforced or not.
        /// </summary>
        public bool EnforceMaximumExtents
        {
            get { return _mapViewportGuard.EnforceMaximumExtents; }
            set { _mapViewportGuard.EnforceMaximumExtents = value; }
        }

        private static void ExtendBoxForCollection(IEnumerable<ILayer> layersCollection, ref Envelope bbox)
        {
            foreach (var l in layersCollection)
            {

                //Tries to get bb. Fails on some specific shapes and Mercator projects (World.shp)
                Envelope bb;
                try
                {
                    bb = l.Envelope;
                }
                catch (Exception)
                {
                    bb = new Envelope(new Coordinate(-20037508.342789, -20037508.342789), new Coordinate(20037508.342789, 20037508.342789));
                }

                if (bbox == null)
                    bbox = bb;
                else
                {
                    //FB: bb can be null on empty layers (e.g. temporary working layers with no data objects)
                    if (bb != null)
                        bbox.ExpandToInclude(bb);
                }

            }
        }

        #endregion
    }
}
