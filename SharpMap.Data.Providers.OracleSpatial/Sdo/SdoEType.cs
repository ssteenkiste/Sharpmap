using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpMap.Data.Providers.OracleSpatial.Sdo
{
    internal enum SdoEType
    {
        Unknown = -1,
        Coordinate = 1,
        Line = 2,
        Polygon = 3,
        PolygonExterior = 1003,
        PolygonInterior = 2003
    }
}
