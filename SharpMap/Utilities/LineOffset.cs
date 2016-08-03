using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;

namespace SharpMap.Utilities
{
    /// <summary>
    /// Methods for calculating line offsets
    /// </summary>
    public class LineOffset
    {
        private static PointF TranslatePoint(PointF pt, float dist, double radians)
        {
            return new PointF(pt.X + dist * (float)Math.Cos(radians), pt.Y + dist * (float)Math.Sin(radians));
        }

        private class Segments
        {
            public double Angle;
            public double OffsetAngle;
            public float Distance;
            public Tuple<PointF, PointF> Offset;
            public Tuple<PointF, PointF> Original;

        }

        private static List<Segments> OffsetPointLine(PointF[] points, float distance)
        {
            var l = points.Length;
            if (l < 2)
            {
                throw new Exception("Line should be defined by at least 2 points");
            }

            var a = points[0];
            double segmentAngle;
            var offsetSegments = new List<Segments>();

            for (var i = 1; i < l; i++)
            {
                var b = points[i];
                // angle in (-PI, PI]
                segmentAngle = Math.Atan2(a.Y - b.Y, a.X - b.X);
                // angle in (-1.5 * PI, PI/2]
                var offsetAngle = segmentAngle - Math.PI / 2;

                // store offset point and other information to avoid recomputing it later
                offsetSegments.Add(new Segments
                {
                    Angle = segmentAngle,
                    OffsetAngle = offsetAngle,
                    Distance = distance,
                    Original = new Tuple<PointF, PointF>(a, b),
                    Offset = new Tuple<PointF, PointF>(
                      TranslatePoint(a, distance, offsetAngle),
                      TranslatePoint(b, distance, offsetAngle))
                });
                a = b;
            }

            return offsetSegments;
        }

        /*
        Interpolates points between two offset segments in a circular form
        */
        private static PointF[] CircularArc(Segments s1, Segments s2, float distance)
        {
            var points = new List<PointF>();
            if (s1.Angle == s2.Angle)
            {
                points.Add(s1.Offset.Item2);
                return points.ToArray();
            }

            var center = s1.Original.Item2;

            double startAngle, endAngle;

            if (distance < 0)
            {
                startAngle = s1.OffsetAngle;
                endAngle = s2.OffsetAngle;
            }
            else
            {
                // switch start and end angle when going right
                startAngle = s2.OffsetAngle;
                endAngle = s1.OffsetAngle;
            }

            if (endAngle < startAngle)
            {
                endAngle += Math.PI * 2; // the end angle should be bigger than the start angle
            }

            if (endAngle > startAngle + Math.PI)
            {
                var i = Intersection(s1.Offset.Item1, s1.Offset.Item2, s2.Offset.Item1, s2.Offset.Item2);
                if (i != null)
                {
                    points.Add(i.Value);
                }
                return points.ToArray();
            }

            // Step is distance dependent. Bigger distance results in more steps to take
            var step = Math.Abs(8 / distance);
            for (var a = startAngle; a < endAngle; a += step)
            {
                points.Add(TranslatePoint(center, distance, a));
            }
            points.Add(TranslatePoint(center, distance, endAngle));

            if (distance > 0)
            {
                // reverse all points again when going right
                points.Reverse();
            }

            return points.ToArray();
        }

        private static PointF? Intersection(PointF l1a, PointF l1b, PointF l2a, PointF l2b)
        {
            var line1 = LineEquation(l1a, l1b);
            var line2 = LineEquation(l2a, l2b);

            if (line1 == null || line2 == null)
            {
                return null;
            }

            if (line1.Type == EquationType.constant)
            {
                if (line2.Type == EquationType.constant)
                {
                    return null;
                }
                return new PointF(line1.X, line2.A * line1.X + line2.B);
            }
            if (line2.Type == EquationType.constant)
            {
                return new PointF(line2.X, line1.A * line2.X + line1.B);
            }

            if (line1.A == line2.A)
            {
                return null;
            }

            var x = (line2.B - line1.B) / (line1.A - line2.A);
            var y = line1.A * x + line1.B;

            return new PointF(x, y);
        }

        /**
 Find the coefficients (a,b) of a line of equation y = a.x + b,
 or the constant x for vertical lines
 Return null if there's no equation possible
 */
        enum EquationType
        {
            normal,
            constant
        }

        class LineEquationResult
        {
            public EquationType Type;
            public float A;
            public float B;
            public float X;
        }

        static LineEquationResult LineEquation(PointF pt1, PointF pt2)
        {
            if (pt1.X != pt2.X)
            {
                var a = (pt2.Y - pt1.Y) / (pt2.X - pt1.X);
                return new LineEquationResult { A = a, B = pt1.Y - a * pt1.X, Type = EquationType.normal };
            }

            if (pt1.Y != pt2.Y)
            {
                return new LineEquationResult { Type = EquationType.constant, X = pt1.X };
            }

            return null;
        }

