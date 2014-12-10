using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Color = System.Drawing.Color;

namespace Bot4UPlayer
{
    public class Dominion
    {
        private enum ActionType
        {
            Back,
            Fight,
            GetTower
        };

        // declare the indices of spells in the spellbook
        private const int Q_SPELLBOOK_INDEX = 0;
        private const int W_SPELLBOOK_INDEX = 1;
        private const int E_SPELLBOOK_INDEX = 2;
        private const int R_SPELLBOOK_INDEX = 3;

        // declare ratios to define health states
        private const float LOW_HEALTH_RATIO = 0.2f;
        private const float MAX_HEALTH_RATIO = 0.8f;

        // declare radius of close enemies
        private const float ENEMY_CLOSE_RADIUS = 800.0f;

        // decleare the name to find dominion towers
        private const string DOMINION_TOWER_NAME = "OdinNeutralGuardian";

        // declare spawn positions for either team
        private Vector3 ORDER_SPAWN_POS = new Vector3(567.8055f, 4136.705f, -35.15588f);
        private Vector3 CHAOS_SPAWN_POS = new Vector3(13314.1f, 4141.6f, -37.2f);

        // declare shorthandle to access the player object
        // Properties http://msdn.microsoft.com/en-us/library/aa288470%28v=vs.71%29.aspx 
        private Obj_AI_Hero Player { get { return ObjectManager.Player; } }

        // declare handle to access dominion towers
        private List<Obj_AI_Minion> DominionTowers
        {
            get
            {
                return _DominionTowers;
            }
            set
            {
                if (_DominionTowers != value)
                {
                    _DominionTowers = value;
                }
            }
        }

        // stores access to dominion towers for easy access
        private List<Obj_AI_Minion> _DominionTowers;

        // returns nearest tower that is enemy controlled or uncaptured
        private Obj_AI_Minion NearestCapturableTower
        {
            get
            {
                if (DominionTowers != null)
                {
                    return
                        DominionTowers
                            .Where(tower => tower.Team != Player.Team)
                            .OrderBy(tower => Player.Distance(tower)).FirstOrDefault();
                }

                return null;
            }
        }

        // returns the count of towers which are enemy controlled
        // or uncaptured
        private int CaptureableTowerCount
        {
            get
            {
                if (DominionTowers != null)
                {
                    return
                        DominionTowers
                            .Count(tower => tower.Team != Player.Team);
                }

                return 0;
            }
        }

        // Helper for choosing what target to attack
        private TargetSelector _TS;

        // declare list of spells
        private Spell Q, W, E, R;
        private DamageSpell _DamageQ, _DamageW, _DamageE, _DamageR;
        private bool _QIsDamageSpell, _WIsDamageSpell, _EIsDamageSpell, _RIsDamageSpell;

        // Last buy time to help buying logic
        private int _NextBuyTick;
        
        // Last capture time to help capture logic
        private int _NextCaptureTick;

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
        private Vector3 _SpawnPos = new Vector3(0.0f, 0.0f, 0.0f);

        // Stores what level should be upgraded at each level up
        private int[] _LevelSequence; 

        // Tracks what item should be bought next
        private int _NextBuyIndex = 0;
        // Stores the order of items that should be bought
        // with their item id
        private int[] _ShopList;

        // State the bot is currently in
        private ActionType _CurrentAction;

        public Dominion()
        {
            _TS = new TargetSelector(Player.CastRange, TargetSelector.TargetingMode.Closest);
            _LevelSequence = new int[] { 0, 1, 2, 0, 0, 3, 1, 0, 0, 1, 3, 1, 2, 1, 2, 3, 2, 2 };
            _ShopList = new int[] { 3006, 1036, 1053, 1028, 1036, 1036, 3134, 3071, 3086, 3044, 3057, 3078, 1038, 3072, 1038, 3072 };

            CustomEvents.Game.OnGameLoad += Game_OnGameLoad;
        }

        public static bool IsDominion()
        {
            return Game.Type == GameType.Dominion;
        }

