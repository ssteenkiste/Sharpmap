using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GeoAPI.Geometries;
using NetTopologySuite.IO;
using SharpMap.Data.Providers.OracleSpatial.Sdo;

namespace SharpMap.Data.Providers.OracleSpatial
{
    public static class GeometryExtensions
    {

        public static IGeometry AsGeometry(this SdoGeometry sdoGeometry, IGeometryFactory factory)
        {
            var reader = new OracleGeometryReader(factory);
            return reader.Read(sdoGeometry);

        } 
    }
}
