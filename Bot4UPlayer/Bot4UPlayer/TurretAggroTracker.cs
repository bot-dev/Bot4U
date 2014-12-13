using LeagueSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LeagueSharp.Common;
namespace Bot4UPlayer
{
    public class TurretAggroTracker
    {
        private Dictionary<int, int> AggroList { get; set; }

        public TurretAggroTracker()
        {
            AggroList = new Dictionary<int, int>();
            Game.OnGameProcessPacket += Game_OnGameProcessPacket;
        }

        void Game_OnGameProcessPacket(GamePacketEventArgs args)
        {
            Packet.S2C.TowerAggro.Struct aggroDate = Packet.S2C.TowerAggro.Decoded(args.PacketData);
        }



    }
}
