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
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using SharpMap.Data;
using SharpMap.Data.Providers;
using GeoAPI.Geometries;
using SharpMap.Rendering;
using SharpMap.Rendering.Thematics;
using SharpMap.Styles;
using Transform = SharpMap.Utilities.Transform;
using SharpMap.Rendering.Symbolizer;
using SharpMap.Fetching;
using System.Threading;
using System.Threading.Tasks;

namespace SharpMap.Layers
{
    /// <summary>
    /// Label layer class
    /// </summary>
    /// <example>
    /// Creates a new label layer and sets the label text to the "Name" column in the FeatureDataTable of the datasource
    /// <code lang="C#">
    /// //Set up a label layer
    /// SharpMap.Layers.LabelLayer layLabel = new SharpMap.Layers.LabelLayer("Country labels");
    /// layLabel.DataSource = layCountries.DataSource;
    /// layLabel.Enabled = true;
    /// layLabel.LabelColumn = "Name";
    /// layLabel.Style = new SharpMap.Styles.LabelStyle();
    /// layLabel.Style.CollisionDetection = true;
    /// layLabel.Style.CollisionBuffer = new SizeF(20, 20);
    /// layLabel.Style.ForeColor = Color.White;
    /// layLabel.Style.Font = new Font(FontFamily.GenericSerif, 8);
    /// layLabel.MaxVisible = 90;
    /// layLabel.Style.HorizontalAlignment = SharpMap.Styles.LabelStyle.HorizontalAlignmentEnum.Center;
    /// </code>
    /// </example>
    [Serializable]
    public class LabelLayer : Layer, IAsyncDataFetcher
    {
        #region Delegates

        /// <summary>
        /// Delegate method for creating advanced label texts
        /// </summary>
        /// <param name="fdr">the <see cref="FeatureDataRow"/> to build the label for</param>
        /// <returns>the label</returns>
        public delegate string GetLabelMethod(FeatureDataRow fdr);

        /// <summary>
        /// Delegate method for calculating the priority of label rendering
        /// </summary>
        /// <param name="fdr">the <see cref="FeatureDataRow"/> to compute the priority value from</param>
        /// <returns>the priority value</returns>
        public delegate int GetPriorityMethod(FeatureDataRow fdr);

        /// <summary>
        /// Delegate method for advanced placement of the label position
        /// </summary>
        /// <param name="fdr">the <see cref="FeatureDataRow"/> to compute the label position from</param>
        /// <returns>the priority value</returns>
        public delegate Coordinate GetLocationMethod(FeatureDataRow fdr);
        #endregion

        #region MultipartGeometryBehaviourEnum enum

        /// <summary>
        /// Labelling behaviour for Multipart geometry collections
        /// </summary>
        public enum MultipartGeometryBehaviourEnum
        {
            /// <summary>
            /// Place label on all parts (default)
            /// </summary>
            All,
            /// <summary>
            /// Place label on object which the greatest length or area.
            /// </summary>
            /// <remarks>
            /// Multipoint geometries will default to <see cref="First"/>
            /// </remarks>
            Largest,
            /// <summary>
            /// The center of the combined geometries
            /// </summary>
            CommonCenter,
            /// <summary>
            /// Center of the first geometry in the collection (fastest method)
            /// </summary>
            First
        }

        #endregion

        private IProvider _dataSource;
        private GetLabelMethod _getLabelMethod;
        private GetPriorityMethod _getPriorityMethod;
        private GetLocationMethod _getLocationMethod;

        /// <summary>
        /// Name of the column that holds the value for the label.
        /// </summary>
        private string _labelColumn;

        /// <summary>
        /// Delegate for custom Label Collision Detection
        /// </summary>
        private LabelCollisionDetection.LabelFilterMethod _labelFilter;

        /// <summary>
        /// A value indication the priority of the label in cases of label-collision detection
        /// </summary>
        private int _priority;

        /// <summary>
        /// Name of the column that contains the value indicating the priority of the label in case of label-collision detection
        /// </summary>
        private string _priorityColumn = "";

        /// <summary>
        /// Name of the column that contains the value indicating the rotation value of the label
        /// </summary>
        private string _rotationColumn;


        private TextRenderingHint _textRenderingHint;

        private ITheme _theme;

        public event DataChangedEventHandler DataChanged;

        /// <summary>
        /// Creates a new instance of a LabelLayer
        /// </summary>
        public LabelLayer(string layername)
            :base(new LabelStyle())
        {
            //_Style = new LabelStyle();
            LayerName = layername;
            SmoothingMode = SmoothingMode.AntiAlias;
            TextRenderingHint = TextRenderingHint.AntiAlias;
            MultipartGeometryBehaviour = MultipartGeometryBehaviourEnum.All;
            _labelFilter = LabelCollisionDetection.SimpleCollisionDetection;
        }

