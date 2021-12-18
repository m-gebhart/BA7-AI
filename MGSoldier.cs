using GameFramework;
using AI_Strategy_Utilities;
using System;
using System.Collections.Generic;

namespace AI_Strategy
{
    public class MGSoldier : Soldier
    {
        CentralSoldierData centralData;
        public MGSoldier(Player player, PlayerLane lane, int x) : base(player, lane, x) { }

        public override void Move()
        {
            if (speed > 0 && posY < PlayerLane.HEIGHT - 1)
            {
                int x = posX;
                int y = posY;
                Vector2Di newTargetPos = GetNewPosition();

                for (int i = speed; i > 0; i--)
                {
                    if (MoveTo(newTargetPos.xPos, newTargetPos.yPos)) break; ;
                    if (MoveTo(x, newTargetPos.yPos)) break; ;
                    if (MoveTo(newTargetPos.xPos, y)) break;
                    if (MoveTo(x + 1, y)) break;
                    if (MoveTo(x - 1, y)) break;
                    if (MoveTo(x, y)) break;
                }
            }
        }

        Vector2Di GetNewPosition() 
        {
            if (centralData == null)
                return ActionAutonomousMove();

            switch (centralData.currentAction) {
                default:
                case ESoldierAction.Autonomous:
                    return ActionAutonomousMove();
                case ESoldierAction.AttackTowers:
                    return ActionAttackTowers();
                case ESoldierAction.EvadeTower:
                    return ActionEvadeTowers();
            }
        }

        List<Vector2Di> GetUnitPositionsInFront(int seekRange, string typeID) 
        {
            List<Vector2Di> unitsInFront = new List<Vector2Di>();
            for (int y = seekRange + 1; y >= 0; y--)
            {
                for (int x = -seekRange; x <= seekRange; x++)
                {
                    if (lane.GetCellAt(x + posX, y + posY) != null)
                    {
                        Unit unit = lane.GetCellAt(x + posX, y + posY).Unit;
                        if (unit != null)
                        {
                            if (unit.Type == typeID)
                                unitsInFront.Add(new Vector2Di(unit.PosX, unit.PosY));
                        }
                    }
                }
            }
            return unitsInFront;
        }

        Vector2Di ActionAutonomousMove() 
        {
            //WALK STRAIGHT IF STILL IN SAFE ZONE
            if (posY <= PlayerLane.HEIGHT_OF_SAFETY_ZONE - 1)
                return new Vector2Di(posX, PosY + speed);

            //WALK STRAIGHT IF DEFENSIVE LINE OF ALLY SOLDIERS BUILD
            if (GetUnitPositionsInLine(posY, "S").Count > PlayerLane.WIDTH * 0.5f)
                return new Vector2Di(posX, PosY + speed);

            //JUST WALK STRAIGHT IF NO TOWERS DETECTED
            List<Vector2Di> enemyTowersInFront = GetUnitPositionsInFront(range, "T");
            if (enemyTowersInFront.Count == 0)
                return new Vector2Di(posX, PosY + speed);

            //OTHERWISE EVADE TOWERS AUTONOMOUSLY
            int averageTowerXPos = (int)Math.Round(StaticStrategyUtilities.GetAverageUnitLocation(lane, enemyTowersInFront).xPos);
            int moveXDirection = -1*Math.Sign((averageTowerXPos) - posX);
            if (averageTowerXPos + (range+1*moveXDirection) > PlayerLane.WIDTH || averageTowerXPos + (range+1*moveXDirection) < 0)
                moveXDirection *= -1;

            if (averageTowerXPos == posX)
                moveXDirection = Math.Sign(posX - PlayerLane.WIDTH/2);

            return new Vector2Di(PosX + moveXDirection * speed, PosY);
        }


        Vector2Di ActionEvadeTowers() 
        {
            if (GetUnitPositionsInLine(posY, "S").Count > PlayerLane.WIDTH * 0.5f)
                return new Vector2Di(posX, PosY + speed);
            //PROCESSING FROM CENTRAL DATA REFERENCE
            return new Vector2Di(centralData.targetPos.xPos, PosY);
        } 
        
        Vector2Di ActionAttackTowers() 
        {
            List<Vector2Di> towerPositions = GetUnitPositionsInFront(range, "T");
            if (towerPositions.Count > 0)
            {
                //COUNTING ALLIES CLOSE BY
                List<Vector2Di> allies = GetUnitPositionsCloseBy("S");

                //COUNTING ALLIES IN SAME LINE IF NOT ENOUGH NEAR BY
                if (allies.Count <= health / range)
                {
                    allies.Clear();
                    allies = GetUnitPositionsInLine(posY, "S");
                }

                //IF ALONE, WAITING FOR OTHER ALLIES TO BREACH THROUGH TOGETHER = NO MOVEMENT FOR NOW
                if (allies.Count <= health / range) 
                    return new Vector2Di(posX, posY);
            }

            return new Vector2Di(posX, posY+speed);
        }

        List<Vector2Di> GetUnitPositionsCloseBy(string unitType) 
        {
            List <Vector2Di> unitPositions = new List<Vector2Di>();
            for (int y = -1; y <= 1; y++)
            {
                for (int x = -range; x <= range; x++)
                {
                    if (lane.GetCellAt(x + posX, y + posY) != null)
                    {
                        Unit unit = lane.GetCellAt(x + posX, y + posY).Unit;
                        if (unit != null)
                        {
                            if (unit.Type == unitType)
                                unitPositions.Add(new Vector2Di(unit.PosX, unit.PosY));
                        }
                    }
                }
            }
            return unitPositions;
        }

        List<Vector2Di> GetUnitPositionsInLine(int yLinePosition, string unitType) 
        {
            List<Vector2Di> unitPositions = new List<Vector2Di>();
            for (int x = PlayerLane.WIDTH - 1; x >= 0; x--)
            {
                if (lane.GetCellAt(x, yLinePosition) != null)
                {
                    Unit unit = lane.GetCellAt(x, yLinePosition).Unit;
                    if (unit != null)
                    {
                        if (unit.Type == unitType)
                            unitPositions.Add(new Vector2Di(unit.PosX, unit.PosY));
                    }
                }
            }
            return unitPositions;
        } 


        public bool HasCentralReference() 
        {
            return centralData != null;
        }

        public void SetCentralReference(CentralSoldierData centralSoldierData) 
        {
            centralData = centralSoldierData;
        }
    }
}
