using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpMap.Utilities.Indexing
{
    /// <summary>
    /// A quad tree node.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class QuadtreeNode<T>
    {
        List<QuadtreeNode<T>> children = null;
        List<T> objects;
        int maxObjects;
        int level;
        int maxLevel;

        /// <summary>
        /// Gets or sets the left position.
        /// </summary>
        public double Left { get; set; }

        /// <summary>
        /// Gets or sets the right position.
        /// </summary>
        public double Right { get; set; }

        /// <summary>
        /// Gets or sets the right position.
        /// </summary>
        public double Top { get; set; }

        /// <summary>
        /// Gets or sets the bottom position.
        /// </summary>
        public double Bottom { get; set; }

        protected QuadtreeContainmentChecker<T> containmentChecker;

        public const int DEFAULT_MAX_OBJECTS_PER_NODE = 5;

        /// <summary>
        /// Initialize a new instance of <see cref="QuadtreeNode{T}"/>.
        /// </summary>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <param name="top"></param>
        /// <param name="bottom"></param>
        /// <param name="containmentChecker"></param>
        /// <param name="maxObjects"></param>
        /// <param name="level"></param>
        /// <param name="maxLevel"></param>
        public QuadtreeNode(double left, double right, double top, double bottom, QuadtreeContainmentChecker<T> containmentChecker, int maxObjects, int level, int maxLevel)
        {
            this.Left = left;
            this.Right = right;
            this.Top = top;
            this.Bottom = bottom;
            this.containmentChecker = containmentChecker;
            this.maxObjects = maxObjects;
            this.objects = new List<T>(maxObjects);
            this.level = level;
            this.maxLevel = maxLevel;
        }

        /// <summary>
        /// Initialize a new instance of <see cref="QuadtreeNode{T}"/>.
        /// </summary>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <param name="top"></param>
        /// <param name="bottom"></param>
        /// <param name="containmentChecker"></param>
        /// <param name="level"></param>
        /// <param name="maxLevel"></param>
        public QuadtreeNode(double left, double right, double top, double bottom, QuadtreeContainmentChecker<T> containmentChecker, int level, int maxLevel)
            : this(left, right, top, bottom, containmentChecker, QuadtreeNode<T>.DEFAULT_MAX_OBJECTS_PER_NODE, level, maxLevel)
        { }

        /// <summary>
        /// Insert an object.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public bool Insert(T obj)
        {
            if (this.IsFullyContained(obj))
            {
                if (this.IsLeaf())
                {
                    if (objects.Count >= maxObjects && level < maxLevel)
                    {
                        this.Divide();
                        if (!this.InsertIntoChildren(obj)) this.objects.Add(obj);
                    }
                    else
                    {
                        this.objects.Add(obj);

                    }
                }
                else
                {
                    if (!this.InsertIntoChildren(obj)) this.objects.Add(obj);
                }
                return true;
            }
            return false;
        }

        public bool IsFullyContained(T obj)
        {
            return this.containmentChecker.IsFullyContained(this, obj);
        }

        public bool CollidesWithOrContains(T obj)
        {
            return this.containmentChecker.IsContainedOrIntersects(this, obj);
        }

        public bool IsLeaf()
        {
            return this.children == null;
        }

        /// <summary>
        /// Get the childrens.
        /// </summary>
        /// <returns></returns>
        public List<QuadtreeNode<T>> GetChildren()
        {
            return this.children;
        }

        public bool CollidesWithAny(T obj)
        {
            if (this.CollidesWithOrContains(obj))
            {
                //collides with any of current level?
                foreach (T o in this.objects)
                {
                    if (this.containmentChecker.IsContainedOrIntersects(o, obj)) return true;
                }

                //collides with a child?
                if (!this.IsLeaf())
                {
                    for (int i = 0; i < 4; i++)
                    {
                        if (children[i].CollidesWithAny(obj)) return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Gets objects relative to position.
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public List<T> GetObjects(double x, double y)
        {
            if (this.IsLeaf())
            {
                return this.objects;
            }
            else
            {
                foreach (QuadtreeNode<T> node in this.children)
                {
                    if (x >= node.Left && x <= node.Right && y >= node.Bottom && y <= node.Top)
                    {
                        return node.GetObjects(x, y);
                    }
                }
            }
            return new List<T>();
        }

        /// <summary>
        /// Insert into children.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        protected bool InsertIntoChildren(T obj)
        {
            bool inserted = false;
            for (int i = 0; i < 4; i++)
            {
                if (children[i].Insert(obj)) inserted = true;
            }
            return inserted;
        }

        /// <summary>
        /// Divide node in quad nodes.
        /// </summary>
        protected void Divide()
        {
            this.children = new List<QuadtreeNode<T>>(4);
            double middleX = Left + ((Right - Left) / 2);
            double middleY = Top + (Math.Abs(Top - Bottom) / 2);
            this.children.Add(new QuadtreeNode<T>(Left, middleX, Top, middleY, containmentChecker, maxObjects, level + 1, maxLevel));
            this.children.Add(new QuadtreeNode<T>(middleX, Right, Top, middleY, containmentChecker, maxObjects, level + 1, maxLevel));
            this.children.Add(new QuadtreeNode<T>(Left, middleX, middleY, Bottom, containmentChecker, maxObjects, level + 1, maxLevel));
            this.children.Add(new QuadtreeNode<T>(middleX, Right, middleY, Bottom, containmentChecker, maxObjects, level + 1, maxLevel));

            //move objects to child nodes
            List<T> remainingObjects = new List<T>();
            foreach (T obj in this.objects)
            {
                if (!this.InsertIntoChildren(obj)) remainingObjects.Add(obj);
            }
            this.objects = remainingObjects;
        }

        /// <summary>
        /// Interface for quad tree containment checker.
        /// </summary>
        /// <typeparam name="P"></typeparam>
        public interface QuadtreeContainmentChecker<P>
        {
            bool IsContainedOrIntersects(QuadtreeNode<P> node, P obj);

            bool IsContainedOrIntersects(P obj1, P obj2);

            bool IsFullyContained(QuadtreeNode<P> node, P obj);
        }
    }

}
