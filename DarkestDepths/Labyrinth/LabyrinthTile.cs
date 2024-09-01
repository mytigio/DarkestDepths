using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DarkestDepths.Labyrinth
{
    public class LabyrinthTile
    {



        public Vector2 Point { get; set; }
        public int Walls { get; set; } = Wall.All;

        public LabyrinthTile(Vector2 point, int wallConfiguration)
        {
            Point = point;
            Walls = wallConfiguration;

        }
    }
}
