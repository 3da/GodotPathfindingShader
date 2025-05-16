using System.Collections.Generic;
using System.Formats.Tar;
using System.Threading.Tasks;
using Godot;

namespace GodotPathfindingShader
{
    public class CpuPathFinder : IPathFinder
    {
        private MapData _mapData;
        private readonly int _maxIterations;

        public CpuPathFinder(int maxIterations = 10000)
        {
            _maxIterations = maxIterations;
        }

        public void FindPaths(IList<PathFindItem> items)
        {
            if (_mapData?.Cells == null)
                return;
            Parallel.ForEach(items, item =>
            {
                var moveDirections = new Vector2I[]
                {
                    new(-1, 0),
                    new(1, 0),
                    new(0, -1),
                    new(0, 1)
                };

                var mapRect = new Rect2I(0, 0, _mapData.Width, _mapData.Height);


                var buffer = new uint[_mapData.Width, _mapData.Height];
                var cells = _mapData.Cells;

                var b = item.B;

                uint iteration = 1;

                var points = new List<Vector2I>
                {
                    item.A
                };

                bool exitFound = false;

                while (iteration < _maxIterations && !exitFound)
                {
                    var nextPoints = new List<Vector2I>();
                    foreach (var a in points)
                    {
                        buffer[a.X, a.Y] = iteration;


                        foreach (var moveDirection in moveDirections)
                        {
                            var point = new Vector2I(a.X + moveDirection.X, a.Y + moveDirection.Y);

                            if (point == b)
                            {
                                exitFound = true;
                            }

                            if (!mapRect.HasPoint(point))
                                continue;

                            if (cells[point.X, point.Y])
                                continue;

                            if (buffer[point.X, point.Y] > 0)
                                continue;

                            buffer[point.X, point.Y] = iteration;
                            nextPoints.Add(point);
                        }


                    }
                    iteration++;
                    points = nextPoints;
                }

                if (exitFound)
                {
                    var movePath = new Vector2I[iteration + 1];
                    item.Path = movePath;

                    var point = b;

                    movePath[iteration] = b;
                    movePath[0] = item.A;

                    for (uint i = iteration - 1; i > 0; i--)
                    {
                        foreach (var moveDirection in moveDirections)
                        {
                            var nextPoint = point + moveDirection;

                            if (!mapRect.HasPoint(nextPoint))
                                continue;

                            if (buffer[nextPoint.X, nextPoint.Y] == i)
                            {
                                movePath[i] = nextPoint;
                                point = nextPoint;
                                break;
                            }
                        }
                    }
                }
                else
                {

                }

            });


        }

        public void SetMap(MapData mapData)
        {
            _mapData = mapData;
        }
    }
}
