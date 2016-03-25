// Copyright 2006 - Morten Nielsen (www.iter.dk)
//
// This file is part of SharpMap.
// SharpMap is free software; you can redistribute it and/or modify
// it under the terms of the GNU Lesser General Public License as published by
// the Free Software Foundation; either version 2 of the License, or
// (at your option) any later version.
// 
// SharpMap is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Lesser General Public License for more details.

// You should have received a copy of the GNU Lesser General Public License
// along with SharpMap; if not, write to the Free Software
// Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA 

using System.Collections.Generic;
using System.Linq;
using System;
using SharpMap.Utilities.Indexing;

namespace SharpMap.Rendering
{
    /// <summary>
    /// Class defining delegate for label collision detection and static predefined methods
    /// </summary>
    public class LabelCollisionDetection
    {
        #region Delegates

        /// <summary>
        /// Delegate method for filtering labels. Useful for performing custom collision detection on labels
        /// </summary>
        /// <param name="labels"></param>
        /// <returns></returns>
        public delegate void LabelFilterMethod(List<BaseLabel> labels);

        #endregion

        #region Label filter methods

        /// <summary>
        /// Simple and fast label collision detection.
        /// </summary>
        /// <param name="labels"></param>
        public static void SimpleCollisionDetection(List<BaseLabel> labels)
        {
            labels.Sort(); // sort labels by intersectiontests of labelbox
            //remove labels that intersect other labels
            for (int i = labels.Count - 1; i > 0; i--)
                if (labels[i].CompareTo(labels[i - 1]) == 0)
                {
                    if (labels[i].Priority == labels[i - 1].Priority) continue;

                    if (labels[i].Priority > labels[i - 1].Priority)
                        //labels.RemoveAt(i - 1);
                        labels[i - 1].Show = false;
                    else
                        //labels.RemoveAt(i)
                        labels[i].Show = false;
                }
        }

        /// <summary>
        /// Thorough label collision detection.
        /// </summary>
        /// <param name="labels"></param>
        public static void ThoroughCollisionDetection(List<BaseLabel> labels)
        {
            labels.Sort(); // sort labels by intersectiontests of labelbox
            //remove labels that intersect other labels
            for (int i = labels.Count - 1; i > 0; i--)
            {
                if (!labels[i].Show) continue;
                for (int j = i - 1; j >= 0; j--)
                {
                    if (!labels[j].Show) continue;
                    if (labels[i].CompareTo(labels[j]) == 0)
                        if (labels[i].Priority >= labels[j].Priority)
                        {
                            labels[j].Show = false;
                            //labels.RemoveAt(j);
                            //i--;
                        }
                        else
                        {
                            labels[i].Show = false;
                            //labels.RemoveAt(i);
                            //i--;
                            break;
                        }
                }
            }
        }

        /// <summary>
        /// Quick (O(n log n)) and accurate collision detection
        /// </summary>
        /// <param name="labels"></param>
        public static void QuickAccurateCollisionDetectionMethod(List<BaseLabel> labels)
        {
            // find minimum / maximum coordinates
            double minX = double.MaxValue;
            double maxX = double.MinValue;
            double minY = double.MaxValue;
            double maxY = double.MinValue;
            foreach (BaseLabel l in labels)
            {
                ProperBox box = new ProperBox(l.Box);
                if (box.Left < minX) minX = box.Left;
                if (box.Right > maxX) maxX = box.Right;
                if (box.Bottom > maxY) maxY = box.Bottom;
                if (box.Top < minY) minY = box.Top;
            }

            // sort by area (highest priority first, followed by low area to maximize the amount of labels displayed)
            var sortedLabels = labels.OrderByDescending(label => label.Priority).ThenBy(label => label.Box.Width * label.Box.Height);

            // make visible if it does not collide with other labels. Uses a quadtree and is therefore fast O(n log n) 
            QuadtreeNode<ProperBox> quadTree = new QuadtreeNode<ProperBox>(minX, maxX, minY, maxY, new LabelBoxContainmentChecker(), 0, 10);
            foreach (BaseLabel l in sortedLabels)
            {
                if (!l.Show) continue;
                if (quadTree.CollidesWithAny(new ProperBox(l.Box)))
                {
                    l.Show = false;
                }
                else
                {
                    ProperBox box = new ProperBox(l.Box);
                    quadTree.Insert(box);
                }
            }
        }

        #endregion

        #region "Tools"

        private class ProperBox
        {
            private LabelBox box;

            /// <summary>
            /// Initialize a new instance of <see cref="ProperBox"/>.
            /// </summary>
            /// <param name="box"></param>
            public ProperBox(LabelBox box)
            {
                this.box = box;
            }

            public float Left
            {
                get { return box.Left; }
            }

            public float Right
            {
                get { return box.Right; }
            }

            public float Height
            {
                get { return box.Height; }
            }

            public float Width
            {
                get { return box.Width; }
            }

            public float Bottom
            {
                get { return box.Top + Height; }
            }

            public float Top
            {
                get { return box.Top; }
            }
        }

        /// <summary>
        /// A labelbox checker.
        /// </summary>
        private class LabelBoxContainmentChecker : QuadtreeNode<ProperBox>.QuadtreeContainmentChecker<ProperBox>
        {
            /// <summary>
            /// Check if node contains or intersects the box.
            /// </summary>
            /// <param name="node"></param>
            /// <param name="obj"></param>
            /// <returns></returns>
            public bool IsContainedOrIntersects(QuadtreeNode<ProperBox> node, ProperBox obj)
            {
                return !(obj.Left > node.Right | obj.Right < node.Left | obj.Top > node.Bottom | obj.Bottom < node.Top);
            }

            /// <summary>
            /// Check if
            /// </summary>
            /// <param name="node"></param>
            /// <param name="obj"></param>
            /// <returns></returns>
            public bool IsFullyContained(QuadtreeNode<ProperBox> node, ProperBox obj)
            {
                return obj.Left >= node.Left & obj.Right <= node.Right & obj.Bottom <= node.Bottom & obj.Top >= node.Top;
            }

            public bool IsContainedOrIntersects(ProperBox obj1, ProperBox obj2)
            {
                return !(obj1.Left > obj2.Right | obj1.Right < obj2.Left | obj1.Top > obj2.Bottom | obj1.Bottom < obj2.Top);
            }
        }

        #endregion
    }
}
