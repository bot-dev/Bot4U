﻿using LeagueSharp;
using LeagueSharp.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bot4UPlayer
{
    class Program
    {
        static void Main(string[] args)
        {
            if(Dominion.IsDominion())
            {
                new Normal();
                // new Dominion();
            }
            else if(Normal.IsNormal())
            {
                new Normal();
            }
        }
    }
}
