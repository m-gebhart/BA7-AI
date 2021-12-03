using System;
using GameFramework;
using AI_Strategy_Utilities;
using System.Collections.Generic;

namespace AI_Strategy
{
    enum EDefenseStrategy { defendBack, defendMiddle, defendFront };
    enum EStressLevel { low, middle, high};

    struct StrategySet 
    { 
        public float stressLevelValue; //own endangerment and enemy player's performance
        public int towersToBuy, soldiersToBuy;
        public EDefenseStrategy defenseStrategy;
        public EStressLevel stressLevel;
    };

    public class MyStrategy : AbstractStrategy
    {
        public static readonly int SOLDIER_COST = 2, SOLDIER_RANGE = 2;
        StrategySet activeStrategy;
        List<Unit> EnemySoldiers, EnemyTowers;
        public List<Vector2D> EnemySoldierPositions, EnemyTowerPositons;
        public MyStrategy(PlayerLane defendLane, PlayerLane attackLane, Player player) : base(defendLane, attackLane, player)
        {
            activeStrategy.stressLevel = 0f;
        }

        void CheckStressLevel()
        {
            int ownTowerCount = defendLane.TowerCount();
            foreach (Tower tower in EnemyTowers)
            {
                if (tower.Health < 3)
                    ownTowerCount--;
            }

            float enemySoldierCount = defendLane.SoldierCount();

            //STRESS LEVEL: performance of enemy soldiers + status of own towers compared to enemy towers
            float tempStressLevel = enemySoldierCount + Math.Max(0, attackLane.TowerCount() - ownTowerCount);
            activeStrategy.stressLevelValue = Math.Max(0, tempStressLevel);

            if (tempStressLevel < 5f)
                activeStrategy.stressLevel = EStressLevel.low;
            else if (tempStressLevel >= 5f && tempStressLevel < 9f)
                activeStrategy.stressLevel = EStressLevel.middle;
            else
                activeStrategy.stressLevel = EStressLevel.high;
        }

        void EvaluateBuyingBehaviour() 
        {
            activeStrategy.towersToBuy = 0;
            activeStrategy.soldiersToBuy = 0;
            int goldWallet = player.Gold;
            while (goldWallet > Tower.GetNextTowerCosts(defendLane) || goldWallet > SOLDIER_COST) 
            {
                //more defense with high stress level -> prioritize towers
                if (activeStrategy.stressLevel == EStressLevel.high) 
                {
                    while (goldWallet > Tower.GetNextTowerCosts(defendLane) + activeStrategy.towersToBuy * defendLane.TowerCount()) 
                    {
                        goldWallet -= Tower.GetNextTowerCosts(defendLane) + activeStrategy.towersToBuy * defendLane.TowerCount();
                        activeStrategy.towersToBuy++;
                    }
                    while (goldWallet > SOLDIER_COST)
                    {
                        goldWallet -= SOLDIER_COST;
                        activeStrategy.soldiersToBuy++;
                    }
                }

                else 
                { //50:50 soldiers and towers in accordance to money
                    if (goldWallet > Tower.GetNextTowerCosts(defendLane) + activeStrategy.towersToBuy * defendLane.TowerCount())
                    {
                        goldWallet -= Tower.GetNextTowerCosts(defendLane) + activeStrategy.towersToBuy * defendLane.TowerCount();
                        activeStrategy.towersToBuy++;
                    }
                    if (goldWallet > SOLDIER_COST)
                    {
                        goldWallet -= SOLDIER_COST;
                        activeStrategy.soldiersToBuy++;
                    }
                }
            }
            instantLogMessage = "S: " + activeStrategy.stressLevel.ToString() + "T: " + activeStrategy.towersToBuy + " S: " + activeStrategy.soldiersToBuy;
        }



