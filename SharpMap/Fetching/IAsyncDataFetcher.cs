﻿using System;
using BruTile;
using GeoAPI.Geometries;
using SharpMap.Layers;

namespace SharpMap.Fetching
{
    public interface IAsyncDataFetcher
    {
        void AbortFetch();

        /// <summary>
        /// Indicates that there has been a change in the view of the map
        /// </summary>
        void LoadDatas(IMapViewPort view);
        event DataChangedEventHandler DataChanged;
        void ClearCache();
    }

    public delegate void DataChangedEventHandler(object sender, DataChangedEventArgs e);

    public class DataChangedEventArgs : EventArgs
    {
        public DataChangedEventArgs() : this(null, false) { }

        public DataChangedEventArgs(Exception error, bool cancelled)
            : this(error, cancelled, string.Empty)
        { }

        public DataChangedEventArgs(Exception error, bool cancelled,  string layerName)
        {
            Error = error;
            Cancelled = cancelled;
            LayerName = layerName;
        }

        public DataChangedEventArgs(Exception error, bool cancelled, string layerName, ILayer layer)
        {
            Error = error;
            Cancelled = cancelled;
            LayerName = layerName;
            Layer = layer;

        }

        public Exception Error { get; private set; }
        public bool Cancelled { get; private set; }
        public string LayerName { get; set; }

        public ILayer Layer { get; set; }
        
    }
}