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
        public int towersBuyTargetNr, soldiersBuyTargetNr, towersBought, soldiersBought;
        public EDefenseStrategy defenseStrategy;
        public EStressLevel stressLevel;

        public void ClearBuyingData() 
        {
            towersBuyTargetNr = 0;
            soldiersBuyTargetNr = 0;
            towersBought = 0;
            soldiersBought = 0;
        }
    };

    public class MGStrategy : AbstractStrategy
    {
        public static readonly int SOLDIER_COST = 2, SOLDIER_RANGE = 2, TOWER_SPAWN_DISTANCE = 2;
        StrategySet activeStrategy;
        List<Unit> enemySoldiers, enemyTowers;
        List<Vector2Di> enemySoldierPositions, enemyTowerPositions;
        CentralSoldierData centralSoldierData;
        public MGStrategy(PlayerLane defendLane, PlayerLane attackLane, Player player) : base(defendLane, attackLane, player)
        {
            activeStrategy.stressLevel = 0f;
        }

        void UpdateStressLevel()
        {
            //STRESS LEVEL: PERFORMANCE OF ENEMY SOLDIERS + NUMBER OF DEFENSE TOWERS COMPARED TO ENEMY
            int ownTowerCount = defendLane.TowerCount();
            if (enemyTowers != null)
            {
                foreach (Tower tower in enemyTowers)
                {
                    if (tower.Health < 3)
                        ownTowerCount--;
                }
            }

            float enemySoldierCount = defendLane.SoldierCount();
            if (enemySoldiers != null)
            {
                foreach (Soldier soldier in enemySoldiers)
                {
                    if (IsCriticallyClose(soldier.PosY))
                        enemySoldierCount++;
                }
            }

            //CALCULATING STRESS LEVEL
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
            //MOVE DEFENSE LINES TO THE BACK WITH HIGHER NUMBER OF ENEMIES
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
            activeStrategy.ClearBuyingData();
            int goldWallet = player.Gold;
            if (goldWallet > Tower.GetNextTowerCosts(defendLane) || goldWallet > SOLDIER_COST) 
            {
                //MORE DEFENSE WITH HIGH STRESS LEVEL -> PRIORITIZE TOWERS, THEN BUY SOLDIERS WITH REST
                if (activeStrategy.stressLevel == EStressLevel.high) 
                {
                    //SIMULATE NEXT TOWER COSTS
                    while (goldWallet > Tower.GetNextTowerCosts(defendLane) + activeStrategy.towersBuyTargetNr * defendLane.TowerCount()) 
                    {
                        goldWallet -= Tower.GetNextTowerCosts(defendLane) + activeStrategy.towersBuyTargetNr * defendLane.TowerCount();
                        activeStrategy.towersBuyTargetNr++;
                    }

                    Random random = new Random();
                    for (int randomSoldierNumber = goldWallet/SOLDIER_COST; randomSoldierNumber > 0; randomSoldierNumber--) 
                    {
                        goldWallet -= SOLDIER_COST;
                        activeStrategy.soldiersBuyTargetNr++;
                    }
                }

                else 
                { //BALANCING SOLDIERS IN ACCORDANCE TO MONEY
                    for (int i = goldWallet / SOLDIER_COST; i > 0; i--) 
                    {
                        if (goldWallet > Tower.GetNextTowerCosts(defendLane) + activeStrategy.towersBuyTargetNr * defendLane.TowerCount())
                        {
                            goldWallet -= Tower.GetNextTowerCosts(defendLane) + activeStrategy.towersBuyTargetNr * defendLane.TowerCount();
                            activeStrategy.towersBuyTargetNr++;
                        }
                        if (goldWallet > SOLDIER_COST)
                        {
                            goldWallet -= SOLDIER_COST;
                            activeStrategy.soldiersBuyTargetNr++;
                        } 
                    }
                }
            }
        }

        public override void DeployTowers()
        {
            UpdateEnemyTowers();
            UpdateStressLevel();
            EvaluateBuyingBehaviour();
            for (int i = 0; i < activeStrategy.towersBuyTargetNr; i++)
            {
                if (player.Gold >= Tower.GetNextTowerCosts(defendLane))
                {
                    activeStrategy.defenseStrategy = GetDefenseStrategy();

                    //FIRST BUY ATTEMPT BY FINDING OPTIMAL POSITION
                    int spawnYLine = GetTowerDeploymentYPos();
                    int spawnXLine = GetTowerDeploymentXPos();
                    Tower tower = player.BuyTower(defendLane, spawnXLine, spawnYLine);
                    if (tower != null)
                        activeStrategy.towersBought++;
                    else
                    {
                        //IF FIRST ATTEMPT NOT SUCCESSFUL; TRY TO BUILD UP (MID OR HIGH STRESS) OR DOWN (LOW STRESS) THE LANE
                        Random random = new Random();
                        int yAttempts = random.Next(1, 3);
                        bool positionFound = false;
                        int signDirection = activeStrategy.defenseStrategy == EDefenseStrategy.defendFront ? 1 : -1;

                        for (int attempt = 1; attempt <= yAttempts && !positionFound; attempt++)
                        {
                            //INTENDED POSITION ALREADY OCCUPIED
                            Vector2Di altPos = GetFreeTowerXPositionInLine(spawnXLine, spawnYLine + attempt * signDirection * TOWER_SPAWN_DISTANCE);
                            tower = player.BuyTower(defendLane, altPos.xPos, altPos.yPos);
                            if (tower != null)
                            {
                                positionFound = true;
                                activeStrategy.towersBought++;
                            }
                        }
                    }
                }
            }
        }

        int GetTowerDeploymentYPos() 
        {
            int yPos = 0;
            Random yRandom = new Random();

            //IF BOTTOM SOLDIER TOO CLOSE TO FINISH LINE, TRY TO BUILD TWER AHEAD OF HIM
            if (AreSoldiersCriticallyClose())
                yPos = GetBottomEnemySoldierPosition().yPos;
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
            int xPos = PlayerLane.WIDTH/2; //DEFAULT: MID
            //IF BOTTOM SOLDIER TOO CLOSE TO FINISH LINE; GET SAME X POSITION
            if (AreSoldiersCriticallyClose())
                xPos = GetBottomEnemySoldierPosition().xPos;
            else if (enemySoldierPositions != null) //ELSE: GET AVERAGE X POSITION OF ALL ENEMIES
                xPos = (int)Math.Round(StaticStrategyUtilities.GetAverageUnitLocation(defendLane, enemySoldierPositions, 1.5f).xPos);

            return StaticStrategyUtilities.SmoothTowerXPosition(xPos);
        }

        Vector2Di GetFreeTowerXPositionInLine(int originXLine, int spawnYLine)
        {
            if (defendLane.GetCellAt(originXLine, spawnYLine) != null)
                if (defendLane.GetCellAt(originXLine, spawnYLine).Unit == null)
                    return new Vector2Di(originXLine, spawnYLine);

            //GET ESTABLISHED TOWERS ON LANE (DUE TO INACCESSIBLE LIST 'TOWER' IN PLAYERLANE.CS)
            List<Vector2Di> towersOnYLine = new List<Vector2Di>();
            List<Unit> deployedTowers = StaticStrategyUtilities.GetUnitsOfType("T", defendLane);
            if (deployedTowers.Count == 0)
                return new Vector2Di(originXLine, spawnYLine);

            foreach (Vector2Di vector2D in StaticStrategyUtilities.GetUnitPositions(deployedTowers))
            {
                if (vector2D.yPos == spawnYLine)
                    towersOnYLine.Add(vector2D);
            }

            //SEARCH DIRECTION OF NEW POSITION (TOWARDS ENEMY CENTER)
            Vector2Di freeCell = new Vector2Di();
            int enemyWeightXPos = enemySoldierPositions != null ? (int)Math.Round(StaticStrategyUtilities.GetAverageUnitLocation(defendLane, enemySoldierPositions, 1.5f).xPos) : PlayerLane.WIDTH / 2;
            int signDirection = Math.Sign(enemyWeightXPos - originXLine);

            //IF STILL SPACE ON Y LINE, SEARCH FROM CENTER TO BORDER FROM INTENDED X POSITION
            if (towersOnYLine.Count < PlayerLane.WIDTH / TOWER_SPAWN_DISTANCE)
            {
                bool positionFound = false;
                for (int attempts = 1; attempts <= PlayerLane.WIDTH / 2 && !positionFound; attempts++)
                {
                    //SEARCH IN DIRECTION OF ENEMY CENTER
                    int attemptX = StaticStrategyUtilities.SmoothWidthPosition(originXLine + signDirection * TOWER_SPAWN_DISTANCE * attempts);
                    if (defendLane.GetCellAt(attemptX, spawnYLine) == null)
                    {
                        freeCell = new Vector2Di(attemptX, spawnYLine);
                        positionFound = true; break;
                    }
                    //SEARCH IN OPPOSITE DIRECTION OF ENEMY CENTER
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
            UpdateStressLevel();
            if (centralSoldierData == null)
                centralSoldierData = new CentralSoldierData(attackLane);

            int averageEnemyTowerPosition = PlayerLane.WIDTH / 2; //default
            if (enemyTowerPositions != null)
                averageEnemyTowerPosition = (int)Math.Round(StaticStrategyUtilities.GetAverageUnitLocation(attackLane, enemyTowerPositions).xPos);

            Random random = new Random();
            for (int i = 0; i < activeStrategy.soldiersBuyTargetNr; i++) 
            {
                //CHOOSE SIDE AWAY FROM ENEMY AVG POS
                int x = random.Next(PlayerLane.WIDTH);
                if (x == averageEnemyTowerPosition)
                    x = x + SOLDIER_RANGE * Math.Sign(averageEnemyTowerPosition - ((PlayerLane.WIDTH - 1) / 2));

                x = StaticStrategyUtilities.SmoothWidthPosition(x);

                if (attackLane.GetCellAt(x, 0).Unit == null)
                {
                    player.BuySoldier(attackLane, x);
                    activeStrategy.soldiersBought++;
                }
            }
            centralSoldierData.UpdateData(enemyTowerPositions, GetAttackSoldiers());

            ReevaluateBuyingBehaviour();
        }

        List<MGSoldier> GetAttackSoldiers()
        {
            List<Unit> tempSoldiers = StaticStrategyUtilities.GetUnitsOfType("S", attackLane);
            List<MGSoldier> attackSoldiers = new List<MGSoldier>();
            //cast Unit to Soldier
            foreach (MGSoldier unitSoldier in tempSoldiers)
            {
                attackSoldiers.Add(unitSoldier);
            }
            return attackSoldiers;
        }

        void ReevaluateBuyingBehaviour() 
        {
            if (activeStrategy.towersBuyTargetNr > activeStrategy.towersBought && player.Gold > Tower.GetNextTowerCosts(defendLane)) {
                Random xRandom = new Random();
                int randomXPos = StaticStrategyUtilities.SmoothTowerXPosition(xRandom.Next(0, PlayerLane.WIDTH - 1));
                player.BuyTower(defendLane, randomXPos, PlayerLane.HEIGHT-1);
            }

            if (activeStrategy.soldiersBuyTargetNr > activeStrategy.soldiersBought && player.Gold > SOLDIER_COST) 
            {
                Random xRandom = new Random();
                player.BuySoldier(attackLane, xRandom.Next(0, PlayerLane.WIDTH - 1));
            }
        }

        public override List<Soldier> SortedSoldierArray(List<Soldier> unsortedList)
        {
            return unsortedList;
        }

        void UpdateEnemySoldiers()
        {
            enemySoldiers = StaticStrategyUtilities.GetUnitsOfType("S", defendLane);
            enemySoldierPositions = GetEnemySoldierPositions();
        }

        void UpdateEnemyTowers()
        {
            enemyTowers = StaticStrategyUtilities.GetUnitsOfType("T", attackLane);
            enemyTowerPositions = GetEnemyTowerPositions();
        }

        List<Vector2Di> GetEnemySoldierPositions() 
        {
            return StaticStrategyUtilities.GetUnitPositions(enemySoldiers);
        }

        Vector2Di GetBottomEnemySoldierPosition() 
        {
            if (enemySoldierPositions == null)
                return new Vector2Di(PlayerLane.WIDTH / 2, PlayerLane.HEIGHT / 2); //DEFAULT: MID
            if (enemySoldierPositions.Count > 0)
            {
                for (int y = PlayerLane.HEIGHT - 1; y >= 0; y--){
                    for (int x = 0; x < PlayerLane.WIDTH; x++) {
                        Cell cell = defendLane.GetCellAt(x, y);
                        if (cell != null) {
                            if (cell.Unit != null)  {
                                if (cell.Unit.Type == "S")
                                    return new Vector2Di(cell.Unit.PosX, cell.Unit.PosY);
                            }
                        }
                    }
                }
            }
            return new Vector2Di(PlayerLane.WIDTH / 2, PlayerLane.HEIGHT / 2);
        }

        bool AreSoldiersCriticallyClose() 
        {
            return IsCriticallyClose(GetBottomEnemySoldierPosition().yPos);
        }

        List<Vector2Di> GetEnemyTowerPositions()
        {
            return StaticStrategyUtilities.GetUnitPositions(enemyTowers);
        }

        public static bool IsCriticallyClose(float yPos) 
        {
            return yPos > PlayerLane.HEIGHT * 0.65f;
        }
    }
}
