using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpMap.Utilities
{
    /// <summary>
    /// Helper class for zoom calculation.
    /// </summary>
    public static class ZoomHelper
    {
        
        public static void ZoomToBoudingbox(IMapViewPort viewport, double x1, double y1, double x2, double y2, double screenWidth)
        {
            if (x1 > x2) Swap(ref x1, ref x2);
            if (y1 > y2) Swap(ref y1, ref y2);

            var x = (x2 + x1) / 2;
            var y = (y2 + y1) / 2;
            var resolution = (x2 - x1) / screenWidth;
            viewport.Center.X = x;
            viewport.Center.Y = y;
            viewport.Zoom = resolution;
        }

        private static void Swap(ref double xMin, ref double xMax)
        {
            var tempX = xMin;
            xMin = xMax;
            xMax = tempX;
        }

    }
}