        public override void DeployTowers()
        {
            UpdateEnemyTowers();
            CheckStressLevel();
            EvaluateBuyingBehaviour();
            while (activeStrategy.towersToBuy > 0)
            {
                if (player.Gold >= Tower.GetNextTowerCosts(defendLane))
                {
                    activeStrategy.defenseStrategy = GetDefenseStrategy();

                    int spawnYLine = GetTowerDeploymentYPos();
                    int spawnXLine = GetTowerDeploymentXPos();
                    //BUY IF INTENDED POSITION NOT OCCUPIED
                    if (CanBuyTower(defendLane, spawnXLine, spawnYLine))
                    {
                        Tower tower = player.BuyTower(defendLane, spawnXLine, spawnYLine);
                    }

                    else if (EnemySoldierPositions != null)
                    {   //ELSE: FIND FREE POSITION CLOSE BY
                        bool positioned = false;
                        int xpos = (int)Math.Round(GetAverageUnitLocation(defendLane, EnemySoldierPositions, EnemySoldierPositions.Count).xPos);
                        for (int y = 1; y >= -1; y--) {
                            for (int x = 1; x >= 0; x--)
                                if (CanBuyTower(defendLane, spawnXLine, spawnYLine))
                                {
                                    if (player.BuyTower(defendLane, spawnXLine, spawnYLine) != null)
                                        positioned = true;
                                } 
                        }
                        //ELSE: JUST PUT ANYWHERE IF NOWHERE CLOSE
                        if (!positioned)
                        {
                            int attempts = 5;
                            while (attempts > 0)
                            { 
                                Random random = new Random();
                                if (player.BuyTower(defendLane,
                                    StaticStrategyUtilities.SmoothTowerXPosition(random.Next(0, PlayerLane.WIDTH - 1)),
                                    StaticStrategyUtilities.SmoothTowerYPosition(random.Next(PlayerLane.HEIGHT_OF_SAFETY_ZONE, PlayerLane.HEIGHT - 1))) != null)
                                        positioned = true;
                                attempts--;
                            }
                        }
                    }
                }
                activeStrategy.towersToBuy--;
            }
        }

        bool CanBuyTower(PlayerLane lane, int x, int y) //copied from Player.cs
        {
            return y >= PlayerLane.HEIGHT_OF_SAFETY_ZONE && 
                y < PlayerLane.HEIGHT &&
                x >= 0 && x < PlayerLane.WIDTH &&
                lane.GetCellAt(x, y).Unit == null &&
                (y % 2) != 0 && (x % 2) == 0;
        }

        int GetTowerDeploymentYPos() 
        {
            int yPos = 0;
            Random yRandom = new Random();

            //IF BOTTOM SOLDIER CLOSE TO FINISH LINE; BUILD AHEAD
            if (IsCriticallyClose(GetBottomEnemySoldier().yPos))
            {
                int bottomEnemyYPos = (int)GetBottomEnemySoldier().yPos;
                int randomYOffset = yRandom.Next(2, 5);
                if (randomYOffset + bottomEnemyYPos > PlayerLane.HEIGHT - 1)
                    yPos = PlayerLane.HEIGHT - 1;
                else
                    yPos = randomYOffset + bottomEnemyYPos;
            }
            //ELSE: BUILD DEFENSIVE LINES
            else
            {
                switch (activeStrategy.defenseStrategy)
                {
                    default:
                    case EDefenseStrategy.defendFront: // 0-40% of lane
                        yPos = yRandom.Next(PlayerLane.HEIGHT_OF_SAFETY_ZONE, (int)Math.Round(PlayerLane.HEIGHT * 0.4f));
                        break;
                    case EDefenseStrategy.defendMiddle: // 40-70% of lane
                        yPos = yRandom.Next((int)Math.Round(PlayerLane.HEIGHT * 0.4f), (int)Math.Round(PlayerLane.HEIGHT * 0.7f));
                        break;
                    case EDefenseStrategy.defendBack: // 70-100% of lane
                        yPos = yRandom.Next((int)Math.Round(PlayerLane.HEIGHT * 0.7f), PlayerLane.HEIGHT);
                        break;
                }
            }
            yPos = StaticStrategyUtilities.SmoothTowerYPosition(yPos);
            return yPos;
        }