        /// <summary>
        /// Gets or sets labelling behavior on multipart geometries
        /// </summary>
        /// <remarks>Default value is <see cref="MultipartGeometryBehaviourEnum.All"/></remarks>
        public MultipartGeometryBehaviourEnum MultipartGeometryBehaviour { get; set; }

        /// <summary>
        /// Filtermethod delegate for performing filtering
        /// </summary>
        /// <remarks>
        /// Default method is <see cref="SharpMap.Rendering.LabelCollisionDetection.SimpleCollisionDetection"/>
        /// </remarks>
        public LabelCollisionDetection.LabelFilterMethod LabelFilter
        {
            get { return _labelFilter; }
            set { _labelFilter = value; }
        }


        /// <summary>
        /// Render whether smoothing (antialiasing) is applied to lines and curves and the edges of filled areas
        /// </summary>
        public SmoothingMode SmoothingMode { get; set; }

        /// <summary>
        /// Specifies the quality of text rendering
        /// </summary>
        public TextRenderingHint TextRenderingHint
        {
            get { return _textRenderingHint; }
            set { _textRenderingHint = value; }
        }

        /// <summary>
        /// Gets or sets the datasource
        /// </summary>
        public IProvider DataSource
        {
            get { return _dataSource; }
            set { _dataSource = value; }
        }

