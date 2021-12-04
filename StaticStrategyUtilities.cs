using System;
using GameFramework;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AI_Strategy_Utilities
{
    public static class StaticStrategyUtilities
    {
        public static List<Unit> GetUnitsOfType(string type, PlayerLane lane)
        {
            //workaround because lists of PlayerLane.cs are not accessible
            List<Unit> tempUnitList = new List<Unit>();
            int row = 0;
            while (row < PlayerLane.HEIGHT)
            {
                int column = 0;
                while (column < PlayerLane.WIDTH)
                {
                    Unit tempUnit = lane.GetCellAt(column, row).Unit;
                    if (tempUnit != null && tempUnit.Type == type)
                        tempUnitList.Add(tempUnit);

                    column++;
                }
                row++;
            }
            return tempUnitList;
        }

        public static List<Vector2D> GetUnitPositions(List<Unit> unitList)
        {
            List<Vector2D> tempVectorPositions = new List<Vector2D>();
            foreach (Unit unit in unitList)
            {
                tempVectorPositions.Add(new Vector2D(unit.PosX, unit.PosY));
            }
            return tempVectorPositions;
        }

        public static int SmoothTowerYPosition(int originYPos) 
        {
            if (originYPos % 2 == 0)
                return ++originYPos;
            return originYPos;
        }

        public static int SmoothHeightPosition(int originYPos)
        {
            if (originYPos < PlayerLane.HEIGHT_OF_SAFETY_ZONE-1)
                return PlayerLane.HEIGHT_OF_SAFETY_ZONE - 1;
            else if (originYPos > PlayerLane.HEIGHT - 1)
                return PlayerLane.HEIGHT - 1;
            return originYPos;
        }

        public static int SmoothTowerXPosition(int originXPos)
        {
            originXPos = SmoothWidthPosition(originXPos);
            if (originXPos % 2 == 1)
                return ++originXPos;
            return originXPos;
        }

        public static int SmoothWidthPosition(int originXPos) 
        {
            if (originXPos < 0)
                return 0;
            else if (originXPos > PlayerLane.WIDTH - 1)
                return PlayerLane.WIDTH - 1;
            return originXPos;
        }
    }

    public struct Vector2D
    {
        public float xPos, yPos;
        public Vector2D(float x, float y)
        {
            this.xPos = x;
            this.yPos = y;
        }
    }
}
