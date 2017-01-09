using Oracle.DataAccess.Types;

namespace SharpMap.Data.Providers.OracleSpatial.Sdo
{
    /// <summary>
    /// Sdo Point définition.
    /// </summary>
    [OracleCustomTypeMapping("MDSYS.SDO_POINT_TYPE")]
    public class SdoPoint : OracleCustomTypeBase<SdoPoint>
    {
        private decimal? _x;
        private decimal? _y;
        private decimal? _z;

        /// <summary>
        /// Gets or sets the X coordinate.
        /// </summary>
        [OracleObjectMapping("X")]
        public decimal? X
        {
            get { return _x; }
            set { _x = value; }
        }

        /// <summary>
        /// Gets or sets the Y coordinate.
        /// </summary>
        [OracleObjectMapping("Y")]
        public decimal? Y
        {
            get { return _y; }
            set { _y = value; }
        }

        /// <summary>
        /// Gets or sets the Z coordinate.
        /// </summary>
        [OracleObjectMapping("Z")]
        public decimal? Z
        {
            get { return _z; }
            set { _z = value; }
        }


        public override void MapFromCustomObject()
        {
            SetValue("X", _x);
            SetValue("Y", _y);
            SetValue("Z", _z);
        }
        public override void MapToCustomObject()
        {
            X = GetValue<decimal?>("X");
            Y = GetValue<decimal?>("Y");
            Z = GetValue<decimal?>("Z");
        }
    }
}
