using System;
using GameFramework;
using AI_Strategy;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AI_Strategy_Utilities
{
    public static class StaticStrategyUtilities
    {
        //LIBRARY OF STATIC FUNCTIONS 
        public static List<Unit> GetUnitsOfType(string type, PlayerLane lane)
        {
            //WORKAROUND BECAUSE LISTS OF PLAYERLANE.CS ARE NOT ACCESSIBLE
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

        public static List<Vector2Di> GetUnitPositions(List<Unit> unitList)
        {
            List<Vector2Di> tempVectorPositions = new List<Vector2Di>();
            foreach (Unit unit in unitList)
            {
                tempVectorPositions.Add(new Vector2Di(unit.PosX, unit.PosY));
            }
            return tempVectorPositions;
        }

        public static int SmoothTowerYPosition(int originYPos) 
        {
            originYPos = SmoothHeightPosition(originYPos);
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

        public static bool IsHeightPosValid(int yPos) 
        {
            return yPos >= 0 && yPos < PlayerLane.HEIGHT;
        }

        public static bool IsWidthPosValid(int xPos)
        {
            return xPos >= 0 && xPos < PlayerLane.WIDTH;
        }

        public static Vector2Df GetAverageUnitLocation(PlayerLane lane, List<Vector2Di> unitPositionList)
        {
            return GetAverageUnitLocation(lane,unitPositionList, 0f);
        }

        public static Vector2Df GetAverageUnitLocation(PlayerLane lane, List<Vector2Di> unitPositionList, float critValue)
        {
            float xAverage = 0;
            float yAverage = 0;
            foreach (Vector2Di vector2D in unitPositionList)
            {
                xAverage += vector2D.xPos;
                if (MGStrategy.IsCriticallyClose(vector2D.yPos) && critValue != 0f)
                    yAverage += vector2D.yPos * critValue;
                else
                    yAverage += vector2D.yPos;
            }
            return new Vector2Df(xAverage /= unitPositionList.Count, yAverage /= unitPositionList.Count);
        }
    }

    public struct Vector2Df
    {
        public float xPos, yPos;
        public Vector2Df(float x, float y)
        {
            this.xPos = x;
            this.yPos = y;
        }
    }

    public struct Vector2Di 
    {
        public int xPos, yPos;
        public Vector2Di(int x, int y) 
        {
            this.xPos = x;
            this.yPos = y;
        }
    }
}
