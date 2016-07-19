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
using System.Collections.ObjectModel;
using System.Drawing;
using System.Drawing.Drawing2D;
//#if !DotSpatialProjections
//using GeoAPI;
//using GeoAPI.CoordinateSystems.Transformations;
//#else
//using DotSpatial.Projections;
//#endif
using SharpMap.Data;
using SharpMap.Data.Providers;
using GeoAPI.Geometries;
using SharpMap.Rendering;
using SharpMap.Rendering.Thematics;
using SharpMap.Styles;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Common.Logging;
using SharpMap.Fetching;

namespace SharpMap.Layers
{
    /// <summary>
    /// Class for vector layer properties
    /// </summary>
    [Serializable]
    public class VectorLayer : Layer, ICanQueryLayer, IAsyncDataFetcher
    {
        static readonly ILog _logger = LogManager.GetLogger(typeof(VectorLayer));

        private bool _clippingEnabled;
        private bool _isQueryEnabled = true;
        private IProvider _dataSource;
        private SmoothingMode _smoothingMode;
        private ITheme _theme;
        private Envelope _envelope;

        /// <summary>
        /// Initializes a new layer
        /// </summary>
        /// <param name="layername">Name of layer</param>
        public VectorLayer(string layername)
            : base(new VectorStyle())
        {
            LayerName = layername;
            FetchingPostponedInMilliseconds = 100;
        }

        /// <summary>
        /// Initializes a new layer with a specified datasource
        /// </summary>
        /// <param name="layername">Name of layer</param>
        /// <param name="dataSource">Data source</param>
        public VectorLayer(string layername, IProvider dataSource) : this(layername)
        {
            _dataSource = dataSource;
            SmoothingMode = SmoothingMode.HighQuality;
        }

        /// <summary>
        /// Gets or sets a Dictionary with themes suitable for this layer. A theme in the dictionary can be used for rendering be setting the Theme Property using a delegate function
        /// </summary>
        public Dictionary<string, ITheme> Themes
        {
            get;
            set;
        }


        /// <summary>
        /// Gets or sets thematic settings for the layer. Set to null to ignore thematics
        /// </summary>
        public ITheme Theme
        {
            get { return _theme; }
            set { _theme = value; }
        }

        /// <summary>
        /// Specifies whether polygons should be clipped prior to rendering
        /// </summary>
        /// <remarks>
        /// <para>Clipping will clip <see cref="GeoAPI.Geometries.IPolygon"/> and
        /// <see cref="GeoAPI.Geometries.IMultiPolygon"/> to the current view prior
        /// to rendering the object.</para>
        /// <para>Enabling clipping might improve rendering speed if you are rendering 
        /// only small portions of very large objects.</para>
        /// </remarks>
        public bool ClippingEnabled
        {
            get { return _clippingEnabled; }
            set { _clippingEnabled = value; }
        }

        /// <summary>
        /// Render whether smoothing (antialiasing) is applied to lines and curves and the edges of filled areas
        /// </summary>
        public SmoothingMode SmoothingMode
        {
            get { return _smoothingMode; }
            set { _smoothingMode = value; }
        }

        /// <summary>
        /// Gets or sets the datasource
        /// </summary>
        public IProvider DataSource
        {
            get { return _dataSource; }
            set { _dataSource = value; _envelope = null; }
        }

