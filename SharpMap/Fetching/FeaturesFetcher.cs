using GeoAPI.Geometries;
using SharpMap.Data;
using SharpMap.Data.Providers;
using System;

namespace SharpMap.Fetching
{

    /// <summary>
    /// Features fetcher.
    /// </summary>
    public class FeaturesFetcher
    {

        /// <summary>
        /// Delegate for data arrived callback.
        /// </summary>
        /// <param name="features"></param>
        /// <param name="state"></param>
        /// <param name="error"></param>
        public delegate void DataArrivedDelegate(FeatureDataSet features, object state = null, Exception error=null);

        private readonly Envelope _envelope;
        private readonly FeatureDataSet _dataSet;
        private readonly DataArrivedDelegate _dataArrived;
        private readonly IProvider _provider;
        private readonly long _timeOfRequest;

        /// <summary>
        /// Initialize a new instance of <see cref="FeaturesFetcher"/>.
        /// </summary>
        /// <param name="envelope"></param>
        /// <param name="ds"></param>
        /// <param name="provider"></param>
        /// <param name="dataArrived"></param>
        /// <param name="timeOfRequest"></param>
        public FeaturesFetcher(Envelope envelope, FeatureDataSet ds, IProvider provider, DataArrivedDelegate dataArrived, long timeOfRequest = default(long))
        {
            _envelope = envelope;
            _envelope.ExpandBy(envelope.Width,envelope.Height);
            _dataSet = ds;
            _provider = provider;
            _dataArrived = dataArrived;
            _timeOfRequest = timeOfRequest;
        }

        public void AbordFetch()
        {

        }

        /// <summary>
        /// Fetches the datas.
        /// </summary>
        /// <param name="state"></param>
        public void FetchOnThread(object state)
        {
            Exception error = null;

            //lock (_provider)
            {
                try
                {
                    _provider.ExecuteIntersectionQuery(_envelope, _dataSet);
                }
                catch(Exception ex)
                {
                    error = ex;
                }
                if (_dataArrived != null) _dataArrived(_dataSet, _timeOfRequest,error);
            }
        }

    }
}
