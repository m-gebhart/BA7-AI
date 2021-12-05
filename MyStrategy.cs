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
        public List<Vector2Di> EnemySoldierPositions, EnemyTowerPositons;
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
            if (goldWallet > Tower.GetNextTowerCosts(defendLane) || goldWallet > SOLDIER_COST) 
            {
                //more defense with high stress level -> prioritize towers, then mb buy a few soldiers on top
                if (activeStrategy.stressLevel == EStressLevel.high) 
                {
                    while (goldWallet > Tower.GetNextTowerCosts(defendLane) + activeStrategy.towersToBuy * defendLane.TowerCount()) 
                    {
                        goldWallet -= Tower.GetNextTowerCosts(defendLane) + activeStrategy.towersToBuy * defendLane.TowerCount();
                        activeStrategy.towersToBuy++;
                    }

                    Random random = new Random();
                    for (int randomSoldierNumber = random.Next(0, 2); randomSoldierNumber > 0; randomSoldierNumber--) 
                    {
                        goldWallet -= SOLDIER_COST;
                        activeStrategy.soldiersToBuy++;
                    }
                }

                else 
                { //balancing soldiers and towers in accordance to money
                    for (int i = goldWallet / SOLDIER_COST; i > 0; i--) 
                    {
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
                        Random random = new Random();
                        int yAttempts = random.Next(1, 2);
                        bool positionFound = false;
                        int signDirection = activeStrategy.defenseStrategy == EDefenseStrategy.defendFront ? 1 : -1;

                        for (int attempt = 1; attempt <= yAttempts && !positionFound; attempt++)
                        {
                            //INTENDED POSITION ALREADY OCCUPIED
                            Vector2Di altPos = GetFreeTowerXPositionInLine(spawnXLine, spawnYLine + attempt * signDirection * TOWER_SPAWN_DISTANCE);
                            tower = player.BuyTower(defendLane, altPos.xPos, altPos.yPos);
                            if (tower != null)
                                positionFound = true;
                        }
                    }
                }
                activeStrategy.towersToBuy--;
            }
        }

        int GetTowerDeploymentYPos() 
        {
            int yPos = 0;
            Random yRandom = new Random();

            //IF BOTTOM SOLDIER CLOSE TO FINISH LINE; BUILD AHEAD
            if (IsCriticallyClose(GetBottomEnemySoldierPosition().yPos))
            {
                int bottomEnemyYPos = GetBottomEnemySoldierPosition().yPos;
                int randomYOffset = StaticStrategyUtilities.SmoothHeightPosition(yRandom.Next(0, 3) + bottomEnemyYPos);
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
                    case EDefenseStrategy.defendMiddle: // 40-60% of lane
                        yPos = yRandom.Next((int)Math.Round(PlayerLane.HEIGHT * 0.4f), (int)Math.Round(PlayerLane.HEIGHT * 0.6f));
                        break;
                    case EDefenseStrategy.defendBack: // 60-100% of lane
                        yPos = yRandom.Next((int)Math.Round(PlayerLane.HEIGHT * 0.6f), PlayerLane.HEIGHT);
                        break;
                }
            }
            return StaticStrategyUtilities.SmoothTowerYPosition(yPos); ;
        }

        int GetTowerDeploymentXPos() 
        {
            int xPos = PlayerLane.WIDTH/2; //default: mid
            //IF BOTTOM SOLDIER TOO CLOSE TO FINISH LINE; GET SAME X POSITION
            if (IsCriticallyClose(GetBottomEnemySoldierPosition().yPos))
                xPos = GetBottomEnemySoldierPosition().xPos;
            else if (EnemySoldierPositions != null) //ELSE: GET AVERAGE X POSITION OF ALL ENEMIES
                xPos = (int)Math.Round(StaticStrategyUtilities.GetAverageUnitLocation(defendLane, EnemySoldierPositions, EnemySoldierPositions.Count).xPos);

            return StaticStrategyUtilities.SmoothTowerXPosition(xPos);
        }

        Vector2Di GetFreeTowerXPositionInLine(int originXLine, int spawnYLine)
        {
            if (defendLane.GetCellAt(originXLine, spawnYLine) != null)
                if (defendLane.GetCellAt(originXLine, spawnYLine).Unit == null)
                    return new Vector2Di(originXLine, spawnYLine);

            //get established towers on lane (due to inaccessible list 'tower' in PlayerLane.cs)
            List<Vector2Di> towersOnYLine = new List<Vector2Di>();
            List<Unit> deployedTowers = StaticStrategyUtilities.GetUnitsOfType("T", defendLane);
            if (deployedTowers.Count == 0)
                return new Vector2Di(originXLine, spawnYLine);

            foreach (Vector2Di vector2D in StaticStrategyUtilities.GetUnitPositions(deployedTowers))
            {
                if (vector2D.yPos == spawnYLine)
                    towersOnYLine.Add(vector2D);
            }

            //search direction of new position (towards enemy center)
            Vector2Di freeCell = new Vector2Di();
            int enemyWeightXPos = EnemySoldierPositions != null ? (int)Math.Round(StaticStrategyUtilities.GetAverageUnitLocation(defendLane, EnemySoldierPositions, EnemySoldierPositions.Count).xPos) : PlayerLane.WIDTH / 2;
            int signDirection = Math.Sign(enemyWeightXPos - originXLine);

            //if still space on y Line, search from center to border from intended x Position
            if (towersOnYLine.Count < PlayerLane.WIDTH / TOWER_SPAWN_DISTANCE)
            {
                bool positionFound = false;
                for (int attempts = 1; attempts <= PlayerLane.WIDTH / 2 && !positionFound; attempts++)
                {
                    //search in direction of enemy center
                    int attemptX = StaticStrategyUtilities.SmoothWidthPosition(originXLine + signDirection * TOWER_SPAWN_DISTANCE * attempts);
                    if (defendLane.GetCellAt(attemptX, spawnYLine) == null)
                    {
                        freeCell = new Vector2Di(attemptX, spawnYLine);
                        positionFound = true; break;
                    }
                    //search in opposite direction of enemy center
                    else
                    {
                        attemptX = StaticStrategyUtilities.SmoothWidthPosition(originXLine + (-1) * signDirection * TOWER_SPAWN_DISTANCE * attempts);
                        if (defendLane.GetCellAt(attemptX, spawnYLine) == null)
                        {
                            freeCell = new Vector2Di(attemptX, spawnYLine);
                            positionFound = true; break;
                        }
                    }
                }
            }

            return freeCell;
        }

        public override void DeploySoldiers()
        {
            UpdateEnemySoldiers();
            CheckStressLevel();

            int averageEnemyTowerPosition = PlayerLane.WIDTH / 2; //default
            if (EnemyTowerPositons != null)
                averageEnemyTowerPosition = (int)Math.Round(StaticStrategyUtilities.GetAverageUnitLocation(attackLane, EnemyTowerPositons, EnemyTowerPositons.Count - 1).xPos);

            Random random = new Random();
            while (activeStrategy.soldiersToBuy > 0) 
            {
                //choose random side that is not average position of enemy towers
                int x = random.Next(PlayerLane.WIDTH);
                if (x == averageEnemyTowerPosition)
                    x = x + SOLDIER_RANGE * Math.Sign(averageEnemyTowerPosition - ((PlayerLane.WIDTH - 1) / 2));

                x = StaticStrategyUtilities.SmoothWidthPosition(x);

                if (attackLane.GetCellAt(x, 0).Unit == null)
                    player.BuySoldier(attackLane, x);

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

        List<Vector2Di> GetEnemySoldierPositions() 
        {
            return StaticStrategyUtilities.GetUnitPositions(EnemySoldiers);
        }

        Vector2Di GetBottomEnemySoldierPosition() 
        {
            if (EnemySoldierPositions != null && EnemySoldierPositions.Count > 0)
                return EnemySoldierPositions[EnemySoldierPositions.Count - 1];
            return new Vector2Di(0, 0);
        }

        List<Vector2Di> GetEnemyTowerPositions()
        {
            return StaticStrategyUtilities.GetUnitPositions(EnemyTowers);
        }

        public static bool IsCriticallyClose(float yPos) 
        {
            return yPos > PlayerLane.HEIGHT * 0.65f;
        }

        public static bool IsCriticallyClose(int yPos)
        {
            return yPos > PlayerLane.HEIGHT * 0.65f;
        }
    }
}
