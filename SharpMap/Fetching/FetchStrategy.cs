﻿using BruTile;
using SharpMap.Utilities;
using System.Collections.Generic;
using System.Linq;

namespace SharpMap.Fetching
{
    class FetchStrategy : IFetchStrategy
    {
        public IList<TileInfo> GetTilesWanted(ITileSchema schema, Extent extent, string levelId)
        {
            
            var infos = new List<TileInfo>();
            // Iterating through all levels from current to zero. If lower levels are
            // not available the renderer can fall back on higher level tiles. 
            var resolution = schema.Resolutions[levelId].UnitsPerPixel;
            var levels = schema.Resolutions.Where(k => k.Value.UnitsPerPixel >= resolution).OrderBy(x => x.Value.UnitsPerPixel).ToList();

            foreach (var level in levels)
            {
                var tileInfos = schema.GetTileInfos(extent, level.Key).OrderBy(
                    t => Algorithms.Distance(extent.CenterX, extent.CenterY, t.Extent.CenterX, t.Extent.CenterY));

                infos.AddRange(tileInfos.Where(info => (info.Index.Row >= 0) && (info.Index.Col >= 0)));
            }
            
            return infos;
        }
    }
}
