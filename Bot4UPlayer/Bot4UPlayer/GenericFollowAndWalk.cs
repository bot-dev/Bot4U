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
    public class GenericFollowAndWalk
    {
        private const int SCRIPT_START_TIME = 10;
        private Obj_AI_Hero Partner { get; set; }
        private Dictionary<string, FollowInfo> _AfkTracker;
        private Vector3 AllySpawn { get; set; }
        private Vector3 EnemySpawn { get; set; }
        private Vector3 BottomPoint { get; set; }
        private bool IsYikes = false;
        private Obj_AI_Turret YikesTurret { get; set; }
        private bool IsPartnerRecalling = false;
        private static Random Randy = new Random();

        public GenericFollowAndWalk()
        {
            GameObject.OnCreate += GameObject_OnCreate;
            GameObject.OnDelete += GameObject_OnDelete;

            CustomEvents.Game.OnGameLoad += Game_OnGameLoad;
        }

        void Game_OnGameLoad(EventArgs args)
        {
            Game.OnGameUpdate += Game_OnGameUpdate;
        }

        void Game_OnGameUpdate(EventArgs args)
        {
            RunBehaviorTree();
        }

        void GameObject_OnDelete(GameObject sender, EventArgs args)
        {
            if (sender.Name.Contains("yikes"))
            {
                IsYikes = false;
                YikesTurret = null;
            }
            else if (sender.Name.Contains("TeleportHome") && (Partner != null && sender.Position.Distance(Partner.Position) < 70.0f))
            {
                IsPartnerRecalling = false;
            }
        }

        void GameObject_OnCreate(GameObject sender, EventArgs args)
        {
            if(sender.Name.Contains("yikes"))
            {
                IsYikes = false;
                YikesTurret = null;
            }
            else if (sender.Name.Contains("TeleportHome") && (Partner != null && sender.Position.Distance(Partner.Position) < 70.0f))
            {
                IsPartnerRecalling = true;
            }
        }

        private Obj_AI_Hero Player
        {
            get
            {
                return ObjectManager.Player;
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

        // declare handle to access allies
        private List<Obj_AI_Hero> Enemies
        {
            get
            {
                return ObjectManager
                    .Get<Obj_AI_Hero>()
                    .Where(hero => hero.Team != Player.Team && hero.NetworkId != Player.NetworkId).ToList();
            }
        }

        private bool CheckStartTime()
        {
            return Game.ClockTime > SCRIPT_START_TIME;
        }

        private bool CheckNoPartner()
        {
            return (Partner == null);
        }

        private bool CheckPartnerAfk()
        {
            return Partner != null && 
                AfkTracker[Partner.ChampionName].IsAfk();
        }

        private bool CheckPartnerAlive()
        {
            return (Partner != null && !Partner.IsDead);
        }

        private bool CheckPartnerDead()
        {
            return (Partner != null && Partner.IsDead);
        }

        private bool CheckPartnerClose()
        {
            return (Partner != null &&
                Player.Distance(Partner) < 450.0f);
        }

        private bool CheckFriendClose()
        {
            List<Obj_AI_Hero> friends = GetPlayers(Player.Team, false, false);
            Obj_AI_Hero closest = null;

            foreach(Obj_AI_Hero hero in friends)
            {
                if(closest != null &&
                    Player.Distance(hero) < Player.Distance(closest))
                {
                    closest = hero;
                }
            }

            return Player.Distance(closest) < 450.0f;
        }

        private bool CheckInTurret()
        {
            Obj_AI_Turret myTurret = GetCloseTurret(Player, Player.Team);

            return (myTurret != null && Player.Distance(myTurret) < 450.0f &&
                Player.Distance(AllySpawn) < myTurret.Distance(AllySpawn));
        }

        public bool CheckMatchPartner()
        {
            if (Partner == null)
            {
                List<Obj_AI_Hero> myCarry = GetPlayers(Player.Team, false, false);
                Dictionary<Obj_AI_Hero, int> score = new Dictionary<Obj_AI_Hero, int>();
                
                int maxScore = int.MinValue;

                int count = 0;
                foreach(Obj_AI_Hero hero in myCarry)
                {
                    score[hero] = 0;
                    foreach(Obj_AI_Hero altHero in myCarry)
                    {
                        if(hero.Distance(BottomPoint) < altHero.Distance(BottomPoint))
                        {
                            score[hero]++;
                        }
                    }

                    if(hero.Distance(BottomPoint) < 6000.0f)
                    {
                        score[hero] += 5;
                    }

                    if(AllySpawn.Distance(BottomPoint) <= 5000.0f)
                    {
                        score[hero] -= 10000;
                    }
                    count++;
                }

                foreach(Obj_AI_Hero hero in myCarry)
                {
                    if(score[hero] > maxScore && score[hero] > 0)
                    {
                        maxScore = score[hero];
                        Partner = hero;
                    }
                }

                return Partner != null;
            }

            return false;
        }

        private bool CheckPartnerRecalling()
        {
            return IsPartnerRecalling;
        }

        private bool CheckIsRecalling()
        {
            foreach(GameObject gameObject in ObjectManager.Get<GameObject>())
            {
                if(gameObject != null && gameObject.IsValid && gameObject.Name.Contains("TeleportHome") && Player.Position.Distance(gameObject.Position) < 70.0f)
                {
                    return true;
                }
            }

            return false;
        }

        private bool CheckFollowPartner()
        {
            float followX = ((AllySpawn.X - Partner.Position.X) / Partner.Distance(AllySpawn)) * ((450.0f - 300.0f) / 2.0f + 300.0f) + Partner.Position.X + Randy.NextFloat(-((450.0f - 300.0f) / 3), ((450.0f - 300.0f) / 3));
            float followY = ((AllySpawn.Y - Partner.Position.Y) / Partner.Distance(AllySpawn)) * ((450.0f - 300.0f) / 2.0f + 300.0f) + Partner.Position.Y + Randy.NextFloat(-((450.0f - 300.0f) / 3), ((450.0f - 300.0f) / 3));

            Player.IssueOrder(GameObjectOrder.MoveTo, new Vector3(followX, followY, 0.0f));

            return true;
        }

        private bool CheckGoTurret()
        {
            Obj_AI_Turret myTurret = GetCloseTurret(Player, Player.Team);
            float followX = (AllySpawn.X - myTurret.Position.X) / (myTurret.Distance(AllySpawn)) * ((450.0f - 300.0f) / 2.0f + 300.0f) + myTurret.Position.X;
            float followY = (AllySpawn.Y + myTurret.Position.Y) / (myTurret.Distance(AllySpawn)) * ((450.0f - 300.0f) / 2.0f + 300.0f) + myTurret.Position.Y;

            Player.IssueOrder(GameObjectOrder.MoveTo, new Vector3(followX, followY, 0.0f));

            return true;
        }

        public bool CheckTowerFocusPlayer()
        {
            return IsYikes;
        }

        public bool CheckRunFromTower()
        {
            float followX = (2 * Player.Position.X) - YikesTurret.Position.X;
            float followY = (2 * Player.Position.Y) - YikesTurret.Position.Y;

            Player.IssueOrder(GameObjectOrder.MoveTo, new Vector3(followX, followY, 0.0f));

            return true;
        }

        public bool CheckRecall()
        {
            if(!Utility.InFountain())
                Player.Spellbook.CastSpell(SpellSlot.Recall);
            return true;
        }

        private void DetectSpawnPoints()
        {
            IEnumerable<GameObject> spawnPoints = ObjectManager.Get<GameObject>()
                .Where(spawnPoint => spawnPoint is Obj_SpawnPoint);

            AllySpawn = spawnPoints.Where(point => point.Position.X < 3000.0f && Player.Team == point.Team).FirstOrDefault().Position;

            EnemySpawn = spawnPoints.Where(point => point.Position.X >= 3000.0f && Player.Team != point.Team).FirstOrDefault().Position;
        }

        private List<Obj_AI_Turret> GetTowers()
        {
            return ObjectManager.Get<Obj_AI_Turret>().ToList();
        }

        public Obj_AI_Turret GetCloseTurret(Obj_AI_Hero hero, GameObjectTeam team)
        {
            List<Obj_AI_Turret> turrets = GetTowers();

            if(turrets.Count > 0)
            {
                Obj_AI_Turret closest = null;

                foreach(Obj_AI_Turret turret in turrets)
                {
                    if (closest != null && (Player.Distance(turret) < Player.Distance(closest)))
                    {
                        closest = turret;
                    }
                }

                return closest;
            }

            return null;
        }

        private List<Obj_AI_Hero> GetPlayers(GameObjectTeam team, bool includeDead, bool includeSelf)
        {
            List<Obj_AI_Hero> players;
            List<Obj_AI_Hero> result;

            if(team == Player.Team)
            {
                players = Allies;
            }
            else
            {
                players = Enemies;
            }

            result = players.Where(player => player.IsVisible && (!player.IsDead || (player.IsDead == includeDead))).ToList();
            
            if(includeSelf)
            {
                result.Add(Player);
            }

            return result;
        }

        // Sequence
        public bool RunBehaviorTree()
        {
            if (CheckStartTime())
                return true;

            if (Selector1())
                return true;

            return false;
        }

        public bool Selector1()
        {
            if (Sequence1())
                return true;

            if (Sequence7())
                return true;

            if (Sequence2())
                return true;

            if (Sequence3())
                return true;

            if (Sequence4())
                return true;

            return false;
        }

        public bool Sequence1()
        {
            if (!Selector5())
                return false;

            if (!CheckMatchPartner())
                return false;

            return true;
        }

        public bool Sequence7()
        {
            if (!CheckTowerFocusPlayer())
                return false;

            if (!CheckRunFromTower())
                return false;

            return true;
        }

        public bool Sequence2()
        {
            if (!CheckPartnerAlive())
                return false;

            if (!Selector2())
                return false;

            return true;
        }

        public bool Sequence3()
        {
            if (!CheckPartnerDead())
                return false;

            if (!Selector3())
                return false;

            return true;
        }

        public bool Sequence4()
        {
            if (!CheckInTurret())
                return false;

            if (!CheckRecall())
                return false;

            return true;
        }

        public bool Selector5()
        {
            if (CheckPartnerAfk())
                return true;

            if (CheckNoPartner())
                return true;

            return false;
        }

        public bool Selector2()
        {
            if (Sequence5())
                return true;

            if (CheckPartnerClose())
                return true;

            if (CheckFollowPartner())
                return true;

            return false;
        }

        public bool Selector3()
        {
            if (Selector3())
                return true;

            return false;
        }

        public bool Sequence5()
        {
            if (!CheckPartnerRecalling())
                return false;

            if (!CheckRecall())
                return false;

            return true;
        }

        public bool Selector4()
        {
            if (Sequence8())
                return true;

            if (CheckGoTurret())
                return true;

            return false;
        }

        public bool Sequence8()
        {
            if (!CheckInTurret())
                return false;

            if (!CheckRecall())
                return false;

            return true;
        }
    }
}