        /**
  Join 2 line segments defined by 2 points each,
  with a specified methodnormalizeAngle( (default : intersection);
  */
        static PointF[] JoinSegments(Segments s1, Segments s2, float offset, string joinStyle)
        {
            var jointPoints = new List<PointF>();
            switch (joinStyle)
            {
                case "round":
                    jointPoints.AddRange(CircularArc(s1, s2, offset));
                    break;
                case "cut":

                    var p1 = (Intersection(s1.Offset.Item1, s1.Offset.Item2, s2.Original.Item1, s2.Original.Item2));
                    var p2 = (Intersection(s1.Original.Item1, s1.Original.Item2, s2.Offset.Item1, s2.Offset.Item2));
                    if (p1.HasValue) jointPoints.Add(p1.Value);
                    if (p2.HasValue) jointPoints.Add(p2.Value);
                    break;
                case "straight":
                    jointPoints.Add(s1.Offset.Item2);
                    jointPoints.Add(s2.Offset.Item1);
                    break;
                case "intersection":
                default:
                    jointPoints.Add(Intersection(s1.Offset.Item1, s1.Offset.Item2, s2.Offset.Item1, s2.Offset.Item2).Value);
                    break;
            }
            // filter out null-results
            return jointPoints.ToArray();
        }

        static PointF[]  JoinLineSegments(List<Segments> segments,float offset,string joinStyle)
        {
            var l = segments.Count;
            var joinedPoints = new List<PointF>();
            var s1 = segments[0];
            var s2 = segments[0];
            joinedPoints.Add(s1.Offset.Item1);

            for (var i = 1; i < l; i++)
            {
                s2 = segments[i];
                joinedPoints = joinedPoints.Concat(JoinSegments(s1, s2, offset, joinStyle)).ToList();
                s1 = s2;
            }
            joinedPoints.Add (s2.Offset.Item2);

            return joinedPoints.ToArray();
        }

        public static PointF[]  OffsetPoints(PointF[] pts,float offset)
        {
            var offsetSegments = OffsetPointLine(pts, offset);
            return JoinLineSegments(offsetSegments, offset, "round");
        }

        /// <summary>
        /// Offset a Linestring the given amount perpendicular to the line
        /// For example if a line should be drawn 10px to the right of its original position
        ///
        /// Positive offset offsets right
        /// Negative offset offsets left
        /// </summary>
        /// <param name="lineCoordinates">LineString</param>
        /// <param name="offset">offset amount</param>
        /// <returns>Array of coordinates for the offseted line</returns>
        public static PointF[] OffsetPolyline(PointF[] lineCoordinates, float offset)
        {
            List<PointF> retPoints = new List<PointF>();
            PointF old_pt, old_diffdir = PointF.Empty, old_offdir = PointF.Empty;
            int idx = 0;
            bool first = true;

            /* saved metrics of the last processed point */
            if (lineCoordinates.Length > 0)
            {
                old_pt = lineCoordinates[0];
                for (int j = 1; j < lineCoordinates.Length; j++)
                {
                    PointF pt = lineCoordinates[j]; /* place of the point */
                    PointF diffdir = point_norm(point_diff(pt, old_pt)); /* direction of the line */
                    PointF offdir = point_rotz90(diffdir);/* direction where the distance between the line and the offset is measured */
                    PointF offPt;
                    if (first)
                    {
                        first = false;
                        offPt = point_sum(old_pt, point_mul(offdir, offset));
                    }
                    else /* middle points */
                    {
                        /* curve is the angle of the last and the current line's direction (supplementary angle of the shape's inner angle) */
                        double sin_curve = point_cross(diffdir, old_diffdir);
                        double cos_curve = point_cross(old_offdir, diffdir);
                        if ((-1.0) * CURVE_SIN_LIMIT < sin_curve && sin_curve < CURVE_SIN_LIMIT)
                        {
                            offPt = point_sum(old_pt, point_mul(point_sum(offdir, old_offdir), 0.5 * offset));
                        }
                        else
                        {
                            double base_shift = -1.0 * (1.0 + cos_curve) / sin_curve;
                            offPt = point_sum(old_pt, point_mul(point_sum(point_mul(diffdir, base_shift), offdir), offset));
                        }
                    }

                    retPoints.Add(offPt);

                    old_pt = pt;
                    old_diffdir = diffdir;
                    old_offdir = offdir;
                }

                /* last point */
                if (!first)
                {
                    PointF offpt = point_sum(old_pt, point_mul(old_offdir, offset));
                    retPoints.Add(offpt);
                    idx++;
                }
            }
            return retPoints.ToArray();
        }

        #region helper_methods
        static PointF point_norm(PointF a)
        {
            double lenmul;
            PointF retv = new PointF();
            if (a.X == 0 && a.Y == 0)
                return a;

            lenmul = 1.0 / Math.Sqrt(point_abs2(a));  /* this seems to be the costly operation */

            retv.X = (float)(a.X * lenmul);
            retv.Y = (float)(a.Y * lenmul);

            return retv;
        }
        static double point_abs2(PointF a)
        {
            return a.X * a.X + a.Y * a.Y;
        }
        /* rotate a vector 90 degrees */
        static PointF point_rotz90(PointF a)
        {
            double nx = -1.0 * a.Y, ny = a.X;
            PointF retv = new PointF();
            retv.X = (float)nx; retv.Y = (float)ny;
            return retv;
        }

        /* vector multiply */
        static PointF point_mul(PointF a, double b)
        {
            PointF retv = new PointF((float)(a.X * b), (float)(a.Y * b));
            return retv;
        }
        static PointF point_sum(PointF a, PointF b)
        {
            PointF retv = new PointF(a.X + b.X, a.Y + b.Y);
            return retv;
        }
        static PointF point_diff(PointF a, PointF b)
        {
            PointF retv = new PointF(a.X - b.X, a.Y - b.Y);
            return retv;
        }
        static double point_cross(PointF a, PointF b)
        {
            return a.X * b.Y - a.Y * b.X;
        }
        #endregion

        static readonly double CURVE_SIN_LIMIT = 0.3;


    }
}