        int GetTowerDeploymentXPos() 
        {
            int xPos = PlayerLane.WIDTH/2; //default: mid
            //IF BOTTOM SOLDIER TO CLOSE TO GOAL; SET TOWER CLOSE
            if (IsCriticallyClose(GetBottomEnemySoldier().yPos))
                xPos = (int)GetBottomEnemySoldier().xPos;
            else if (EnemySoldierPositions != null) //ELSE: GET AVERAGE POSITION OF ALL ENEMIES
                xPos = (int)Math.Round(GetAverageUnitLocation(defendLane, EnemySoldierPositions, EnemySoldierPositions.Count).xPos);
            xPos = StaticStrategyUtilities.SmoothTowerXPosition(xPos);
            return xPos;
        }

        Vector2D GetAverageUnitLocation(PlayerLane lane, List<Vector2D> unitPositionList, int count)
        {
            float xAverage = 0;
            float yAverage = 0;
            foreach (Vector2D vector2D in EnemySoldierPositions)
            {
                xAverage += vector2D.xPos;
                if (IsCriticallyClose(vector2D.yPos))
                    yAverage += vector2D.yPos * 1.5f;
                else
                    yAverage += vector2D.yPos;
            }
            return new Vector2D(xAverage /= count, yAverage /= count);
        }

        EDefenseStrategy GetDefenseStrategy() 
        {
            switch (activeStrategy.stressLevel) 
            {
                case EStressLevel.low:
                    return EDefenseStrategy.defendFront;
                case EStressLevel.middle:
                    return EDefenseStrategy.defendMiddle;
                case EStressLevel.high:
                default:
                    return EDefenseStrategy.defendBack;
            }
        }

        public override void DeploySoldiers()
        {
            UpdateEnemySoldiers();
            CheckStressLevel();

            int averageEnemyTowerPosition = PlayerLane.WIDTH / 2; //default
            if (EnemyTowerPositons != null)
                averageEnemyTowerPosition = (int)Math.Round(GetAverageUnitLocation(attackLane, EnemyTowerPositons, EnemyTowerPositons.Count - 1).xPos);

            Random random = new Random();

            while (activeStrategy.soldiersToBuy > 0) 
            {
                //choose random side that is not average position of enemy towers
                int x = random.Next(PlayerLane.WIDTH);
                if (x == averageEnemyTowerPosition)
                    x = x + SOLDIER_RANGE * Math.Sign(averageEnemyTowerPosition - ((PlayerLane.WIDTH - 1) / 2));
                x = StaticStrategyUtilities.SmoothWidthPosition(x);

                if (attackLane.GetCellAt(x, 0).Unit == null)
                {
                    if(player.BuySoldier(attackLane, x) == null);
                        activeStrategy.soldiersToBuy++; //try again
                }
                activeStrategy.soldiersToBuy--;
            }
        }

        public override List<Soldier> SortedSoldierArray(List<Soldier> unsortedList)
        {
            return unsortedList;
        }

        void UpdateEnemySoldiers()
        {
            EnemySoldiers = StaticStrategyUtilities.CheckEnemyUnits("S", defendLane);
            EnemySoldierPositions = GetEnemySoldierPositions();
        }

        void UpdateEnemyTowers()
        {
            EnemyTowers = StaticStrategyUtilities.CheckEnemyUnits("T", attackLane);
            EnemyTowerPositons = GetEnemyTowerPositions();
        }

        List<Vector2D> GetEnemySoldierPositions() 
        {
            return StaticStrategyUtilities.GetEnemyPositions(EnemySoldiers);
        }

        Vector2D GetBottomEnemySoldier() 
        {
            if (EnemySoldierPositions != null && EnemySoldierPositions.Count > 0)
                return EnemySoldierPositions[EnemySoldierPositions.Count - 1];
            return new Vector2D(0f, 0f);
        }

        List<Vector2D> GetEnemyTowerPositions()
        {
            return StaticStrategyUtilities.GetEnemyPositions(EnemyTowers);
        }

        bool IsCriticallyClose(float yPos) 
        {
            return yPos > PlayerLane.HEIGHT * 0.6f;
        }

        bool IsCriticallyClose(int yPos)
        {
            return yPos > PlayerLane.HEIGHT * 0.6f;
        }
    }
}
