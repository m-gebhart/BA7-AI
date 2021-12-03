﻿using GameFramework;
namespace AI_Strategy
{
    /*
     * This class derives from Soldier and provides a new move method. Your assignment should
     * do the same - but with your own movement strategy.
     */
    public class MySoldier : Soldier
    {
        public MySoldier(Player player, PlayerLane lane, int x) : base(player, lane, x)
        {
        }

        /*
         * This move method is a mere copy of the base movement method.
         */
        public override void Move()
        {
            if (speed > 0 && posY < PlayerLane.HEIGHT - 1)
            {
                int x = posX;
                int y = posY;
                for (int i = speed; i > 0; i--)
                {
                    if (MoveTo(x, y + i)) return;
                    if (MoveTo(x + i, y + i)) return;
                    if (MoveTo(x - i, y + i)) return;
                    if (MoveTo(x + i, y)) return;
                    if (MoveTo(x - i, y)) return;
                    if (MoveTo(x, y - i)) return;
                    if (MoveTo(x - i, y - i)) return;
                    if (MoveTo(x + i, y - i)) return;
                }
            }
        }
    }
}
