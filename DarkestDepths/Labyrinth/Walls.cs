using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DarkestDepths.Labyrinth
{
    public static class Wall
    {
        //the basic directions.
        public const int North = 1;
        public const int East = 2;
        public const int South = 4;
        public const int West = 8;

        //various combinations of sides
        public const int None = 0;
        public const int All = North | East | South | West;

        //corners
        public const int NorthEast = North | East;
        public const int NorthWest = North | West;
        public const int SouthEast = South | East;
        public const int SouthWest = South | West;

        public static bool isCorner(int wallConfig)
        {
            return wallConfig == NorthEast || wallConfig == NorthWest || wallConfig == SouthEast || wallConfig == SouthWest;
        }


        //straitaways
        public const int Verticle = East | West;
        public const int Horizontal = North | South;

        public static bool isStraight(int wallConfig)
        {
            return wallConfig == Verticle || wallConfig == Horizontal;
        }

        //endcaps
        public const int LeftEnd = North | West | South;
        public const int RightEnd = North | East | South;
        public const int NorthEnd = West | North | East;
        public const int SouthEnd = East | South | West;

        public static bool isEndCap(int wallConfig)
        {
            return wallConfig == LeftEnd || wallConfig == RightEnd || wallConfig == NorthEnd || wallConfig == SouthEnd;
        }

        //t-junctions
        public const int LeftT = West;
        public const int RightT = East;
        public const int TopT = North;
        public const int BottomT = South;

        public static bool isTJunction(int wallConfig)
        {
            return wallConfig == LeftT || wallConfig == RightT || wallConfig == TopT || wallConfig == BottomT;
        }
    }
}
