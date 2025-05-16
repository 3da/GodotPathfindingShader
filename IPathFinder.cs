using System.Collections.Generic;

namespace GodotPathfindingShader;

public interface IPathFinder
{
    void FindPaths(IList<PathFindItem> items);
    void SetMap(MapData mapData);
}