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
using System.Drawing;
#if !DotSpatialProjections
using GeoAPI.CoordinateSystems.Transformations;
#else
using DotSpatial.Projections;
#endif
using GeoAPI.Geometries;
using SharpMap.Base;
using SharpMap.Styles;
using Style = SharpMap.Styles.Style;

namespace SharpMap.Layers
{
    /// <summary>
    /// Abstract class for common layer properties
    /// Implement this class instead of the ILayer interface to save a lot of common code.
    /// </summary>
    [Serializable]
    public abstract partial class Layer : DisposableObject, ILayer
    {
        #region Events

        #region Delegates

        /// <summary>
        /// EventHandler for event fired when the layer has been rendered
        /// </summary>
        /// <param name="layer">Layer rendered</param>
        /// <param name="g">Reference to graphics object used for rendering</param>
        public delegate void LayerRenderedEventHandler(Layer layer, Graphics g);

        /// <summary>
        /// EventHandler for event fired whenthe layer datas has been loaded
        /// </summary>
        /// <param name="layer">Layer data loaded</param>
        public delegate void LayerDataLoadedEventHandler(Layer layer);

        #endregion

        /// <summary>
        /// Event fired when the layer has been rendered
        /// </summary>
        public event LayerRenderedEventHandler LayerRendered;

        /// <summary>
        /// Event fired when data has been loaded
        /// </summary>
        public event LayerDataLoadedEventHandler LayerDataLoaded;

        /// <summary>
        /// Event raised when the layer's <see cref="SRID"/> property has changed
        /// </summary>
        public event EventHandler SRIDChanged;

        /// <summary>
        /// Method called when <see cref="SRID"/> has changed, to invoke <see cref="E:SharpMap.Layers.Layer.SRIDChanged"/>
        /// </summary>
        /// <param name="eventArgs">The arguments associated with the event</param>
        protected virtual void OnSridChanged(EventArgs eventArgs)
        {
            _sourceFactory = GeoAPI.GeometryServiceProvider.Instance.CreateGeometryFactory(SRID);

            SRIDChanged?.Invoke(this, eventArgs);
        }

        /// <summary>
        /// Event raised when the layer's <see cref="Style"/> property has changed
        /// </summary>
        public event EventHandler StyleChanged;

        /// <summary>
        /// Method called when <see cref="Style"/> has changed, to invoke <see cref="E:SharpMap.Layers.Layer.StyleChanged"/>
        /// </summary>
        /// <param name="eventArgs">The arguments associated with the event</param>
        protected virtual void OnStyleChanged(EventArgs eventArgs)
        {
            StyleChanged?.Invoke(this, eventArgs);
        }
        
        /// <summary>
        /// Event raised when the layers's <see cref="LayerName"/> property has changed
        /// </summary>
        public event EventHandler LayerNameChanged;

        /// <summary>
        /// Method called when <see cref="LayerName"/> has changed, to invoke <see cref="E:SharpMap.Layers.Layer.LayerNameChanged"/>
        /// </summary>
        /// <param name="eventArgs">The arguments associated with the event</param>
        protected virtual void OnLayerNameChanged(EventArgs eventArgs)
        {
            LayerNameChanged?.Invoke(this, eventArgs);
        }

        #endregion

        private ICoordinateTransformation _coordinateTransform;
        private ICoordinateTransformation _reverseCoordinateTransform;
        private IGeometryFactory _sourceFactory;
        private IGeometryFactory _targetFactory;

        private object _tag;
        private string _layerName;
        private IStyle _style;
        private int _srid = -1;
        private int? _targetSrid;

        ///<summary>
        /// Creates an instance of this class using the given Style
        ///</summary>
        ///<param name="style"></param>
        protected Layer(IStyle style)
        {
            _style = style;
        }

        /// <summary>
        /// Creates an instance of this class
        /// </summary>
        protected Layer()
        {
            _style = new Style();
        }

        /// <summary>
        /// Releases managed resources
        /// </summary>
        protected override void ReleaseManagedResources()
        {
            _coordinateTransform = null;
            _reverseCoordinateTransform = null;
            _style = null;

            base.ReleaseManagedResources();
        }

#if !DotSpatialProjections
        /// <summary>
        /// Gets or sets the <see cref="GeoAPI.CoordinateSystems.Transformations.ICoordinateTransformation"/> applied 
        /// to this vectorlayer prior to rendering
        /// </summary>
#else
        /// <summary>
        /// Gets or sets the <see cref="DotSpatial.Projections.ICoordinateTransformation"/> applied 
        /// to this vectorlayer prior to rendering
        /// </summary>
#endif
        public virtual ICoordinateTransformation CoordinateTransformation
        {
            get { return _coordinateTransform; }
            set
            {
                if (value == _coordinateTransform)
                    return;
                _coordinateTransform = value;
                OnCoordinateTransformationChanged(EventArgs.Empty);
            }
        }

        /// <summary>
        /// Event raised when the <see cref="CoordinateTransformation"/> has changed
        /// </summary>
        public event EventHandler CoordinateTransformationChanged;

