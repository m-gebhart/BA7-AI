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
        public static readonly int SOLDIER_COST = 2, SOLDIER_RANGE = 2, TOWER_SPAWN_DISTANCE = 2;
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
            if (EnemyTowers != null)
            {
                foreach (Tower tower in EnemyTowers)
                {
                    if (tower.Health < 3)
                        ownTowerCount--;
                }
            }

            float enemySoldierCount = defendLane.SoldierCount();
            if (EnemySoldiers != null)
            {
                foreach (Soldier soldier in EnemySoldiers)
                {
                    if (IsCriticallyClose(soldier.PosY))
                        enemySoldierCount++;
                }
            }

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

        void EvaluateBuyingBehaviour() 
        {
            activeStrategy.towersToBuy = 0;
            activeStrategy.soldiersToBuy = 0;
            int goldWallet = player.Gold;
            while (goldWallet > Tower.GetNextTowerCosts(defendLane) || goldWallet > SOLDIER_COST) 
            {
                //more defense with high stress level -> prioritize towers, then buy a few soldiers on top
                if (activeStrategy.stressLevel == EStressLevel.high) 
                {
                    while (goldWallet > Tower.GetNextTowerCosts(defendLane) + activeStrategy.towersToBuy * defendLane.TowerCount()) 
                    {
                        goldWallet -= Tower.GetNextTowerCosts(defendLane) + activeStrategy.towersToBuy * defendLane.TowerCount();
                        activeStrategy.towersToBuy++;
                    }

                    Random random = new Random();
                    for (int randomSoldierNumber = random.Next(0, 3); randomSoldierNumber > 0; randomSoldierNumber--) 
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
                    Tower tower = player.BuyTower(defendLane, spawnXLine, spawnYLine);
                    if (tower == null)
                    {
                        //INTENDED POSITION ALREADY OCCUPIED
                        Vector2D altPos = GetFreeTowerPosition(spawnXLine, spawnYLine);
                        player.BuyTower(defendLane, (int)altPos.xPos, (int)altPos.yPos);
                    }
                    activeStrategy.towersToBuy--;
                }
            } 
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
            //IF BOTTOM SOLDIER TO CLOSE TO GOAL; GET SAME X POSITION
            if (IsCriticallyClose(GetBottomEnemySoldier().yPos))
                xPos = (int)GetBottomEnemySoldier().xPos;
            else if (EnemySoldierPositions != null) //ELSE: GET AVERAGE X POSITION OF ALL ENEMIES
                xPos = (int)Math.Round(GetAverageUnitLocation(defendLane, EnemySoldierPositions, EnemySoldierPositions.Count).xPos);
            xPos = StaticStrategyUtilities.SmoothTowerXPosition(xPos);
            return xPos;
        }

        Vector2D GetFreeTowerPosition(int originXLine, int spawnYLine)
        {
            if (defendLane.GetCellAt(originXLine, spawnYLine) != null)
                if (defendLane.GetCellAt(originXLine, spawnYLine).Unit == null)
                    return new Vector2D(originXLine, spawnYLine);

            List<Vector2D> towersOnYLine = new List<Vector2D>();
            List<Unit> deployedTowers = StaticStrategyUtilities.GetUnitsOfType("T", defendLane);
            if (deployedTowers.Count == 0)
                return new Vector2D(originXLine, spawnYLine);

            foreach (Vector2D vector2D in StaticStrategyUtilities.GetUnitPositions(deployedTowers))
            {
                if (vector2D.yPos == spawnYLine)
                    towersOnYLine.Add(vector2D);
            }

            Vector2D freeCell = new Vector2D();
            int enemyWeightXPos = EnemySoldierPositions != null ? (int)Math.Round(GetAverageUnitLocation(defendLane, EnemySoldierPositions, EnemySoldierPositions.Count).xPos) : PlayerLane.WIDTH / 2;
            int signDirection = Math.Sign(enemyWeightXPos - originXLine);
            //if still space on y Line
            if (towersOnYLine.Count < PlayerLane.WIDTH / 2)
            {
                bool positionFound = false;
                for (int attempts = 1; attempts <= PlayerLane.WIDTH / 2 && !positionFound; attempts++)
                {
                    int attemptX = StaticStrategyUtilities.SmoothWidthPosition(originXLine + signDirection * TOWER_SPAWN_DISTANCE * attempts);
                    if (defendLane.GetCellAt(attemptX, spawnYLine) == null)
                    {
                        freeCell = new Vector2D(attemptX, spawnYLine);
                        positionFound = true; break;
                    }
                    else
                    {
                        attemptX = StaticStrategyUtilities.SmoothWidthPosition(originXLine + (-1) * signDirection * TOWER_SPAWN_DISTANCE * attempts);
                        if (defendLane.GetCellAt(attemptX, spawnYLine) == null)
                        {
                            freeCell = new Vector2D(attemptX, spawnYLine);
                            positionFound = true; break;
                        }
                    }
                }
            }

            else //move up - recursive
            {
                Random random = new Random();
                int newYAttempt = StaticStrategyUtilities.SmoothHeightPosition(spawnYLine + TOWER_SPAWN_DISTANCE * random.Next(1, 3));
                GetFreeTowerPosition(originXLine, newYAttempt);
            }

            return freeCell;
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
            EnemySoldiers = StaticStrategyUtilities.GetUnitsOfType("S", defendLane);
            EnemySoldierPositions = GetEnemySoldierPositions();
        }

        void UpdateEnemyTowers()
        {
            EnemyTowers = StaticStrategyUtilities.GetUnitsOfType("T", attackLane);
            EnemyTowerPositons = GetEnemyTowerPositions();
        }

        List<Vector2D> GetEnemySoldierPositions() 
        {
            return StaticStrategyUtilities.GetUnitPositions(EnemySoldiers);
        }

        Vector2D GetBottomEnemySoldier() 
        {
            if (EnemySoldierPositions != null && EnemySoldierPositions.Count > 0)
                return EnemySoldierPositions[EnemySoldierPositions.Count - 1];
            return new Vector2D(0f, 0f);
        }

        List<Vector2D> GetEnemyTowerPositions()
        {
            return StaticStrategyUtilities.GetUnitPositions(EnemyTowers);
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