        /// <summary>
        /// Gets or sets the rendering style of the vector layer.
        /// </summary>
        public new VectorStyle Style
        {
            get { return base.Style as VectorStyle; }
            set { base.Style = value; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the layer envelope should be treated as static or not.
        /// </summary>
        /// <remarks>
        /// When CacheExtent is enabled the layer Envelope will be calculated only once from DataSource, this 
        /// helps to speed up the Envelope calculation with some DataProviders. Default is false for backward
        /// compatibility.
        /// </remarks>
        public virtual bool CacheExtent { get; set; }

        /// <summary>
        /// Returns the extent of the layer
        /// </summary>
        /// <returns>Bounding box corresponding to the extent of the features in the layer</returns>
        public override Envelope Envelope
        {
            get
            {
                if (DataSource == null)
                    throw (new ApplicationException("DataSource property not set on layer '" + LayerName + "'"));

                if (_envelope != null && CacheExtent)
                    return ToTarget(_envelope.Clone());

                Envelope box;
                lock (_dataSource)
                {
                    bool wasOpen = DataSource.IsOpen;
                    if (!wasOpen)
                        DataSource.Open();
                    box = DataSource.GetExtents();
                    if (!wasOpen) //Restore state
                        DataSource.Close();

                    if (CacheExtent)
                        _envelope = box;
                }

                return ToTarget(box);
            }
        }

        /// <summary>
        /// Gets or sets the SRID of this VectorLayer's data source
        /// </summary>
        public override int SRID
        {
            get
            {
                if (DataSource == null)
                    throw (new ApplicationException("DataSource property not set on layer '" + LayerName + "'"));

                return DataSource.SRID;
            }
            set { DataSource.SRID = value; }
        }

        #region IDisposable Members

        /// <summary>
        /// Disposes the object
        /// </summary>
        protected override void ReleaseManagedResources()
        {
            ClearCache();
            if (DataSource != null)
                DataSource.Dispose();
            base.ReleaseManagedResources();
        }

        #endregion

        /// <summary>
        /// Renders the layer to a graphics object
        /// </summary>
        /// <param name="g">Graphics object reference</param>
        /// <param name="map">Map which is rendered</param>
        /// 
        public override void Render(Graphics g, IMapViewPort map)
        {
            if (map.Center == null)
                throw (new ApplicationException("Cannot render map. View center not specified"));

            var oldSmoothingMode = g.SmoothingMode;

            g.SmoothingMode = SmoothingMode;
            var envelope = ToSource(map.Envelope); //View to render

            if (DataSource == null)
                throw (new ApplicationException("DataSource property not set on layer '" + LayerName + "'"));

            //If thematics is enabled, we use a slighty different rendering approach
            if (Theme != null)
                RenderInternal(g, map, envelope, Theme);
            else
                RenderInternal(g, map, envelope);

            g.SmoothingMode = oldSmoothingMode;

            base.Render(g, map);
        }

        public override void CleanupRendering()
        {
            //ClearCache();
            base.CleanupRendering();
        }

        /// <summary>
        /// Method to render this layer to the map, applying <paramref name="theme"/>.
        /// </summary>
        /// <param name="g">The graphics object</param>
        /// <param name="map">The map object</param>
        /// <param name="envelope">The envelope to render</param>
        /// <param name="theme">The theme to apply</param>
        protected virtual void RenderInternal(Graphics g, IMapViewPort map, Envelope envelope, ITheme theme)
        {
            var useCache = _dataCache != null;
            FeatureDataSet ds;
            if (!useCache)
            {
                ds = new FeatureDataSet();
                lock (_dataSource)
                {
                    DataSource.Open();
                    DataSource.ExecuteIntersectionQuery(envelope, ds);
                    DataSource.Close();
                }
            }
            else
            {
                ds = _dataCache;
            }

            try
            {
                foreach (var features in ds.Tables)
                {
  

                    //Linestring outlines is drawn by drawing the layer once with a thicker line
                    //before drawing the "inline" on top.
                    if (Style.EnableOutline)
                    {

                        for (var i = 0; i < features.Count; i++)
                        {
                            var feature = features[i];
                            using (var outlineStyle = theme.GetStyle(feature) as VectorStyle)
                            {
                                ApplyStyle(g, map, outlineStyle, (graphics, map1, style) =>
                                  {
                                      //Draw background of all line-outlines first
                                      var lineString = feature.Geometry as ILineString;
                                      if (lineString != null)
                                      {
                                          VectorRenderer.DrawLineString(g, lineString, style.Outline,
                                              map, style.LineOffset);
                                      }
                                      else if (feature.Geometry is IMultiLineString)
                                      {
                                          VectorRenderer.DrawMultiLineString(g, (IMultiLineString)feature.Geometry,
                                              style.Outline, map, style.LineOffset);
                                      }
                                  }
                                  );

                            }
                        }
                    }


                    for (var i = 0; i < features.Count; i++)
                    {
                        var feature = features[i];
                        var style = theme.GetStyle(feature);

                        ApplyStyle(g, map, style, (graphics, map1, vstyle) => RenderGeometry(g, map, feature.Geometry, vstyle));
                    }
                }
            }
            finally
            {
                if (!useCache)
                {
                    ds.Dispose();
                }
            }
        }

        /// <summary>
        /// Method to render this layer to the map, applying <see cref="Style"/>.
        /// </summary>
        /// <param name="g">The graphics object</param>
        /// <param name="map">The map object</param>
        /// <param name="envelope">The envelope to render</param>
        protected virtual void RenderInternal(Graphics g, IMapViewPort map, Envelope envelope)
        {
            //if style is not enabled, we don't need to render anything
            if (!Style.Enabled) return;

            var stylesToRender = GetStylesToRender(Style);

            if (stylesToRender == null)
                return;

            var useCache = _dataCache != null;
            FeatureDataSet ds;
            if (!useCache)
            {
                ds = new FeatureDataSet();
                lock (_dataSource)
                {
                    DataSource.Open();
                    DataSource.ExecuteIntersectionQuery(envelope, ds);
                    DataSource.Close();
                }
            }
            else
            {
                ds = _dataCache;
            }

            try
            {

                foreach (var features in ds.Tables)
                {
                    ApplyStyle(g, map, Style, (graphics, map1, vStyle) =>
                    {
                        if (vStyle.LineSymbolizer != null)
                        {
                            vStyle.LineSymbolizer.Begin(g, map,features.Count);
                        }
                        else
                        {
                            //Linestring outlines is drawn by drawing the layer once with a thicker line
                            //before drawing the "inline" on top.
                            if (vStyle.EnableOutline)
                            {
                                for (var i = 0;
                                    i < features.Count;
                                    i++)
                                {
                                    var geom = features[i].Geometry;
                                    if (geom == null) continue;

                                    //Draw background of all line-outlines first
                                    var line = geom as ILineString;
                                    if (line != null)
                                        VectorRenderer
                                            .DrawLineString(g,
                                                line,
                                                vStyle.Outline,
                                                map,
                                                vStyle
                                                    .LineOffset);
                                    else if (geom is IMultiLineString)
                                        VectorRenderer.DrawMultiLineString(g, (IMultiLineString)geom,
                                                vStyle.Outline, map, vStyle.LineOffset);
                                }
                            }
                        }

                        for (var i = 0; i < features.Count; i++)
                        {
                            var geom = features[i].Geometry;
                            if (geom != null)
                            {
                                RenderGeometry(g, map, geom, vStyle);
                            }
                        }

                        if (vStyle.LineSymbolizer != null)
                        {
                            vStyle.LineSymbolizer.Symbolize(g, map);
                            vStyle.LineSymbolizer.End(g, map);
                        }

                    });
                }
            }
            finally
            {
                if (!useCache)
                {
                    ds.Dispose();
                }
            }
        }

        /// <summary>
        /// Unpacks styles to render (can be nested group-styles)
        /// </summary>
        /// <param name="style"></param>
        /// <returns></returns>
        protected static IEnumerable<IStyle> GetStylesToRender(IStyle style)
        {
            IStyle[] stylesToRender = null;
            var groupStyle = style as GroupStyle;
            if (groupStyle != null)
            {
                var gs = groupStyle;
                var styles = new List<IStyle>();
                for (var i = 0; i < gs.Count; i++)
                {
                    styles.AddRange(GetStylesToRender(gs[i]));
                }
                stylesToRender = styles.ToArray();
            }
            else if (style is VectorStyle)
            {
                stylesToRender = new[] { style };
            }

            return stylesToRender;
        }

        /// <summary>
        /// Aplly style 
        /// </summary>
        /// <param name="g"></param>
        /// <param name="map"></param>
        /// <param name="style"></param>
        /// <param name="action"></param>
        protected static void ApplyStyle(Graphics g, IMapViewPort map, IStyle style, Action<Graphics, IMapViewPort, VectorStyle> action)
        {
            if (style == null) return;
            if (!style.Enabled) return;

            var scale = map.MapScale;
            var zoom = map.Zoom;
            var compare = style.VisibilityUnits == VisibilityUnits.ZoomLevel ? zoom : scale;
            if (style.MinVisible > compare || compare > style.MaxVisible) return;

            var groupStyle = style as GroupStyle;
            var vectorStyle = style as VectorStyle;

            if (groupStyle != null)
            {
                for (var i = 0; i < groupStyle.Count; i++)
                {
                    ApplyStyle(g, map, style, action);
                }
            }
            else if (vectorStyle != null)
            {
                action(g, map, vectorStyle);
            }
        }

        /// <summary>
        /// Method to render <paramref name="feature"/> using <paramref name="style"/>
        /// </summary>
        /// <param name="g">The graphics object</param>
        /// <param name="map">The map</param>
        /// <param name="feature">The feature's geometry</param>
        /// <param name="style">The style to apply</param>
        protected void RenderGeometry(Graphics g, IMapViewPort map, IGeometry feature, VectorStyle style)
        {
            if (feature == null)
                return;

            var geometryType = feature.OgcGeometryType;
            switch (geometryType)
            {
                case OgcGeometryType.Polygon:
                    VectorRenderer.DrawPolygon(g, (IPolygon)feature, style.Fill,
                        style.EnableOutline ? style.Outline : null, _clippingEnabled,
                        map);
                    break;
                case OgcGeometryType.MultiPolygon:
                    VectorRenderer.DrawMultiPolygon(g, (IMultiPolygon)feature, style.Fill,
                        style.EnableOutline ? style.Outline : null,
                        _clippingEnabled, map);
                    break;
                case OgcGeometryType.LineString:
                    if (style.LineSymbolizer != null)
                    {
                        style.LineSymbolizer.Render(map, (ILineString)feature, g);
                        return;
                    }
                    VectorRenderer.DrawLineString(g, (ILineString)feature, style.Line, map, style.LineOffset);
                    return;
                case OgcGeometryType.MultiLineString:
                    if (style.LineSymbolizer != null)
                    {
                        style.LineSymbolizer.Render(map, (IMultiLineString)feature, g);
                        return;
                    }
                    VectorRenderer.DrawMultiLineString(g, (IMultiLineString)feature, style.Line, map, style.LineOffset);
                    break;
                case OgcGeometryType.Point:
                    if (style.PointSymbolizer != null)
                    {
                        VectorRenderer.DrawPoint(style.PointSymbolizer, g, (IPoint)feature, map);
                        return;
                    }

                    if (style.Symbol != null || style.PointColor == null)
                    {
                        VectorRenderer.DrawPoint(g, (IPoint)feature, style.Symbol, style.SymbolScale, style.SymbolOffset,
                                                 style.SymbolRotation, map);
                        return;
                    }
                    VectorRenderer.DrawPoint(g, (IPoint)feature, style.PointColor, style.PointSize, style.SymbolOffset, map);

                    break;
                case OgcGeometryType.MultiPoint:
                    if (style.PointSymbolizer != null)
                    {
                        VectorRenderer.DrawMultiPoint(style.PointSymbolizer, g, (IMultiPoint)feature, map);
                    }
                    if (style.Symbol != null || style.PointColor == null)
                    {
                        VectorRenderer.DrawMultiPoint(g, (IMultiPoint)feature, style.Symbol, style.SymbolScale,
                                                  style.SymbolOffset, style.SymbolRotation, map);
                    }
                    else
                    {
                        VectorRenderer.DrawMultiPoint(g, (IMultiPoint)feature, style.PointColor, style.PointSize, style.SymbolOffset, map);
                    }
                    break;
                case OgcGeometryType.GeometryCollection:
                    var coll = (IGeometryCollection)feature;
                    for (var i = 0; i < coll.NumGeometries; i++)
                    {
                        var geom = coll[i];
                        RenderGeometry(g, map, geom, style);
                    }
                    break;
                default:
                    break;
            }
        }

        #region Implementation of ICanQueryLayer

        /// <summary>
        /// Returns the data associated with all the geometries that are intersected by 'geom'
        /// </summary>
        /// <param name="box">Geometry to intersect with</param>
        /// <param name="ds">FeatureDataSet to fill data into</param>
        public void ExecuteIntersectionQuery(Envelope box, FeatureDataSet ds)
        {
            box = ToSource(box);

            if (_dataCache != null)
            {
                throw new NotImplementedException();
            }
            else
            {
                lock (_dataSource)
                {
                    _dataSource.Open();
                    var tableCount = ds.Tables.Count;
                    _dataSource.ExecuteIntersectionQuery(box, ds);
                    if (ds.Tables.Count > tableCount)
                    {
                        //We added a table, name it according to layer
                        ds.Tables[ds.Tables.Count - 1].TableName = LayerName;
                    }
                    _dataSource.Close();
                }
            }
        }

        /// <summary>
        /// Returns the data associated with all the geometries that are intersected by 'geom'
        /// </summary>
        /// <param name="geometry">Geometry to intersect with</param>
        /// <param name="ds">FeatureDataSet to fill data into</param>
        public void ExecuteIntersectionQuery(IGeometry geometry, FeatureDataSet ds)
        {
            geometry = ToSource(geometry);
            if (_dataCache != null)
            {
                throw new NotImplementedException();
            }
            else
            {
                lock (_dataSource)
                {
                    var open = !_dataSource.IsOpen;
                    if (open) _dataSource.Open();
                    var tableCount = ds.Tables.Count;
                    _dataSource.ExecuteIntersectionQuery(geometry, ds);
                    if (ds.Tables.Count > tableCount)
                    {
                        //We added a table, name it according to layer
                        ds.Tables[ds.Tables.Count - 1].TableName = LayerName;
                    }
                    if (open) _dataSource.Close();
                }
            }
        }

        /// <summary>
        /// Whether the layer is queryable when used in a SharpMap.Web.Wms.WmsServer, ExecuteIntersectionQuery() will be possible in all other situations when set to FALSE
        /// </summary>
        public bool IsQueryEnabled
        {
            get { return _isQueryEnabled; }
            set { _isQueryEnabled = value; }
        }

        #endregion

        #region Data loading

        /// <summary>
        /// The feature data cache.
        /// </summary>
        protected FeatureDataSet _dataCache;

        protected bool IsFetching;
        protected bool NeedsUpdate = true;
        protected Envelope NewEnvelope;
        private int FetchingPostponedInMilliseconds { get; set; }
        private Timer StartFetchTimer;

        /// <summary>
        /// Called when data has been loaded.
        /// </summary>
        protected override void OnLayerDataLoaded()
        {
            if (DataChanged != null)
            {
                DataChanged(this, new DataChangedEventArgs(null, false, LayerName, this));
            }
            base.OnLayerDataLoaded();
        }

        /// <summary>
        /// Abords fetching.
        /// </summary>
        public void AbortFetch()
        {
            //throw new NotImplementedException();
        }

        /// <summary>
        /// Reload datas for current enveloppe.
        /// </summary>
        public virtual void LoadDatas()
        {

        }

        /// <summary>
        /// Load datas.
        /// </summary>
        /// <param name="view"></param>
        public override void LoadDatas(IMapViewPort view)
        {
            NewEnvelope = view.Envelope;

            if (IsFetching)
            {
                NeedsUpdate = true;
                return;
            }

            if (StartFetchTimer != null) StartFetchTimer.Dispose();
            StartFetchTimer = new Timer(StartFetchTimerElapsed, null, FetchingPostponedInMilliseconds, int.MaxValue);
        }

        /// <summary>
        /// Event Raised when data are loaded.
        /// </summary>
        public event DataChangedEventHandler DataChanged;

        /// <summary>
        /// Clears the cache.
        /// </summary>
        public void ClearCache()
        {
            var oldDatas = _dataCache;
            _dataCache = null;
            if (oldDatas != null)
            {
                oldDatas.Dispose();
            }
        }

        void StartFetchTimerElapsed(object state)
        {
            if (NewEnvelope == null) return;
            LoadDatas(NewEnvelope);
            StartFetchTimer.Dispose();
        }

        /// <summary>
        /// Loads the datas.
        /// </summary>
        /// <param name="envelope"></param>
        protected void LoadDatas(Envelope envelope)
        {
            if (IsDisposed) return;
            IsFetching = true;
            NeedsUpdate = false;
            var newenvelope = ToSource(envelope);
            var ds = new FeatureDataSet();

            var fetcher = new FeaturesFetcher(newenvelope, ds, _dataSource, DataArrived);
            Task.Factory.StartNew(() => fetcher.FetchOnThread(null));
        }

        /// <summary>
        /// Called when data are loaded.
        /// </summary>
        /// <param name="ds"></param>
        /// <param name="state"></param>
        /// <param name="error"></param>
        protected void DataArrived(FeatureDataSet ds, object state = null, Exception error = null)
        {
            if (ds == null) throw new ArgumentException("argument features may not be null");

            try
            {
                // Transform geometries if necessary
                if (CoordinateTransformation != null)
                {
                    foreach (var features in ds.Tables)
                    {
                        for (var i = 0; i < features.Count; i++)
                        {
                            features[i].Geometry = ToTarget(features[i].Geometry);
                        }
                    }
                }

                var oldDatas = _dataCache;
                _dataCache = ds;

                OnLayerDataLoaded();

                if (oldDatas != null)
                {
                    oldDatas.Dispose();
                }
                IsFetching = false;
                if (NeedsUpdate) LoadDatas(NewEnvelope);
            }
            catch (InvalidOperationException ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
            }

        }

        /// <summary>
        /// Gets the datas
        /// </summary>
        /// <returns></returns>
        public FeatureDataSet GetDatas()
        {
            return _dataCache;
        }

        #endregion
    }
}