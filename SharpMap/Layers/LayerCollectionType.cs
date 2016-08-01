using System;
using System.Collections.Generic;
using System.Timers;

namespace SharpMap.Layers
{
    /// <summary>
    /// Types of layer collections
    /// </summary>
    public enum LayerCollectionType
    {
        /// <summary>
        /// Layer collection for layers with datasources that are more or less static (e.g ShapeFiles)
        /// </summary>
        Static,

        /// <summary>
        /// Layer collection for layers are completely opaque and serve as Background (e.g. WMS, OSM)
        /// </summary>
        Background,
    }
    
    

}