using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Godot;

namespace GodotPathfindingShader
{
    public class PathFindItem
    {
        public PathFindItem()
        {
        }

        public PathFindItem(Vector2I a, Vector2I b)
        {
            A = a;
            B = b;
        }

        public Vector2I A { get; init; }
        public Vector2I B { get; init; }
        public Vector2I[] Path { get; set; }
    }
}
