using System;
using SharpMap.Layers;

namespace SharpMap.Rendering
{
    /// <summary>
    /// Layer rendering event arguments class
    /// </summary>
    public class LayerRenderingEventArgs : EventArgs
    {
        /// <summary>
        /// The layer that is being or has been rendered
        /// </summary>
        public readonly ILayer Layer;

        /// <summary>
        /// The layer collection type the layer belongs to.
        /// </summary>
        public readonly LayerCollectionType LayerCollectionType;

        /// <summary>
        /// Creates an instance of this class
        /// </summary>
        /// <param name="layer">The layer that is being or has been rendered</param>
        public LayerRenderingEventArgs(ILayer layer)
        {
            Layer = layer;
        }

        /// <summary>
        /// Creates an instance of this class
        /// </summary>
        /// <param name="layer">The layer that is being or has been rendered</param>
        /// <param name="layerCollectionType">The layer collection type the layer belongs to.</param>
        public LayerRenderingEventArgs(ILayer layer, LayerCollectionType layerCollectionType)
        {
            Layer = layer;
            LayerCollectionType = layerCollectionType;
        }
    }
    /// <summary>
    /// Map rendering event arguments class
    /// </summary>
    public class MapRenderingEventArgs : EventArgs
    {
        
    }

}