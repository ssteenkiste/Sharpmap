using BruTile;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SharpMap.Fetching
{

    /// <summary>
    /// Interface for fetching startegy
    /// </summary>
    public interface IFetchStrategy
    {

        /// <summary>
        /// Get the tiles.
        /// </summary>
        /// <param name="schema">The tile schema.</param>
        /// <param name="extent">The extend.</param>
        /// <param name="levelId">The levelID.</param>
        /// <returns></returns>
        IList<TileInfo> GetTilesWanted(ITileSchema schema, Extent extent, string levelId);
    }

}
