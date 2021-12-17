using System;
using System.Collections.Generic;
using AI_Strategy_Utilities;
using GameFramework;
using System.Linq;
using System.Text;

namespace AI_Strategy
{
    public enum ESoldierAction { Autonomous, AttackTowers, EvadeTower };

    public class CentralSoldierData
    {
        static int TOWER_ATTACK_RANGE = 2;
        public ESoldierAction currentAction;
        public Vector2Di targetPos;
        List<Vector2Di> enemyTowers;
        List<MGSoldier> attackSoldiers;
        PlayerLane attackLane;

        public CentralSoldierData(PlayerLane lane) 
        {
            currentAction = ESoldierAction.Autonomous;
            attackLane = lane;
        }

        public List<Vector2Di> GetEnemyTowers() 
        {
            return enemyTowers;
        }

        public void UpdateData(List<Vector2Di> enemyTowerPositions, List<MGSoldier> soldierList)
        {
            UpdateEnemyData(enemyTowerPositions);
            UpdateSoldierData(soldierList);
            UpdateSoldierAction();
        }

        void UpdateEnemyData(List<Vector2Di> enemyTowerPositions) 
        {
            enemyTowers = enemyTowerPositions;
        }

        void UpdateSoldierData(List<MGSoldier> soldierList) 
        {
            foreach(MGSoldier soldier in soldierList) 
            {
                if (!soldier.HasCentralReference())
                    soldier.SetCentralReference(this);
            }
            attackSoldiers = soldierList;
        }

        ESoldierAction GetAction()
        {
            if (attackLane.TowerCount() > 0)
            {
                if (attackLane.TowerCount() > PlayerLane.WIDTH / 2)
                {
                    if (attackLane.SoldierCount() < attackLane.TowerCount())
                        return ESoldierAction.EvadeTower;
                    return ESoldierAction.AttackTowers;
                }
            }
            return ESoldierAction.Autonomous;
        }

        void UpdateSoldierAction() 
        {
            currentAction = GetAction();
            if (currentAction == ESoldierAction.EvadeTower)
            {
                //find vertical part of lane without towers or least towers as possible 
                int leastTowerCounter = PlayerLane.HEIGHT/2;
                int xWithLeastTowers = 0;
                for (int x = 0; x < PlayerLane.WIDTH-1; x++) 
                {
                    int columnTowerCounter = 0;
                    for (int y = 0; y < PlayerLane.HEIGHT; y++) 
                    {
                        for (int closeRange = -TOWER_ATTACK_RANGE; closeRange < TOWER_ATTACK_RANGE; closeRange++) 
                        {
                            Cell tempCell = attackLane.GetCellAt(x + closeRange, y);
                            if (tempCell != null)
                            {
                                if (tempCell.Unit != null)
                                {
                                    if (tempCell.Unit.Type == "T")
                                    {
                                        columnTowerCounter++;
                                    }
                                }
                            }
                        }
                    }
                    if (columnTowerCounter < leastTowerCounter)
                    {
                        leastTowerCounter = columnTowerCounter;
                        xWithLeastTowers = x;
                    }
                }
                targetPos = new Vector2Di(xWithLeastTowers, 0);
            }
            AI_TowerDefense.TowerDefense.LOG_Message += currentAction.ToString();
        }

    }
}
