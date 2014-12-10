using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bot4UPlayer
{
    public class Normal
    {
        private enum ActionType
        {
            Back,
            Fight,
            Follow
        };

        // declare ratios to define health states
        private const float LOW_HEALTH_RATIO = 0.2f;
        private const float MAX_HEALTH_RATIO = 0.8f;

        // declare spawn positions for either team
        private Vector3 ORDER_SPAWN_POS = new Vector3(437.0f, 471.0f, 182.0f);
        private Vector3 CHAOS_SPAWN_POS = new Vector3(14296.0f, 14384.0f, -172.0f);

        // declare radius of close enemies
        private const float ENEMY_CLOSE_RADIUS = 800.0f;

        // Holds followinfo for each of the allies to track if they are afk
        private Dictionary<string, FollowInfo> _AfkTracker;

        // declare shorthandle to access the player object
        // Properties http://msdn.microsoft.com/en-us/library/aa288470%28v=vs.71%29.aspx 
        private Obj_AI_Hero Player { get { return ObjectManager.Player; } }

        // Random provider
        private static Random Randy = new Random();

        // declare handle to access allies
        private List<Obj_AI_Hero> Allies
        {
            get
            {
                return ObjectManager
                    .Get<Obj_AI_Hero>()
                    .Where(hero => hero.Team == Player.Team && hero.NetworkId != Player.NetworkId).ToList();
            }
        }

        // if afktracker is null init it for all allies
        private Dictionary<string, FollowInfo> AfkTracker
        {
            get
            {
                if (_AfkTracker == null)
                {
                    _AfkTracker = new Dictionary<string, FollowInfo>();
                    foreach (Obj_AI_Hero ally in Allies)
                    {
                        _AfkTracker.Add(ally.ChampionName, new FollowInfo(ally.Position, Environment.TickCount));
                    }
                }

                return _AfkTracker;
            }
        }

        // Returns the closest distance ally to the player that is not dead
        private Obj_AI_Hero NearestFollowableAlly
        {
            get
            {
                Obj_AI_Hero closest = null;
                float minDist = float.MaxValue;

                foreach (Obj_AI_Hero ally in Allies.Where(ally => !ally.IsDead))
                {
                    if (Player.Distance(ally) < minDist && !AfkTracker[ally.ChampionName].IsAfk())
                    {
                        closest = ally;
                        minDist = Player.Distance(ally);
                    }
                }

                return closest;
            }
        }

        // Returns the count of allies that are not dead
        private int FollowableAlliesCount
        {
            get
            {
                return Allies.Count(ally => !ally.IsDead);
            }
        }

        // Last buy time to help buying logic
        private int _NextBuyTick;

        // Last capture time to help capture logic
        private int _NextFollowTick;

        // Stores the spawn of the player based off the team
        // of the player
        private Vector3 SpawnPos
        {
            get
            {
                // check to see if position has been initialized
                if (_SpawnPos.X == 0.0f && _SpawnPos.Y == 0.0f && _SpawnPos.Z == 0.0f)
                {
                    // set spawn pos according to player's team
                    if (Player.Team == GameObjectTeam.Order)
                    {
                        _SpawnPos = ORDER_SPAWN_POS;
                    }
                    else
                    {
                        _SpawnPos = CHAOS_SPAWN_POS;
                    }
                }

                return _SpawnPos;
            }
        }

        // declare initial spawn position
        private Vector3 _SpawnPos;

        private int[] _LevelSequence;


        private int _NextBuyIndex = 0;
        private int[] _ShopList;
        private ActionType _CurrentAction;
       
        public Normal()
        {
            _SpawnPos = new Vector3(0.0f, 0.0f, 0.0f);
            _LevelSequence = new int[] { 0, 1, 2, 0, 0, 3, 1, 0, 0, 1, 3, 1, 2, 1, 2, 3, 2, 2 };
            _ShopList = new int[] { 3006, 1036, 1053, 1028, 1036, 1036, 3134, 3071, 3086, 3044, 3057, 3078, 1038, 3072, 1038, 3072 };

            CustomEvents.Game.OnGameLoad += Game_OnGameLoad;
        }

        public static bool IsNormal()
        {
            return Game.Type == GameType.Normal;
        }

        /// <summary>
        /// Game Loaded Method
        /// </summary>
        private void Game_OnGameLoad(EventArgs args)
        {
            if (Game.Type != GameType.Normal) // check if the current gametype is Dominion
                return; // stop program

            LevelUp(Player.Level, Player.SpellTrainingPoints);

            // subscribe to level up event
            CustomEvents.Unit.OnLevelUp += Unit_OnLevelUp;

            // subscribe to Update event gets called every game update around 10ms
            Game.OnGameUpdate += Game_OnGameUpdate;

            // print text in local chat to indicate loaded successfully
            Game.PrintChat("Bot4UNormal Loaded");
        }

        private void Unit_OnLevelUp(Obj_AI_Base sender, CustomEvents.Unit.OnLevelUpEventArgs args)
        {
            LevelUp(args.NewLevel, args.RemainingPoints);
        }

        private void LevelUp(int currentLevel, int remainingPoints)
        {
            // start from as far back as the remaining points will let you and walk forward
            // leveling up each spell on the way until out of points
            for (int i = remainingPoints; i > 0; i--)
            {
                Player.Spellbook.LevelUpSpell((SpellSlot)_LevelSequence[currentLevel - i]);
            }
        }

        private bool IsDying()
        {
            // Return whether player current health is lower than acceptable ratio of max health
            return Player.Health < (Player.MaxHealth * LOW_HEALTH_RATIO);
        }

        private void IsNobodyAround()
        {
            // If Player isn't dying and 
            // nearest enemy is sufficiently far away and
            // we aren't trying to go back
            // then try to follow
            if (!IsDying() &&
                    (NearestEnemy() == null ||
                    Player.Distance(NearestEnemy()) > 900.0f) &&
                _CurrentAction != ActionType.Back)
            {
                _CurrentAction = ActionType.Follow;
            }
        }

        private bool IsEnemyClose()
        {
            // Checks if nearesst enemy is sufficiently close
            return (NearestEnemy() != null && Player.Distance(NearestEnemy()) <= ENEMY_CLOSE_RADIUS);
        }

        private int CloseEnemyCount()
        {
            // Returns count of non dead enemies within sufficently close radius
            return ObjectManager.Get<Obj_AI_Hero>()
                .Count(enemy => enemy.Team != Player.Team &&
                                !enemy.IsDead &&
                                Player.Distance(enemy) <= ENEMY_CLOSE_RADIUS);
        }

        private int CloseAllyCount()
        {
            // Returns count of non dead allies within range.
            return Allies
                .Count(ally => !ally.IsDead && Player.Distance(ally) <= ENEMY_CLOSE_RADIUS);
        }

        /// <summary>
        /// Main Update Method
        /// </summary>
        private void Game_OnGameUpdate(EventArgs args)
        {
            // dont do stuff while dead
            if (Player.IsDead)
                return;

            foreach (Obj_AI_Hero ally in Allies)
            {
                AfkTracker[ally.ChampionName].Update(ally.Position);
            }

            BuyItems();

            if (_CurrentAction != ActionType.Back)
            {
                IsNobodyAround();

                // If player is dying go back
                if (IsDying())
                {
                    _CurrentAction = ActionType.Back;
                }

                // If ordered to follow try and follow if we haven't
                // recently
                if (_CurrentAction == ActionType.Follow)
                {
                    if (Environment.TickCount > _NextFollowTick)
                    {
                        FollowAlly();
                        // Don't follow for a second
                        _NextFollowTick = Environment.TickCount + 500;
                    }
                }
            }
            else if (_CurrentAction == ActionType.Back)
            {
                Player.IssueOrder(GameObjectOrder.MoveTo, SpawnPos);

                // If player is in fountain and very healthy or
                // player is sort of healthier than low ratio
                // then keep trying to follow
                if (Utility.InFountain() &&
                    Player.Health > (Player.MaxHealth * MAX_HEALTH_RATIO) ||
                    Player.Health > (Player.MaxHealth * (LOW_HEALTH_RATIO + 0.1f)))
                {
                    _CurrentAction = ActionType.Follow;
                }
            }
        }

        private void FollowAlly()
        {
            // If no one to follow continue to move around 
            // randomly
            if (FollowableAlliesCount <= 0)
            {

                Player.IssueOrder(
                    GameObjectOrder.MoveTo,
                    Player.Position + new Vector3(Randy.NextFloat(-10.0f, 10.0f),
                                        Randy.NextFloat(-10.0f, 10.0f),
                                        Randy.NextFloat(-10.0f, 10.0f)));
            }
            else
            {
                // Randomly choose a location that is weighted towards being towards the spawn tower
                float followX = 
                    ((SpawnPos.X - NearestFollowableAlly.Position.X) / NearestFollowableAlly.Distance(SpawnPos)) *
                    ((400.0f - 300.0f) / 2.0f + 300.0f) +
                    NearestFollowableAlly.Position.X + Randy.NextFloat(-((-400.0f - 300) / 3), ((-400.0f - 300) / 3));
                float followY = 
                    ((SpawnPos.Y - NearestFollowableAlly.Position.Y) / NearestFollowableAlly.Distance(SpawnPos)) * 
                    ((400.0f - 300.0f) / 2.0f + 300.0f) + 
                    NearestFollowableAlly.Position.Y + Randy.NextFloat(-((-400.0f - 300) / 3), ((-400.0f - 300) / 3));
                // Z axis does nothing
                float followZ = 0.0f;

                Player.IssueOrder(GameObjectOrder.MoveTo, new Vector3(followX, followY, followZ));
            }
        }

        private Obj_AI_Hero NearestEnemy()
        {
            return ObjectManager.Get<Obj_AI_Hero>()
                    .Where(enemy => enemy.Team != Player.Team &&
                                    !enemy.IsDead)
                    .OrderBy(enemy => Player.Distance(enemy)).FirstOrDefault();
        }

        private void BuyItems()
        {
            // If we haven't bought recently try and buy next item
            if (Environment.TickCount >= _NextBuyTick)
            {
                // If we already have the item at our index, look
                // for next item
                if (Items.HasItem(_ShopList[_NextBuyIndex]))
                {
                    _NextBuyIndex++;
                }
                else
                {
                    Items.Item buyItem = new Items.Item(_ShopList[_NextBuyIndex], 100.0f);
                    buyItem.Buy();
                }

                // Don't try and buy for another second to ensure
                // next item gets placed in inventory before checking again
                _NextBuyTick = Environment.TickCount + 1000;
            }
        }
    }
}
