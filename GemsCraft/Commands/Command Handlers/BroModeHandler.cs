﻿//Copyright (C) <2012>  <Jon Baker, Glenn Mariën and Lao Tszy>

//This program is free software: you can redistribute it and/or modify
//it under the terms of the GNU General Public License as published by
//the Free Software Foundation, either version 3 of the License, or
//(at your option) any later version.

//This program is distributed in the hope that it will be useful,
//but WITHOUT ANY WARRANTY; without even the implied warranty of
//MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//GNU General Public License for more details.

//You should have received a copy of the GNU General Public License
//along with this program.  If not, see <http://www.gnu.org/licenses/>.

//Copyright (C) <2012> Glenn Mariën (http://project-vanilla.com)

using System;
using System.Collections.Generic;
using GemsCraft.fSystem;
using GemsCraft.Players;
using GemsCraft.Utils;
// ReSharper disable StringLiteralTypo

namespace GemsCraft.Commands.Command_Handlers
{
    internal class BroMode
    {
        private static BroMode _instance;
        private static List<string> _broNames;
        private static Dictionary<int, Player> _registeredBroNames;
        private static int _namesRegistered;
        public static bool Active = false;

        private BroMode()
        {
            // Empty, singleton
            // single
            // single like glennmr?
        }

        public static BroMode GetInstance()
        {
            if (_instance != null) return _instance;
            _instance = new BroMode();
            _broNames = new List<string>()
            {
                "Brozo the Clown",
                "Rag and Brone",
                "Breau Brummel",
                "Brole Porter",
                "Flannery Bro'Connor",
                "Angelina Brolie",
                "Marco Brolo",
                "Placido Bromingo",
                "Brony Seikaly",
                "Vincent Van Brogh",
                "Brodhistiva",
                "Sandy Broufax",
                "Brosef Stalin",
                "Brojohsephat",
                "Lebrona Helmsley",
                "Tom Brolicchio",
                "Brohan Santana",
                "Brobi-Wan Kenobi",
                "Haley Broel Osment",
                "Brometheus",
                "Fidel Castbro",
                "Broul Castbro",
                "Leonid Brozhnev",
                "Brotello Putin Brodimir Brodimirovich <tm>",
                "Brangela Merkel",
                "Brovio Brobrusconi",
                "Brol Pot",
                "Elvis Costellbro",
                "Amy Broehler",
                "Stephen Brolbert",
                "Nabroleon Bronaparte",
                "Broliver Cromwell",
                "Evander Brolyfield",
                "Mario Brotali",
                "Brophia Loren",
                "David Brohansen",
                "Terrell Browens",
                "Tony Bromo",
                "Braubert",
                "Pete Brose",
                "Brony Soprano",
                "Jonathan Safran Broer",
                "Alex Brovechkin",
                "Bro Jackson",
                "Bropher Grace",
                "Renzo Pianbro",
                "Santiagbro Calatrava",
                "Broam Chomsky",
                "Evelyn Brah",
                "Bronus Wagner",
                "Brad Brohaus",
                "Giorgibro Armani",
                "Al Brolson",
                "Greg Brostertag",
                "Emilibro Estevez",
                "Paul Bro Bryant",
                "Pablo Picassbro",
                "Broto Baggins",
                "Diegbro Velazqeuz",
                "Larry",
                "Bromar Sharif",
                "Willem Dabroe",
                "Brolden Caulfield",
                "Broni Mitchell",
                "Truman Cabrote",
                "John Broltrane",
                "Broman Brolanski",
                "Gary Broldman",
                "Teddy Broosevelt",
                "Marilyn Monbroe",
                "Charles Brokowski",
                "Rimbraud",
                "Brogi Berra",
                "Czeslaw Mibroscz",
                "Paul Brauguin",
                "Tim Tebro",
                "Edgar Allen Bro",
                "Christopher Brolumbus",
                "Norah Brones",
                "Brofessor X",
                "Brofiteroles",
                "Rice o Broni",
                "Pete Brozelle",
                "The Sultan of Bronei",
                "C-3PBro",
                "Brodhisattva",
                "Brohsaphat",
                "Gandalf",
                "Bro Chi Minh",
                "Dirk Diggler",
                "Brodo Baggins",
                "Bromer Simpson",
                "Grady Sizemore",
                "Helmut Brohl",
                "Foghorn Leghorn",
                "Brobespierre",
                "Nicolas Sarbrozy",
                "Sherlock Brolmes",
                "John Brolmes",
                "Coolibro",
                "Broco Crisp",
                "Broald Dahl",
                "Bronan the Brahbarian",
                "Bro Derek",
                "Mr. Brojangles",
                "Bro Diddley",
                "Yo-Yo Brah",
                "BrO. J. Simpson",
                "Mephistophbroles",
                "Wolfgang Amadeus Brozart",
                "G.I. Bro",
                "Brosama bin Laden",
                "Magnetbro"
            };
            _registeredBroNames = new Dictionary<int, Player>();
            Player.Disconnected += Player_Disconnected;
            Player.Connected += Player_Connected;

            return _instance;
        }

