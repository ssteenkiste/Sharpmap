using BruTile;
using System;

namespace SharpMap.Fetching
{
    class FetchOnThread
    //This class is needed because in CF one can only pass arguments to a thread using a class constructor.
    //Once support for CF is dropped (replaced by SL on WinMo?) this class should be removed.
    {
        readonly ITileProvider _tileProvider;
        readonly TileInfo _tileInfo;
        readonly FetchTileCompletedEventHandler _fetchTileCompleted;

        public FetchOnThread(ITileProvider tileProvider, TileInfo tileInfo, FetchTileCompletedEventHandler fetchTileCompleted)
        {
            _tileProvider = tileProvider;
            _tileInfo = tileInfo;
            _fetchTileCompleted = fetchTileCompleted;
        }

        /// <summary>
        /// Fetchs a tile.
        /// </summary>
        public void FetchTile()
        {
            Exception error = null;
            byte[] image = null;

            try
            {
                if (_tileProvider != null) image = _tileProvider.GetTile(_tileInfo);
            }
            catch (Exception ex) //This may seem a bit weird. We catch the exception to pass it as an argument. This is because we are on a worker thread here, we cannot just let it fall through. 
            {
                error = ex;
            }
            _fetchTileCompleted(this, new FetchTileCompletedEventArgs(error, false, _tileInfo, image));
        }
    }

    public delegate void FetchTileCompletedEventHandler(object sender, FetchTileCompletedEventArgs e);

    public class FetchTileCompletedEventArgs
    {
        public FetchTileCompletedEventArgs(Exception error, bool cancelled, TileInfo tileInfo, byte[] image)
        {
            Error = error;
            Cancelled = cancelled;
            TileInfo = tileInfo;
            Image = image;
        }

        public Exception Error;
        public readonly bool Cancelled;
        public readonly TileInfo TileInfo;
        public readonly byte[] Image;
    }
}
