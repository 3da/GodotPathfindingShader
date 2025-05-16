using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GodotPathfindingShader
{
    public class MapData
    {
        /// <summary>
        /// Cells containing wall
        /// </summary>
        public bool[,] Cells { get; set; }

        /// <summary>
        /// Map width
        /// </summary>
        public int Width { get; set; }
        
        /// <summary>
        /// Map height
        /// </summary>
        public int Height { get; set; }
    }
}
