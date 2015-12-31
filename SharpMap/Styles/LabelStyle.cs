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
using System.Runtime.CompilerServices;
using Common.Logging;
using SharpMap.Rendering.Thematics;

namespace SharpMap.Styles
{
    /// <summary>
    /// Defines a style used for rendering labels
    /// </summary>
    [Serializable]
    public class LabelStyle : Style, ICloneable
    {
        #region HorizontalAlignmentEnum enum

        /// <summary>
        /// Label text alignment
        /// </summary>
        public enum HorizontalAlignmentEnum : short
        {
            /// <summary>
            /// Left oriented
            /// </summary>
            Left = 0,
            /// <summary>
            /// Right oriented
            /// </summary>
            Right = 2,
            /// <summary>
            /// Centered
            /// </summary>
            Center = 1
        }

        #endregion

        #region VerticalAlignmentEnum enum

        /// <summary>
        /// Label text alignment
        /// </summary>
        public enum VerticalAlignmentEnum : short
        {
            /// <summary>
            /// Left oriented
            /// </summary>
            Bottom = 0,
            /// <summary>
            /// Right oriented
            /// </summary>
            Top = 2,
            /// <summary>
            /// Centered
            /// </summary>
            Middle = 1
        }

        #endregion

        private Brush _backColor;
        private SizeF _collisionBuffer;
        private bool _collisionDetection;

        private Font _font;

        private Color _foreColor;
        private Pen _halo;
        private HorizontalAlignmentEnum _horizontalAlignment;
        private PointF _offset;
        private VerticalAlignmentEnum _verticalAlignment;
        private float _rotation;
        private bool _ignoreLength;
        private bool _isTextOnPath;
        /// <summary>
        /// get or set label on path
        /// </summary>
        public bool IsTextOnPath
        {
            get { return _isTextOnPath; }
            set { _isTextOnPath = value; }
        }
        /// <summary>
        /// Initializes a new LabelStyle
        /// </summary>
        public LabelStyle()
        {
            _font = new Font(FontFamily.GenericSerif, 12f);
            _offset = new PointF(0, 0);
            _collisionDetection = false;
            _collisionBuffer = new Size(0, 0);
            _foreColor = Color.Black;
            _horizontalAlignment = HorizontalAlignmentEnum.Center;
            _verticalAlignment = VerticalAlignmentEnum.Middle;
        }

        /// <summary>
        /// Releases managed resources
        /// </summary>
        protected override void ReleaseManagedResources()
        {
            if (IsDisposed)
                return;

            if (_font != null) _font.Dispose();
            if (_halo != null) _halo.Dispose();
            if (_backColor != null) _backColor.Dispose();

            base.ReleaseManagedResources();
        }
        /// <summary>
        /// Method to create a deep copy of this <see cref="LabelStyle"/>
        /// </summary>
        /// <returns>A LabelStyle resembling this instance.</returns>
        public LabelStyle Clone()
        {
            var res = (LabelStyle) MemberwiseClone();

            lock (this)
            {
                if (_font != null)
                    res.Font = (Font)_font.Clone();

                if (_halo != null)
                    res._halo = (Pen)_halo.Clone();

                if (_backColor != null)
                    res._backColor = (Brush) _backColor.Clone();
            }

            return res;
        }

        object ICloneable.Clone()
        {
            return Clone();
        }