        /// <summary>
        /// Game Loaded Method
        /// </summary>
        private void Game_OnGameLoad(EventArgs args)
        {
            if (Game.Type != GameType.Dominion) // check if the current gametype is Dominion
                return; // stop program
            
            _DamageQ = Damage.Spells[Player.ChampionName]
                                .Where(dmgSpell => dmgSpell.Slot == SpellSlot.Q)
                                .FirstOrDefault();

            _DamageW = Damage.Spells[Player.ChampionName]
                                .Where(dmgSpell => dmgSpell.Slot == SpellSlot.W)
                                .FirstOrDefault();

            _DamageE = Damage.Spells[Player.ChampionName]
                        .Where(dmgSpell => dmgSpell.Slot == SpellSlot.E)
                        .FirstOrDefault();

            _DamageR = Damage.Spells[Player.ChampionName]
                        .Where(dmgSpell => dmgSpell.Slot == SpellSlot.R)
                        .FirstOrDefault();

            // Dominion starts at lvl 3 so make sure player uses
            // available points on load
            LevelUp(Player.Level, Player.SpellTrainingPoints);

            // init towers
            DominionTowers = ObjectManager.Get<Obj_AI_Minion>().Where(minion => minion.Name == DOMINION_TOWER_NAME).ToList();

            // create Q spell with Q's range
            Q = new Spell(SpellSlot.Q, (int)Player.Spellbook.Spells[Q_SPELLBOOK_INDEX].SData.CastRange[0]);
            // create W spell with W's range
            W = new Spell(SpellSlot.W, Player.Spellbook.Spells[W_SPELLBOOK_INDEX].SData.CastRange[0]);
            // create E spell with E's range
            E = new Spell(SpellSlot.E, Player.Spellbook.Spells[E_SPELLBOOK_INDEX].SData.CastRange[0]);
            // create R spell with R's range
            R = new Spell(SpellSlot.R, Player.Spellbook.Spells[R_SPELLBOOK_INDEX].SData.CastRange[0]);

            // subscribe to level up event
            CustomEvents.Unit.OnLevelUp += Unit_OnLevelUp;

            // subscribe to Update event gets called every game update around 10ms
            Game.OnGameUpdate += Game_OnGameUpdate;

            // print text in local chat to indicate loaded successfully
            Game.PrintChat("Bot4UDominion Loaded");
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

        private void CaptureNearestTower()
        {
            Packet.C2S.InteractObject.Encoded(
                new Packet.C2S.InteractObject.Struct(Player.NetworkId, NearestCapturableTower.NetworkId))
                .Send(PacketChannel.C2S, PacketProtocolFlags.Reliable);
        }

        private bool IsDying()
        {
            // Return whether player current health is lower than acceptable ratio of max health
            return Player.Health < (Player.MaxHealth * LOW_HEALTH_RATIO);
        }

        private void IsNobodyAround()
        {
            // If player is not dying
            // and the closest enemy if fairly far away
            // then go get a tower
            if (!IsDying() &&
                    (NearestEnemy() == null ||
                    Player.Distance(NearestEnemy()) > 900.0f))
            {
                _CurrentAction = ActionType.GetTower;
            }
        }

        private bool IsEnemyClose()
        {
            // Check if the closest enemy is within the acceptable radius
            return (NearestEnemy() != null && Player.Distance(NearestEnemy()) <= ENEMY_CLOSE_RADIUS);
        }

        private int CloseEnemyCount()
        {
            return ObjectManager.Get<Obj_AI_Hero>()
                .Count(enemy => enemy.Team != Player.Team &&
                                !enemy.IsDead &&
                                Player.Distance(enemy) <= ENEMY_CLOSE_RADIUS);
        }

        private int CloseAllyCount()
        {
            return ObjectManager.Get<Obj_AI_Hero>()
                .Count(ally => ally.Team == Player.Team &&
                                ally.NetworkId != Player.NetworkId &&
                                Player.Distance(ally) <= ENEMY_CLOSE_RADIUS);
        }

        /// <summary>
        /// Main Update Method
        /// </summary>
        private void Game_OnGameUpdate(EventArgs args)
        {
            // dont do stuff while dead
            if (Player.IsDead)
                return;

            // Check to see if player can buy items
            BuyItems();

            if (_CurrentAction != ActionType.Back)
            {
                // Check for nearby allies/enemies
                IsNobodyAround();

                // If player is dying probably want to head home
                if (IsDying())
                {
                    _CurrentAction = ActionType.Back;
                }

                // If an enemy is close and we aren't dying try to fight
                if (IsEnemyClose() && !IsDying())
                {
                    _CurrentAction = ActionType.Fight;
                }

                // If a lot of enemies nearby, but few allies run away
                if (CloseEnemyCount() >= 2 && CloseAllyCount() <= 2)
                {
                    _CurrentAction = ActionType.Back;
                }

                // If we think we should capture tower, try to capture it now
                if (_CurrentAction == ActionType.GetTower)
                {
                    if (Environment.TickCount > _NextCaptureTick)
                    {
                        CaptureTower();
                        _NextCaptureTick = Environment.TickCount + 1000;
                    }
                }
                else if (_CurrentAction == ActionType.Fight) // Otherwise fight
                {
                    FightEnemy();
                }
            }
            else if (_CurrentAction == ActionType.Back) 
            {
                Player.IssueOrder(GameObjectOrder.MoveTo, SpawnPos);

                // If player is already healthy, head back to try and capture a tower
                if (Player.Health > (Player.MaxHealth * MAX_HEALTH_RATIO) &&
                    !(CloseEnemyCount() >= 2 && CloseAllyCount() <= 2))
                {
                    _CurrentAction = ActionType.GetTower;
                }
            }
        }

        private void CaptureTower()
        {
            // If there are no towers to capture
            if (CaptureableTowerCount <= 0)
            {
                // Choose a random ally to follow
                Obj_AI_Base randomAlly = ObjectManager.Get<Obj_AI_Hero>()
                                .Where(ally => ally.Team == Player.Team &&
                                                ally.NetworkId != Player.NetworkId).FirstOrDefault();
                // Check that there is someone to follow
                if (randomAlly != null)
                {
                    Player.IssueOrder(GameObjectOrder.MoveTo, randomAlly.Position);
                }
            }
            else
            {
                // If the player is sufficiently far away from the tower, move towards it
                if (Player.Distance(NearestCapturableTower) > 500)
                {
                    Player.IssueOrder(GameObjectOrder.MoveTo, NearestCapturableTower);
                }
                else
                {
                    // Send command to capture tower
                    CaptureNearestTower();
                }
            }
        }

        private Obj_AI_Hero NearestEnemy()
        {
            // Of the enemies that aren't dead, select the one with the 
            // minimum distance from our player
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

        private void FightEnemy()
        {
            if (_DamageQ != null && Q.IsReady())
            {
                Obj_AI_Hero target = SimpleTs.GetTarget(
                                        Q.Range,
                                        (SimpleTs.DamageType)_DamageQ.DamageType);

                if (target != null && !target.IsDead)
                {
                    Q.Cast(target, false, true);
                }
            }

            if (_DamageW != null && W.IsReady())
            {
                Obj_AI_Hero target = SimpleTs.GetTarget(
                                        W.Range,
                                        (SimpleTs.DamageType)_DamageW.DamageType);

                if (target != null && !target.IsDead)
                {
                    W.Cast(target, false, true);
                }
            }

            if (_DamageE != null && E.IsReady())
            {
                Obj_AI_Hero target = SimpleTs.GetTarget(
                            E.Range,
                            (SimpleTs.DamageType)_DamageE.DamageType);

                if (target != null && !target.IsDead)
                {
                    E.Cast(target, false, true);
                }
            }

            if (_DamageR != null && R.IsReady())
            {
                Obj_AI_Hero target = SimpleTs.GetTarget(
                            R.Range,
                            (SimpleTs.DamageType)_DamageR.DamageType);

                if (target != null && !target.IsDead
                    && Player.GetSpellDamage(target, SpellSlot.R, 1) > target.Health)
                {
                    R.Cast(target, false, true);
                }
            }
        }

    }
}