        /// <summary>
        /// Gets or sets the rendering style of the label layer.
        /// </summary>
        public new LabelStyle Style
        {
            get { return base.Style as LabelStyle; }
            set { base.Style = value; }
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
        /// Data column or expression where label text is extracted from.
        /// </summary>
        /// <remarks>
        /// This property is overridden by the <see cref="LabelStringDelegate"/>.
        /// </remarks>
        public string LabelColumn
        {
            get { return _labelColumn; }
            set { _labelColumn = value; }
        }

        /// <summary>
        /// Gets or sets the method for creating a custom label string based on a feature.
        /// </summary>
        /// <remarks>
        /// <para>If this method is not null, it will override the <see cref="LabelColumn"/> value.</para>
        /// <para>The label delegate must take a <see cref="SharpMap.Data.FeatureDataRow"/> and return a string.</para>
        /// <example>
        /// Creating a label-text by combining attributes "ROADNAME" and "STATE" into one string, using
        /// an anonymous delegate:
        /// <code lang="C#">
        /// myLabelLayer.LabelStringDelegate = delegate(SharpMap.Data.FeatureDataRow fdr)
        ///				{ return fdr["ROADNAME"].ToString() + ", " + fdr["STATE"].ToString(); };
        /// </code>
        /// </example>
        /// </remarks>
        public GetLabelMethod LabelStringDelegate
        {
            get { return _getLabelMethod; }
            set { _getLabelMethod = value; }
        }
        /// <summary>
        /// Gets or sets the method for creating a custom position based on a feature.
        /// </summary>
        /// <remarks>
        /// <para>If this method is not null, it will override the position based on the centroid of the boundingbox of the feature </para>
        /// <para>The label delegate must take a <see cref="SharpMap.Data.FeatureDataRow"/> and return a GeoAPI.Geometries.Coordinate.</para>
        /// <para>If the delegate returns a null, the centroid of the feature will be used</para>
        /// <example>
        /// Creating a custom position by using X and Y values from the FeatureDataRow attributes "LabelX" and "LabelY", using
        /// an anonymous delegate:
        /// <code lang="C#">
        /// myLabelLayer.LabelPositionDelegate = delegate(SharpMap.Data.FeatureDataRow fdr)
        ///				{ return new GeoAPI.Geometries.Coordinate(Convert.ToDouble(fdr["LabelX"]), Convert.ToDouble(fdr["LabelY"]));};
        /// </code>
        /// </example>
        /// </remarks>
        public GetLocationMethod LabelPositionDelegate
        {
            get { return _getLocationMethod; }
            set { _getLocationMethod = value; }
        }


        /// <summary>
        /// Gets or sets the method for calculating the render priority of a label based on a feature.
        /// </summary>
        /// <remarks>
        /// <para>If this method is not null, it will override the <see cref="PriorityColumn"/> value.</para>
        /// <para>The label delegate must take a <see cref="SharpMap.Data.FeatureDataRow"/> and return an Int32.</para>
        /// <example>
        /// Creating a priority by combining attributes "capital" and "population" into one value, using
        /// an anonymous delegate:
        /// <code lang="C#">
        /// myLabelLayer.PriorityDelegate = delegate(SharpMap.Data.FeatureDataRow fdr) 
        ///     { 
        ///         Int32 retVal = 100000000 * (Int32)( (String)fdr["capital"] == "Y" ? 1 : 0 );
        ///         return  retVal + Convert.ToInt32(fdr["population"]);
        ///     };
        /// </code>
        /// </example>
        /// </remarks>
        public GetPriorityMethod PriorityDelegate
        {
            get { return _getPriorityMethod; }
            set { _getPriorityMethod = value; }
        }

        /// <summary>
        /// Data column from where the label rotation is derived.
        /// If this is empty, rotation will be zero, or aligned to a linestring.
        /// Rotation are in degrees (positive = clockwise).
        /// </summary>
        public string RotationColumn
        {
            get { return _rotationColumn; }
            set { _rotationColumn = value; }
        }

        /// <summary>
        /// A value indication the priority of the label in cases of label-collision detection
        /// </summary>
        public int Priority
        {
            get { return _priority; }
            set { _priority = value; }
        }

        /// <summary>
        /// Name of the column that holds the value indicating the priority of the label in cases of label-collision detection
        /// </summary>
        public string PriorityColumn
        {
            get { return _priorityColumn; }
            set { _priorityColumn = value; }
        }

        /// <summary>
        /// Gets the boundingbox of the entire layer
        /// </summary>
        public override Envelope Envelope
        {
            get
            {
                var wasOpen = DataSource.IsOpen;
                if (!wasOpen)
                    DataSource.Open();
                var box = DataSource.GetExtents();
                if (!wasOpen) //Restore state
                    DataSource.Close();
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
        /// Releases managed resources
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
        /// Renders the layer
        /// </summary>
        /// <param name="g">Graphics object reference</param>
        /// <param name="map">Map which is rendered</param>
        public override void Render(Graphics g, IMapViewPort map)
        {
            if (DataSource == null)
                throw (new ApplicationException("DataSource property not set on layer '" + LayerName + "'"));
            g.TextRenderingHint = TextRenderingHint;
            g.SmoothingMode = SmoothingMode;

            var mapEnvelope = map.Envelope;
            var layerEnvelope = ToSource(mapEnvelope); //View to render
            var lineClipping = new CohenSutherlandLineClipping(mapEnvelope.MinX, mapEnvelope.MinY,
                mapEnvelope.MaxX, mapEnvelope.MaxY);

            var ds = _dataCache;
                      
            if (ds == null || ds.Tables.Count == 0)
            {
                base.Render(g, map);
                return;
            }

            var features = ds.Tables[0];

            //Initialize label collection
            var labels = new List<BaseLabel>();

            //List<System.Drawing.Rectangle> LabelBoxes; //Used for collision detection
            //Render labels

            for (var i = 0; i < features.Count; i++)
            {
                var feature = features[i];

                LabelStyle style;
                if (Theme != null) //If thematics is enabled, lets override the style
                    style = Theme.GetStyle(feature) as LabelStyle;
                else
                    style = Style;

                var rotationStyle = style?.Rotation ?? 0f;
                var rotationColumn = 0f;
                if (!string.IsNullOrEmpty(RotationColumn))
                    float.TryParse(feature[RotationColumn].ToString(), NumberStyles.Any, Map.NumberFormatEnUs,
                        out rotationColumn);
                var rotation = rotationStyle + rotationColumn;

                var priority = Priority;
                if (_getPriorityMethod != null)
                    priority = _getPriorityMethod(feature);
                else if (!string.IsNullOrEmpty(PriorityColumn))
                    int.TryParse(feature[PriorityColumn].ToString(), NumberStyles.Any, Map.NumberFormatEnUs,
                        out priority);

                string text;
                text = _getLabelMethod != null ? _getLabelMethod(feature) : feature[LabelColumn].ToString();

                if (!string.IsNullOrEmpty(text))
                {
                    // for lineal geometries, try clipping to ensure proper labeling
                    if (feature.Geometry is ILineal)
                    {
                        if (feature.Geometry is ILineString)
                            feature.Geometry = lineClipping.ClipLineString(feature.Geometry as ILineString);
                        else if (feature.Geometry is IMultiLineString)
                            feature.Geometry = lineClipping.ClipLineString(feature.Geometry as IMultiLineString);
                    }

                    if (feature.Geometry is IGeometryCollection)
                    {
                        if (MultipartGeometryBehaviour == MultipartGeometryBehaviourEnum.All)
                        {
                            foreach (var geom in (feature.Geometry as IGeometryCollection))
                            {
                                var lbl = CreateLabel(feature, geom, text, rotation, priority, style, map, g, _getLocationMethod);
                                if (lbl != null)
                                    labels.Add(lbl);
                            }
                        }
                        else if (MultipartGeometryBehaviour == MultipartGeometryBehaviourEnum.CommonCenter)
                        {
                            var lbl = CreateLabel(feature, feature.Geometry, text, rotation, priority, style, map, g, _getLocationMethod);
                            if (lbl != null)
                                labels.Add(lbl);
                        }
                        else if (MultipartGeometryBehaviour == MultipartGeometryBehaviourEnum.First)
                        {
                            if ((feature.Geometry as IGeometryCollection).NumGeometries > 0)
                            {
                                var lbl = CreateLabel(feature, (feature.Geometry as IGeometryCollection).GetGeometryN(0), text,
                                    rotation, style, map, g);
                                if (lbl != null)
                                    labels.Add(lbl);
                            }
                        }
                        else if (MultipartGeometryBehaviour == MultipartGeometryBehaviourEnum.Largest)
                        {
                            var coll = (feature.Geometry as IGeometryCollection);
                            if (coll.NumGeometries > 0)
                            {
                                var largestVal = 0d;
                                var idxOfLargest = 0;
                                for (var j = 0; j < coll.NumGeometries; j++)
                                {
                                    var geom = coll.GetGeometryN(j);
                                    if (geom is ILineString && ((ILineString) geom).Length > largestVal)
                                    {
                                        largestVal = ((ILineString) geom).Length;
                                        idxOfLargest = j;
                                    }
                                    if (geom is IMultiLineString && ((IMultiLineString) geom).Length > largestVal)
                                    {
                                        largestVal = ((IMultiLineString) geom).Length;
                                        idxOfLargest = j;
                                    }
                                    if (geom is IPolygon && ((IPolygon) geom).Area > largestVal)
                                    {
                                        largestVal = ((IPolygon) geom).Area;
                                        idxOfLargest = j;
                                    }
                                    if (geom is IMultiPolygon && ((IMultiPolygon) geom).Area > largestVal)
                                    {
                                        largestVal = ((IMultiPolygon) geom).Area;
                                        idxOfLargest = j;
                                    }
                                }

                                var lbl = CreateLabel(feature, coll.GetGeometryN(idxOfLargest), text, rotation, priority, style,
                                    map, g, _getLocationMethod);
                                if (lbl != null)
                                    labels.Add(lbl);
                            }
                        }
                    }
                    else
                    {
                        var lbl = CreateLabel(feature, feature.Geometry, text, rotation, priority, style, map, g, _getLocationMethod);
                        if (lbl != null)
                            labels.Add(lbl);
                    }
                }
            }
            if (labels.Count > 0) //We have labels to render...
            {
                if (Style.CollisionDetection)
                    _labelFilter?.Invoke(labels);

                for (var i = 0; i < labels.Count; i++)
                {
                    // Don't show the label if not necessary
                    if (!labels[i].Show)
                    {
                        continue;
                    }

                    if (labels[i] is Label)
                    {
                        var label = labels[i] as Label;
                        if (label.Style.IsTextOnPath == false || label.TextOnPathLabel == null)
                        {
                            VectorRenderer.DrawLabel(g, label.Location, label.Style.Offset,
                                label.Style.GetFontForGraphics(g), label.Style.ForeColor,
                                label.Style.BackColor, label.Style.Halo, label.Rotation,
                                label.Text, map, label.Style.HorizontalAlignment,
                                label.LabelPoint);
                        }
                        else
                        {
                            if (label.Style.BackColor != null && label.Style.BackColor != System.Drawing.Brushes.Transparent)
                            {
                                //draw background
                                if (label.TextOnPathLabel.RegionList.Count > 0)
                                {
                                    g.FillRectangles(labels[i].Style.BackColor, labels[i].TextOnPathLabel.RegionList.ToArray());
                                    //g.FillPolygon(labels[i].Style.BackColor, labels[i].TextOnPathLabel.PointsText.ToArray());
                                }
                            }
                            label.TextOnPathLabel.DrawTextOnPath();
                        }
                    }
                    else if (labels[i] is PathLabel)
                    {
                        var plbl = labels[i] as PathLabel;
                        var lblStyle = plbl.Style;
                        g.DrawString(lblStyle.Halo, new SolidBrush(lblStyle.ForeColor), plbl.Text,
                            lblStyle.Font.FontFamily, (int) lblStyle.Font.Style, lblStyle.Font.Size,
                            lblStyle.GetStringFormat(), lblStyle.IgnoreLength, plbl.Location);
                    }
                }
            }
            base.Render(g, map);
        }


        private BaseLabel CreateLabel(FeatureDataRow fdr, IGeometry feature, string text, float rotation, LabelStyle style, IMapViewPort map, Graphics g)
        {
            return CreateLabel(fdr, feature, text, rotation, Priority, style, map, g, _getLocationMethod);
        }

        private static BaseLabel CreateLabel(FeatureDataRow fdr, IGeometry feature, string text, float rotation, int priority, LabelStyle style, IMapViewPort map, Graphics g, GetLocationMethod _getLocationMethod)
        {
            if (feature == null) return null;

            BaseLabel lbl = null;
            var font = style.GetFontForGraphics(g);

            var size = VectorRenderer.SizeOfString(g, text, font);

            if (feature is ILineal)
            {
                var line = feature as ILineString;
                if (line != null)
                {
                    if (style.IsTextOnPath == false)
                    {
                        if (size.Width < 0.95 * line.Length / map.PixelWidth || style.IgnoreLength)
                        {
                            var positiveLineString = PositiveLineString(line, false);
                            var lineStringPath = LineStringToPath(positiveLineString, map /*, false*/);
                            var rect = lineStringPath.GetBounds();

                            if (style.CollisionDetection && !style.CollisionBuffer.IsEmpty)
                            {
                                var cbx = style.CollisionBuffer.Width;
                                var cby = style.CollisionBuffer.Height;
                                rect.Inflate(2 * cbx, 2 * cby);
                                rect.Offset(-cbx, -cby);
                            }
                            var labelBox = new LabelBox(rect);

                            lbl = new PathLabel(text, lineStringPath, 0, priority, labelBox, style);
                        }
                    }
                    else
                    {
                        //get centriod
                        var position2 = map.WorldToImage(feature.EnvelopeInternal.Centre);
                        lbl = new Label(text, position2, rotation, priority, style);
                        if (size.Width < 0.95 * line.Length / map.PixelWidth || !style.IgnoreLength)
                        {
                            CalculateLabelAroundOnLineString(line, ref lbl, map, g, size);
                        }
                    }
                }
                return lbl;
            }

            var worldPosition = _getLocationMethod == null
                ? feature.EnvelopeInternal.Centre
                : _getLocationMethod(fdr);

            if (worldPosition == null) return null;

            var position = Transform.WorldtoMap(worldPosition, map);

            var location = new PointF(
                position.X - size.Width*(short) style.HorizontalAlignment*0.5f,
                position.Y - size.Height*(short) (2 - (int) style.VerticalAlignment)*0.5f);

            if (location.X - size.Width > map.Size.Width || location.X + size.Width < 0 ||
                location.Y - size.Height > map.Size.Height || location.Y + size.Height < 0)
                return null;

            if (!style.CollisionDetection)
                lbl = new Label(text, location, rotation, priority, null, style)
                    {LabelPoint = position};
            else
            {
                //Collision detection is enabled so we need to measure the size of the string
                lbl = new Label(text, location, rotation, priority,
                                new LabelBox(location.X - style.CollisionBuffer.Width,
                                             location.Y - style.CollisionBuffer.Height,
                                             size.Width + 2f*style.CollisionBuffer.Width,
                                             size.Height + 2f*style.CollisionBuffer.Height), style) 
                                { LabelPoint = position }; 
            }

            /*
            if (feature is LineString)
            {
                var line = feature as LineString;

                //Only label feature if it is long enough, or it is definately wanted                
                if (line.Length / map.PixelSize > size.Width || style.IgnoreLength)
                {
                    CalculateLabelOnLinestring(line, ref lbl, map);
                }
                else
                    return null;
            }
            */
            return lbl;
        }

        /// <summary>
        /// Very basic test to check for positive direction of Linestring
        /// </summary>
        /// <param name="line">The linestring to test</param>
        /// <param name="isRightToLeft">Value indicating whether labels are to be printed right to left</param>
        /// <returns>The positively directed linestring</returns>
        private static ILineString PositiveLineString(ILineString line, bool isRightToLeft)
        {
            var s = line.StartPoint;
            var e = line.EndPoint;

            var dx = e.X - s.X;
            if (isRightToLeft && dx < 0)
                return line;
            
            if (!isRightToLeft && dx >= 0)
                return line;

            var revCoord = new Stack<Coordinate>(line.Coordinates);

            return line.Factory.CreateLineString(revCoord.ToArray());
        }

        //private static void WarpedLabel(MultiLineString line, ref BaseLabel baseLabel, Map map)
        //{
        //    var path = MultiLineStringToPath(line, map, true);

        //    var pathLabel = new PathLabel(baseLabel.Text, path, 0f, baseLabel.Priority, new LabelBox(path.GetBounds()), baseLabel.Style);
        //    baseLabel = pathLabel;
        //}

        //private static void WarpedLabel(LineString line, ref BaseLabel baseLabel, Map map)
        //{
            
        //    var path = LineStringToPath(line, map, false);

        //    var pathLabel = new PathLabel(baseLabel.Text, path, 0f, baseLabel.Priority, new LabelBox(path.GetBounds()), baseLabel.Style);
        //    baseLabel = pathLabel;
        //}


        /// <summary>
        /// Function to transform a linestring to a graphics path for further processing
        /// </summary>
        /// <param name="lineString">The Linestring</param>
        /// <param name="map">The map</param>
        /// <!--<param name="useClipping">A value indicating whether clipping should be applied or not</param>-->
        /// <returns>A GraphicsPath</returns>
        public static GraphicsPath LineStringToPath(ILineString lineString, IMapViewPort map/*, bool useClipping*/)
        {
            var gp = new GraphicsPath(FillMode.Alternate);
            //if (!useClipping)
                gp.AddLines(lineString.TransformToImage(map));
            //else
            //{
            //    var bb = map.Envelope;
            //    var cohenSutherlandLineClipping = new CohenSutherlandLineClipping(bb.Left, bb.Bottom, bb.Right, bb.Top);
            //    var clippedLineStrings = cohenSutherlandLineClipping.ClipLineString(lineString);
            //    foreach (var clippedLineString in clippedLineStrings.LineStrings)
            //    {
            //        var s = clippedLineString.StartPoint;
            //        var e = clippedLineString.EndPoint;
                    
            //        var dx = e.X - s.X;
            //        //var dy = e.Y - s.Y;

            //        LineString revcls = null;
            //        if (dx < 0)
            //            revcls = ReverseLineString(clippedLineString);
                    
            //        gp.StartFigure();
            //        gp.AddLines(revcls == null ? clippedLineString.TransformToImage(map) : revcls.TransformToImage(map));
            //    }
            //}
            return gp;
        }

        //private static LineString ReverseLineString(LineString clippedLineString)
        //{
        //    var coords = new Stack<Point>(clippedLineString.Vertices);
        //    return new LineString(coords.ToArray());
        //}

        ///// <summary>
        ///// Function to transform a linestring to a graphics path for further processing
        ///// </summary>
        ///// <param name="multiLineString">The Linestring</param>
        ///// <param name="map">The map</param>
        ///// <param name="useClipping">A value indicating whether clipping should be applied or not</param>
        ///// <returns>A GraphicsPath</returns>
        //public static GraphicsPath MultiLineStringToPath(MultiLineString multiLineString, Map map, bool useClipping)
        //{
        //    var gp = new GraphicsPath(FillMode.Alternate);
        //    foreach (var lineString in multiLineString.LineStrings)
        //        gp.AddPath(LineStringToPath(lineString, map, useClipping), false);

        //    return gp;
        //}

        //private static GraphicsPath LineToGraphicsPath(LineString line, Map map)
        //{
        //    GraphicsPath path = new GraphicsPath();
        //    path.AddLines(line.TransformToImage(map));
        //    return path;
        //}

        private static void CalculateLabelOnLinestring(ILineString line, ref BaseLabel baseLabel, IMapViewPort map)
        {
            double dx, dy;
            var label = baseLabel as Label;

            // first find the middle segment of the line
            var vertices = line.Coordinates;
            var midPoint = (vertices.Length - 1)/2;
            if (vertices.Length > 2)
            {
                dx = vertices[midPoint + 1].X - vertices[midPoint].X;
                dy = vertices[midPoint + 1].Y - vertices[midPoint].Y;
            }
            else
            {
                midPoint = 0;
                dx = vertices[1].X - vertices[0].X;
                dy = vertices[1].Y - vertices[0].Y;
            }
            if (dy == 0)
                label.Rotation = 0;
            else if (dx == 0)
                label.Rotation = 90;
            else
            {
                // calculate angle of line					
                var angle = -Math.Atan(dy/dx) + Math.PI*0.5;
                angle *= (180d/Math.PI); // convert radians to degrees
                label.Rotation = (float) angle - 90; // -90 text orientation
            }
            var tmpx = vertices[midPoint].X + (dx*0.5);
            var tmpy = vertices[midPoint].Y + (dy*0.5);
            label.Location = map.WorldToImage(new Coordinate(tmpx, tmpy));
        }

        private static void CalculateLabelAroundOnLineString(ILineString line, ref BaseLabel label, IMapViewPort map, System.Drawing.Graphics g, System.Drawing.SizeF textSize)
        {
            var sPoints = line.Coordinates;

            // only get point in enverlop of map
            var colPoint = new Collection<PointF>();
            var bCheckStarted = false;
            //var testEnvelope = map.Envelope.Grow(map.PixelSize*10);
            for (var j = 0; j < sPoints.Length; j++)
            {
                if (map.Envelope.Contains(sPoints[j]))
                {
                    //points[j] = map.WorldToImage(sPoints[j]);
                    colPoint.Add(map.WorldToImage(sPoints[j]));
                    bCheckStarted = true;
                }
                else if (bCheckStarted)
                {
                    // fix bug curved line out of map in center segment of line
                    break;
                }
            }

            if (colPoint.Count > 1)
            {
                label.TextOnPathLabel = new TextOnPath();
                switch (label.Style.HorizontalAlignment)
                {
                    case LabelStyle.HorizontalAlignmentEnum.Left:
                        label.TextOnPathLabel.TextPathAlignTop = TextPathAlign.Left;
                        break;
                    case LabelStyle.HorizontalAlignmentEnum.Right:
                        label.TextOnPathLabel.TextPathAlignTop = TextPathAlign.Right;
                        break;
                    case LabelStyle.HorizontalAlignmentEnum.Center:
                        label.TextOnPathLabel.TextPathAlignTop = TextPathAlign.Center;
                        break;
                    default:
                        label.TextOnPathLabel.TextPathAlignTop = TextPathAlign.Center;
                        break;
                }
                switch (label.Style.VerticalAlignment)
                {
                    case LabelStyle.VerticalAlignmentEnum.Bottom:
                        label.TextOnPathLabel.TextPathPathPosition = TextPathPosition.UnderPath;
                        break;
                    case LabelStyle.VerticalAlignmentEnum.Top:
                        label.TextOnPathLabel.TextPathPathPosition = TextPathPosition.OverPath;
                        break;
                    case LabelStyle.VerticalAlignmentEnum.Middle:
                        label.TextOnPathLabel.TextPathPathPosition = TextPathPosition.CenterPath;
                        break;
                    default:
                        label.TextOnPathLabel.TextPathPathPosition = TextPathPosition.CenterPath;
                        break;
                }

                var idxStartPath = 0;
                var numberPoint = colPoint.Count;
                // start Optimzes Path points                
                
                var step = 100;
                if (colPoint.Count >= step * 2)
                {
                    numberPoint = step * 2; ;
                    switch (label.Style.HorizontalAlignment)
                    {
                        case LabelStyle.HorizontalAlignmentEnum.Left:
                            //label.TextOnPathLabel.TextPathAlignTop = SharpMap.Rendering.TextPathAlign.Left;
                            idxStartPath = 0;
                            break;
                        case LabelStyle.HorizontalAlignmentEnum.Right:
                            //label.TextOnPathLabel.TextPathAlignTop = SharpMap.Rendering.TextPathAlign.Right;
                            idxStartPath = colPoint.Count - step;
                            break;
                        case LabelStyle.HorizontalAlignmentEnum.Center:
                            //label.TextOnPathLabel.TextPathAlignTop = SharpMap.Rendering.TextPathAlign.Center;
                            idxStartPath = (int)colPoint.Count / 2 - step;
                            break;
                        default:
                            //label.TextOnPathLabel.TextPathAlignTop = SharpMap.Rendering.TextPathAlign.Center;
                            idxStartPath = (int)colPoint.Count / 2 - step;
                            break;
                    }
                }
                // end optimize path point
                var points = new PointF[numberPoint];
                var count = 0;
                if (colPoint[0].X <= colPoint[colPoint.Count - 1].X)
                {
                    for (var l = idxStartPath; l < numberPoint + idxStartPath; l++)
                    {
                        points[count] = colPoint[l];
                        count++;
                    }
                }
                else
                {
                    //reverse the path                    
                    for (var k = numberPoint - 1 + idxStartPath; k >= idxStartPath; k--)
                    {
                        points[count] = colPoint[k];
                        count++;
                    }
                }
                /*
                //get text size in page units ie pixels
                float textheight = label.Style.Font.Size;
                switch (label.Style.Font.Unit)
                {
                    case GraphicsUnit.Display:
                        textheight = textheight * g.DpiY / 75;
                        break;
                    case GraphicsUnit.Document:
                        textheight = textheight * g.DpiY / 300;
                        break;
                    case GraphicsUnit.Inch:
                        textheight = textheight * g.DpiY;
                        break;
                    case GraphicsUnit.Millimeter:
                        textheight = (float)(textheight / 25.4 * g.DpiY);
                        break;
                    case GraphicsUnit.Pixel:
                        //do nothing
                        break;
                    case GraphicsUnit.Point:
                        textheight = textheight * g.DpiY / 72;
                        break;
                }
                var topFont = new Font(label.Style.Font.FontFamily, textheight, label.Style.Font.Style, GraphicsUnit.Pixel);
                 */
                var topFont = label.Style.GetFontForGraphics(g);
                //
                using (var path = new GraphicsPath())
                {
                    path.AddLines(points);

                    label.TextOnPathLabel.PathColorTop = System.Drawing.Color.Transparent;
                    label.TextOnPathLabel.Text = label.Text;
                    label.TextOnPathLabel.LetterSpacePercentage = 90;
                    label.TextOnPathLabel.FillColorTop = new System.Drawing.SolidBrush(label.Style.ForeColor);
                    label.TextOnPathLabel.Font = topFont;
                    label.TextOnPathLabel.PathDataTop = path.PathData;
                    label.TextOnPathLabel.Graphics = g;
                    //label.TextOnPathLabel.ShowPath=true;
                    //label.TextOnPathLabel.PathColorTop = System.Drawing.Color.YellowGreen;
                    if (label.Style.Halo != null)
                    {
                        label.TextOnPathLabel.ColorHalo = label.Style.Halo;
                    }
                    else
                    {
                        label.TextOnPathLabel.ColorHalo = null;// new System.Drawing.Pen(label.Style.ForeColor, (float)0.5); 
                    }
                }

                // MeasureString to get region
                label.TextOnPathLabel.MeasureString = true;
                label.TextOnPathLabel.DrawTextOnPath();
                label.TextOnPathLabel.MeasureString = false;
                // Get Region label for CollissionDetection here.
                using (var pathRegion = new GraphicsPath())
                {

                    if (label.TextOnPathLabel.RegionList.Count > 0)
                    {
                        //int idxCenter = (int)label.TextOnPathLabel.PointsText.Count / 2;
                        //System.Drawing.Drawing2D.Matrix rotationMatrix = g.Transform.Clone();// new Matrix();
                        //rotationMatrix.RotateAt(label.TextOnPathLabel.Angles[idxCenter], label.TextOnPathLabel.PointsText[idxCenter]);
                        //if (label.TextOnPathLabel.PointsTextUp.Count > 0)
                        //{
                        //    for (int up = label.TextOnPathLabel.PointsTextUp.Count - 1; up >= 0; up--)
                        //    {
                        //        label.TextOnPathLabel.PointsText.Add(label.TextOnPathLabel.PointsTextUp[up]);
                        //    }

                        //}                 
                        pathRegion.AddRectangles(label.TextOnPathLabel.RegionList.ToArray());

                        // get box for detect colission here              
                        label.Box = new LabelBox(pathRegion.GetBounds());
                        //g.FillRectangle(System.Drawing.Brushes.YellowGreen, label.Box);
                    }
                }
            }

        }

        #region IAsyncDataFetcher
        protected FeatureDataSet _dataCache;
        protected bool IsFetching;
        protected bool NeedsUpdate = true;
        private Envelope _newEnvelope;
        private int FetchingPostponedInMilliseconds { get; set; }
        private Timer _startFetchTimer;

        /// <summary>
        /// Raises the dada loaded event.
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
        /// Load datas.
        /// </summary>
        /// <param name="view"></param>
        public override void LoadDatas(IMapViewPort view)
        {
            _newEnvelope = view.Envelope;

            if (IsFetching)
            {
                NeedsUpdate = true;
                return;
            }

            _startFetchTimer?.Dispose();
            _startFetchTimer = new Timer(StartFetchTimerElapsed, null, FetchingPostponedInMilliseconds, int.MaxValue);
        }

        void StartFetchTimerElapsed(object state)
        {
            if (_newEnvelope == null) return;
            LoadDatas(_newEnvelope);
            _startFetchTimer.Dispose();
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
                            if (features[i].Geometry != null)
                            {
                                features[i].Geometry = ToTarget(features[i].Geometry);
                            }
                        }
                    }
                }

                var oldDatas = _dataCache;
                _dataCache = ds;

                OnLayerDataLoaded();

                oldDatas?.Dispose();

                IsFetching = false;
                if (NeedsUpdate) LoadDatas(_newEnvelope);
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

        public void AbortFetch()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Clear cached datas.
        /// </summary>
        public void ClearCache()
        {
            var oldDatas = _dataCache;
            _dataCache = null;
            oldDatas?.Dispose();
        }

        #endregion
    }
}