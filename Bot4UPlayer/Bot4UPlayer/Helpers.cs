using LeagueSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpDX;
using LeagueSharp.Common;

namespace Bot4UPlayer
{
    class Helpers
    {
        public static List<Obj_AI_Hero> AllyInRange(float range)
        {
            return ObjectManager
                .Get<Obj_AI_Hero>()
                    .Where(
                        h =>
                            ObjectManager.Player.Distance(h.Position) < range && h.IsAlly && !h.IsMe && h.IsValid &&
                            !h.IsDead)
                    .OrderBy(h => ObjectManager.Player.Distance(h.Position))
                    .ToList();
        }
    }
}
