using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GeoAPI.Geometries;
using SharpMap.Data;
using SharpMap.Data.Providers;

namespace SharpMap.Fetching
{
    internal class FeaturesFetcher
    {
        internal delegate void DataArrivedDelegate(FeatureDataSet features, object state = null);

        private readonly Envelope _envelope;
        private readonly FeatureDataSet _dataSet;
        private readonly DataArrivedDelegate _dataArrived;
        private readonly IProvider _provider;
        private readonly long _timeOfRequest;

        public FeaturesFetcher(Envelope envelope, FeatureDataSet ds, IProvider provider, DataArrivedDelegate dataArrived, long timeOfRequest = default(long))
        {
            _envelope = envelope;
            _dataSet = ds;
            _provider = provider;
            _dataArrived = dataArrived;
            _timeOfRequest = timeOfRequest;
           

        }

        public void FetchOnThread(object state)
        {
            lock (_provider)
            {
                 _provider.ExecuteIntersectionQuery(_envelope, _dataSet);
                if (_dataArrived != null) _dataArrived(_dataSet, _timeOfRequest);
            }
        }

    }
}