        private static void Player_Connected(object sender, Events.PlayerConnectedEventArgs e)
        {
            if (Active)
            {
                GetInstance().RegisterPlayer(e.Player);
            }
        }

        private static void Player_Disconnected(object sender, Events.PlayerDisconnectedEventArgs e)
        {
            if (Active)
            {
                GetInstance().UnregisterPlayer(e.Player);
            }
        }

        public void RegisterPlayer(Player player)
        {
            if (player.Info.IsWarned) return;
            if (player.Info.IsMuted) return;
            if (player.Info.IsFrozen) return;
            try
            {
                if (_namesRegistered < _broNames.Count)
                {
                    Random randomizer = new Random();
                    int index = randomizer.Next(0, _broNames.Count);
                    int attempts = 0;
                    bool found = false;

                    if (player.Info.DisplayedName == null)
                    {
                        player.Info.changedName = false; //fix for rank problems during
                    }

                    else
                        player.Info.oldname = player.Info.DisplayedName;
                    player.Info.changedName = true; //if name is changed, true

                    while (!found)
                    {
                        _registeredBroNames.TryGetValue(index, out var output);

                        if (output == null)
                        {
                            found = true;
                            break;
                        }

                        attempts++;
                        index = randomizer.Next(0, _broNames.Count);

                        if (attempts > 2000)
                        {
                            // Not good :D
                            break;
                        }
                    }

                    if (found)
                    {
                        player.Message("Giving you name: " + _broNames[index]);
                        player.Info.DisplayedName = Color.ReplacePercentCodes(player.Info.Rank.Color + player.Info.Rank.Prefix + _broNames[index]);
                        _namesRegistered++;
                        _registeredBroNames[index] = player;
                    }
                    else
                    {
                        player.Message("Could not find a name for you.");
                    }
                }
                else
                {
                    player.Message("All bro names have been assigned.");
                }
            }
            catch (Exception ex)
            {
                Logger.Log(LogType.Error, "BroMode.RegisterPlayer: " + ex);
            }
        }

        public void UnregisterPlayer(Player p)
        {
            try
            {
                for (int i = 0; i < _broNames.Count; i++)
                {
                    if (!_registeredBroNames.ContainsKey(i) || !_registeredBroNames[i].Name.Equals(p.Name)) continue;
                    Logger.Log(LogType.SystemActivity, "Unregistering bro name '" + _broNames[i] + "' for player '" + p.Name + "'");
                    _registeredBroNames.Remove(i);
                    _namesRegistered--;
                    if (!p.Info.changedName)
                    {
                        p.Info.DisplayedName = null;
                    }

                    if (!p.Info.changedName) continue;
                    p.Info.DisplayedName = p.Info.oldname;
                    p.Info.oldname = null; //clears oldname if its ever removed in setinfo
                    p.Info.changedName = false;
                }
            }
            catch (Exception ex)
            {
                Logger.Log(LogType.Error, "BroMode.UnregisterPlayer: " + ex);
            }
        }
    }
}