        /// <summary>
        /// Function to get a <see cref="StringFormat"/> with <see cref="StringFormat.Alignment"/> and <see cref="StringFormat.LineAlignment"/> properties according to 
        /// <see cref="HorizontalAlignment"/> and <see cref="VerticalAlignment"/>
        /// </summary>
        /// <returns>A <see cref="StringFormat"/></returns>
        internal StringFormat GetStringFormat()
        {
            var r = (StringFormat)StringFormat.GenericTypographic.Clone();
            switch (HorizontalAlignment)
            {
                case HorizontalAlignmentEnum.Center:
                    r.Alignment = StringAlignment.Center;
                    break;
                case HorizontalAlignmentEnum.Left:
                    r.Alignment = StringAlignment.Near;
                    break;
                case HorizontalAlignmentEnum.Right:
                    r.Alignment = StringAlignment.Far;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            switch (VerticalAlignment)
            {
                case VerticalAlignmentEnum.Middle:
                    r.LineAlignment = StringAlignment.Center;
                    break;
                case VerticalAlignmentEnum.Top:
                    r.LineAlignment = StringAlignment.Near;
                    break;
                case VerticalAlignmentEnum.Bottom:
                    r.LineAlignment = StringAlignment.Far;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            return r;
        }

        /// <summary>
        /// Label Font
        /// </summary>
        public Font Font
        {
            get { return _font; }
            set
            {
                if (value.Unit != GraphicsUnit.Point)
                    LogManager.GetCurrentClassLogger().Error(fmh => fmh("Only assign fonts with size in Points"));
                _font = value;
            }
        }

        [NonSerialized] private float _cachedDpiY;
        [NonSerialized] private Font _cachedFontForGraphics;

        /// <summary>
        /// Method to create a font that ca
        /// </summary>
        /// <param name="g"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public Font GetFontForGraphics(Graphics g)
        {
            if (g.DpiY != _cachedDpiY)
            {
                _cachedDpiY = g.DpiY;
                if (_cachedFontForGraphics != null) _cachedFontForGraphics.Dispose();
                _cachedFontForGraphics = new Font(_font.FontFamily, _font.GetHeight(g), _font.Style, GraphicsUnit.Pixel);
                
            }
            return _cachedFontForGraphics;
        }

        /// <summary>
        /// Font color
        /// </summary>
        public Color ForeColor
        {
            get { return _foreColor; }
            set { _foreColor = value; }
        }

        /// <summary>
        /// The background color of the label. Set to transparent brush or null if background isn't needed
        /// </summary>
        public Brush BackColor
        {
            get { return _backColor; }
            set { _backColor = value; }
        }

        /// <summary>
        /// Creates a halo around the text
        /// </summary>
        [System.ComponentModel.Category("Uneditable")]
        [System.ComponentModel.Editor()]
        public Pen Halo
        {
            get { return _halo; }
            set
            {
                _halo = value;
                //if (_Halo != null)
                //    _Halo.LineJoin = LineJoin.Round;
            }
        }


        /// <summary>
        /// Specifies relative position of labels with respect to objects label point
        /// </summary>
        [System.ComponentModel.Category("Uneditable")]
        public PointF Offset
        {
            get { return _offset; }
            set { _offset = value; }
        }

        /// <summary>
        /// Gets or sets whether Collision Detection is enabled for the labels.
        /// If set to true, label collision will be tested.
        /// </summary>
        /// <remarks>Just setting this property in a <see cref="ITheme.GetStyle"/> method does not lead to the desired result. You must set it to for the whole layer using the default Style.</remarks>
        [System.ComponentModel.Category("Collision Detection")]
        public bool CollisionDetection
        {
            get { return _collisionDetection; }
            set { _collisionDetection = value; }
        }

        /// <summary>
        /// Distance around label where collision buffer is active
        /// </summary>
        [System.ComponentModel.Category("Collision Detection")]
        public SizeF CollisionBuffer
        {
            get { return _collisionBuffer; }
            set { _collisionBuffer = value; }
        }

        /// <summary>
        /// The horizontal alignment of the text in relation to the labelpoint
        /// </summary>
        [System.ComponentModel.Category("Alignment")]
        public HorizontalAlignmentEnum HorizontalAlignment
        {
            get { return _horizontalAlignment; }
            set { _horizontalAlignment = value; }
        }

        /// <summary>
        /// The horizontal alignment of the text in relation to the labelpoint
        /// </summary>
        [System.ComponentModel.Category("Alignment")]
        public VerticalAlignmentEnum VerticalAlignment
        {
            get { return _verticalAlignment; }
            set { _verticalAlignment = value; }
        }

        /// <summary>
        /// The Rotation of the text
        /// </summary>
        [System.ComponentModel.Category("Alignment")]
        public float Rotation
        {
            get { return _rotation; }
            set { _rotation = value%360f; }
        }

        /// <summary>
        /// Gets or sets if length of linestring should be ignored
        /// </summary>
        [System.ComponentModel.Category("Alignment")]
        public bool IgnoreLength
        {
            get { return _ignoreLength; }
            set { _ignoreLength = value; }
        }
    }
}