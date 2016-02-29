using System;
using SharpMap.Layers;
using BruTile;

namespace SharpMap.Fetching
{
    public interface IAsyncDataFetcher
    {
        /// <summary>
        /// Abords fetch.
        /// </summary>
        void AbortFetch();

        /// <summary>
        /// Indicates that there has been a change in the view of the map
        /// </summary>
        void LoadDatas(IMapViewPort view);

        /// <summary>
        /// Event raised when data has change.
        /// </summary>
        event DataChangedEventHandler DataChanged;

        /// <summary>
        /// Clears the cache.
        /// </summary>
        void ClearCache();
    }

    /// <summary>
    /// Data changed delegate.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    public delegate void DataChangedEventHandler(object sender, DataChangedEventArgs e);

    /// <summary>
    /// Data changed event args.
    /// </summary>
    public class DataChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Initialize a new instance of <see cref="DataChangedEventArgs"/>.
        /// </summary>
        public DataChangedEventArgs() : this(null, false) { }

        /// <summary>
        /// Initialize a new instance of <see cref="DataChangedEventArgs"/>.
        /// </summary>
        /// <param name="error"></param>
        /// <param name="cancelled"></param>
        public DataChangedEventArgs(Exception error, bool cancelled)
            : this(error, cancelled, null, string.Empty)
        { }

        /// <summary>
        /// Initialize a new instance of <see cref="DataChangedEventArgs"/>.
        /// </summary>
        /// <param name="error"></param>
        /// <param name="cancelled"></param>
        /// <param name="tileInfo"></param>
        public DataChangedEventArgs(Exception error, bool cancelled, TileInfo tileInfo)
             : this(error, cancelled, tileInfo, string.Empty)
        {
            
        }

        /// <summary>
        /// Initialize a new instance of <see cref="DataChangedEventArgs"/>.
        /// </summary>
        /// <param name="error"></param>
        /// <param name="cancelled"></param>
        /// <param name="tileInfo"></param>
        /// <param name="layerName"></param>
        public DataChangedEventArgs(Exception error, bool cancelled, TileInfo tileInfo, string layerName)
        {
            Error = error;
            Cancelled = cancelled;
            LayerName = layerName;
            TileInfo = tileInfo;
        }

        /// <summary>
        /// Initialize a new instance of <see cref="DataChangedEventArgs"/>.
        /// </summary>
        /// <param name="error"></param>
        /// <param name="cancelled"></param>
        /// <param name="layerName"></param>
        /// <param name="layer"></param>
        public DataChangedEventArgs(Exception error, bool cancelled, string layerName, ILayer layer)
        {
            Error = error;
            Cancelled = cancelled;
            LayerName = layerName;
            Layer = layer;
        }

        /// <summary>
        /// Gets the error.
        /// </summary>
        public Exception Error { get; private set; }

        /// <summary>
        /// Gets information indicating the fetch has been cancelled.
        /// </summary>
        public bool Cancelled { get; private set; }

        /// <summary>
        /// Gets or sets the LayerName.
        /// </summary>
        public string LayerName { get; set; }

        /// <summary>
        /// Gets or sets the layer.
        /// </summary>
        public ILayer Layer { get; set; }

        /// <summary>
        /// Gets the tile info.
        /// </summary>
        public TileInfo TileInfo { get; private set; }

        public byte[] Image { get; set; }

    }
}