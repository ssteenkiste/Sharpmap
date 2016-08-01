//using SharpMap.Geometries;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SharpMap.Utilities
{

    /// <summary>
    /// Algorithms.
    /// </summary>
    public static class Algorithms
    {
        /// <summary>
        /// Gets the euclidean distance between two points.
        /// </summary>
        /// <param name="x1">The first point's X coordinate.</param>
        /// <param name="y1">The first point's Y coordinate.</param>
        /// <param name="x2">The second point's X coordinate.</param>
        /// <param name="y2">The second point's Y coordinate.</param>
        /// <returns></returns>
        public static double Distance(double x1, double y1, double x2, double y2)
        {
            return Math.Sqrt(Math.Pow(x1 - x2, 2.0) + Math.Pow(y1 - y2, 2.0));
        }

        /// <summary>
        /// Converts the specified angle from degrees to radians
        /// </summary>
        /// <param name="degrees">Angle to convert (degrees)</param>
        /// <returns>Returns the angle in radians</returns>
        public static double DegreesToRadians(double degrees)
        {
            return degrees * Math.PI / 180.0;
        }

        /*

        /// <summary>
        /// Rotates the specified point clockwise about the origin
        /// </summary>
        /// <param name="x">X coordinate to rotate</param>
        /// <param name="y">Y coordinate to rotate</param>
        /// <param name="degrees">Angle to rotate (degrees)</param>
        /// <returns>Returns the rotated point</returns>
        public static Point RotateClockwiseDegrees(double x, double y, double degrees)
        {
            var radians = DegreesToRadians(degrees);

            return RotateClockwiseRadians(x, y, radians);
        }

        /// <summary>
        /// Rotates the specified point clockwise about the origin
        /// </summary>
        /// <param name="x">X coordinate to rotate</param>
        /// <param name="y">Y coordinate to rotate</param>
        /// <param name="radians">Angle to rotate (radians)</param>
        /// <returns>Returns the rotated point</returns>
        public static Point RotateClockwiseRadians(double x, double y, double radians)
        {
            var cos = Math.Cos(-radians);
            var sin = Math.Sin(-radians);
            var newX = x * cos - y * sin;
            var newY = x * sin + y * cos;

            return new Point(newX, newY);
        }
        */
    }
}