        /// <summary>
        /// Event invoker for the <see cref="CoordinateTransformationChanged"/> event
        /// </summary>
        /// <param name="e">The event's arguments</param>
        protected virtual void OnCoordinateTransformationChanged(EventArgs e)
        {
            _sourceFactory = _targetFactory = GeoAPI.GeometryServiceProvider.Instance
                .CreateGeometryFactory(SRID);

#if !DotSpatialProjections
            if (CoordinateTransformation != null)
            {
                SRID = Convert.ToInt32(CoordinateTransformation.SourceCS.AuthorityCode);
                TargetSRID = Convert.ToInt32(CoordinateTransformation.TargetCS.AuthorityCode);
            }
#endif
            CoordinateTransformationChanged?.Invoke(this, e);
        }

        /// <summary>
        /// Gets the geometry factory to create source geometries
        /// </summary>
        protected internal IGeometryFactory SourceFactory => _sourceFactory ?? (_sourceFactory = GeoAPI.GeometryServiceProvider.Instance.CreateGeometryFactory(SRID));

        /// <summary>
        /// Gets the geometry factory to create target geometries
        /// </summary>
        protected internal IGeometryFactory TargetFactory => _targetFactory ?? _sourceFactory;

#if !DotSpatialProjections
        /// <summary>
        /// Certain Transformations cannot be inverted in ProjNet, in those cases use this property to set the reverse <see cref="GeoAPI.CoordinateSystems.Transformations.ICoordinateTransformation"/> (of CoordinateTransformation) to fetch data from Datasource
        /// 
        /// If your CoordinateTransformation can be inverted you can leave this property to null
        /// </summary>
        public virtual ICoordinateTransformation ReverseCoordinateTransformation
        {
            get { return _reverseCoordinateTransform; }
            set { _reverseCoordinateTransform = value; }
        }
#endif

        #region ILayer Members

        /// <summary>
        /// Gets or sets the name of the layer
        /// </summary>
        public string LayerName
        {
            get { return _layerName; }
            set { _layerName = value; OnLayerNameChanged(EventArgs.Empty); }
        }

        /// <summary>
        /// The spatial reference ID (CRS)
        /// </summary>
        public virtual int SRID
        {
            get { return _srid; }
            set
            {
                if (value != _srid)
                {
                    _srid = value;
                    OnSridChanged(EventArgs.Empty);
                }
            }
        }

        /// <summary>
        /// Gets or sets a value indicating the target spatial reference id
        /// </summary>
        public virtual int TargetSRID
        {
            get { return _targetSrid.HasValue ? _targetSrid.Value : SRID; }
            set
            {
                _targetSrid = value;
                _targetFactory = GeoAPI.GeometryServiceProvider.Instance.CreateGeometryFactory(value);
            }
        }

        /// <summary>
        /// Cleanup the rendering.
        /// </summary>
        public virtual void CleanupRendering()
        {
        }

        /// <summary>
        /// Renders the layer
        /// </summary>
        /// <param name="g">Graphics object reference</param>
        /// <param name="map">Map which is rendered</param>
        public virtual void Render(Graphics g, IMapViewPort map)
        {
            OnLayerRendered(g);
        }

        /// <summary>
        /// Event invoker for the <see cref="LayerRendered"/> event.
        /// </summary>
        /// <param name="g">The graphics object</param>
        protected virtual void OnLayerRendered(Graphics g)
        {
            LayerRendered?.Invoke(this, g);
        }

        /// <summary>
        /// Returns the extent of the layer
        /// </summary>
        /// <returns>Bounding box corresponding to the extent of the features in the layer</returns>
        public abstract Envelope Envelope { get; }

        #endregion

        #region Data Loading

        /// <summary>
        /// Loads the datas
        /// </summary>
        /// <param name="view">The map view port.</param>
        public virtual void LoadDatas(IMapViewPort view)
        {
            OnLayerDataLoaded();
        }

        /// <summary>
        /// Method called when Data has been loaded, to invoke <see cref="E:SharpMap.Layers.Layer.LayerDataLoaded"/>
        /// </summary>
        protected virtual void OnLayerDataLoaded()
        {
            LayerDataLoaded?.Invoke(this);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Proj4 projection definition string
        /// </summary>
        public string Proj4Projection { get; set; }

        /// <summary>
        /// Minimum visibility zoom, including this value
        /// </summary>
        public double MinVisible
        {
            get
            {
                return _style.MinVisible;
            }
            set
            {
                _style.MinVisible = value;
            }
        }

        /// <summary>
        /// Maximum visibility zoom, excluding this value
        /// </summary>
        public double MaxVisible
        {
            get
            {

                return _style.MaxVisible;
            }
            set
            {

                _style.MaxVisible = value;
            }
        }

        /// <summary>
        /// Gets or Sets what kind of units the Min/Max visible properties are defined in
        /// </summary>
        public VisibilityUnits VisibilityUnits
        {
            get
            {
                return _style.VisibilityUnits;
            }
            set
            {
                _style.VisibilityUnits = value;
            }
        }

        /// <summary>
        /// Specified whether the layer is rendered or not
        /// </summary>
        public bool Enabled
        {
            get
            {
                //return _Enabled;
                return _style != null && _style.Enabled;
            }
            set
            {
                if (_style == null)
                    return;
                if (_style.Enabled != value)
                {
                    //_Enabled = value;
                    _style.Enabled = value;
                    OnStyleChanged(EventArgs.Empty);
                }
            }
        }

        /// <summary>
        /// Gets or sets the Style for this Layer
        /// </summary>
        public virtual IStyle Style
        {
            get { return _style; }
            set
            {
                if (value != _style && !_style.Equals(value))
                {
                    _style = value;
                    OnStyleChanged(EventArgs.Empty);
                }
            }
        }

        /// <summary>
        /// Gets or sets an arbitrary object value that can be used to store custom information about this element
        /// </summary>
        public object Tag
        {
            get { return _tag; }
            set
            {
                _tag = value;
            }
        }

        #endregion

        /// <summary>
        /// Returns the name of the layer.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return LayerName;
        }

        #region Reprojection utility functions

        /// <summary>
        /// Utility function to transform given envelope using a specific transformation
        /// </summary>
        /// <param name="envelope">The source envelope</param>
        /// <param name="coordinateTransformation">The <see cref="GeoAPI.CoordinateSystems.Transformations.ICoordinateTransformation"/> to use.</param>
        /// <returns>The target envelope</returns>
        protected virtual Envelope ToTarget(Envelope envelope, ICoordinateTransformation coordinateTransformation)
        {
            if (coordinateTransformation == null)
            {
                return envelope;
            }
#if !DotSpatialProjections
            return GeometryTransform.TransformBox(envelope, coordinateTransformation.MathTransform);
#else
            return GeometryTransform.TransformBox(envelope, coordinateTransformation.Source, coordinateTransformation.Target);
#endif
        }

        /// <summary>
        /// Utility function to transform given envelope to the target envelope
        /// </summary>
        /// <param name="envelope">The source envelope</param>
        /// <returns>The target envelope</returns>
        protected Envelope ToTarget(Envelope envelope)
        {
            return ToTarget(envelope, CoordinateTransformation);
        }

        /// <summary>
        /// Utility function to transform given envelope to the source envelope
        /// </summary>
        /// <param name="envelope">The target envelope</param>
        /// <returns>The source envelope</returns>
        protected virtual Envelope ToSource(Envelope envelope)
        {
#if !DotSpatialProjections
            if (ReverseCoordinateTransformation != null)
            {
                return GeometryTransform.TransformBox(envelope, ReverseCoordinateTransformation.MathTransform);
            }
#endif
            if (CoordinateTransformation != null)
            {
#if !DotSpatialProjections
                var mt = CoordinateTransformation.MathTransform;
                mt.Invert();
                var res = GeometryTransform.TransformBox(envelope, mt);
                mt.Invert();
                return res;
#else
                return GeometryTransform.TransformBox(envelope, CoordinateTransformation.Target, CoordinateTransformation.Source);
#endif
            }

            // no transformation
            return envelope;
        }

        /// <summary>
        /// Utility function to transform given geometry using a specific transformation
        /// </summary>
        /// <param name="geometry">The source geometry.</param>
        /// <returns>The target Geometry.</returns>
        protected virtual IGeometry ToTarget(IGeometry geometry)
        {
            
            if (geometry.SRID == TargetSRID)
                return geometry;

            if (CoordinateTransformation != null)
            {
#if !DotSpatialProjections
                return GeometryTransform.TransformGeometry(geometry, CoordinateTransformation.MathTransform, TargetFactory);
#else
                return GeometryTransform.TransformGeometry(geometry, CoordinateTransformation.Source, CoordinateTransformation.Target, TargetFactory);
#endif
            }

            return geometry;
        }

        /// <summary>
        /// Utility function to transform given geometry to the source geometry
        /// </summary>
        /// <param name="geometry"></param>
        /// <returns></returns>
        protected virtual IGeometry ToSource(IGeometry geometry)
        {
            

            if (geometry.SRID == SRID)
                return geometry;

#if !DotSpatialProjections
            if (ReverseCoordinateTransformation != null)
            {
                return GeometryTransform.TransformGeometry(geometry,
                    ReverseCoordinateTransformation.MathTransform, SourceFactory);
            }
#endif
            if (CoordinateTransformation != null)
            {
#if !DotSpatialProjections
                var mt = CoordinateTransformation.MathTransform;
                mt.Invert();
                var res = GeometryTransform.TransformGeometry(geometry, mt, SourceFactory);
                mt.Invert();
                return res;
#else
                return GeometryTransform.TransformGeometry(geometry, CoordinateTransformation.Target, CoordinateTransformation.Source, SourceFactory);
#endif
            }

            return geometry;
        }

        #endregion
    }
}