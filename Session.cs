﻿using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;

namespace BeaconLimits
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class Session : MySessionComponentBase
    {
        public int UPDATE_RATE = 300; // 60 ticks * 5 seconds
        public int ALERT_RATE = 300; // how often to spam chat alerts in seconds
        public int MAX_BEACONS = 25; // beacons per faction, if player not in faction then per player


        public static Session Instance;
        private int ticks;
        private bool isServer;
        private bool isDedicated;
        private bool init;
        public Config config;
        public List<IMyBeacon> beacons = new List<IMyBeacon>();
        public BeaconList beaconCache = new BeaconList();
        public ConcurrentDictionary<long, int> factionAlerts = new ConcurrentDictionary<long, int>();
        public ConcurrentDictionary<long, int> playerAlerts = new ConcurrentDictionary<long, int>();
        public Dictionary<long, string> allPlayers = new Dictionary<long, string>();
        public List<string> beaconSubtypes = new List<string>();
        //private List<IMyPlayer> players = new List<IMyPlayer>();

        public override void LoadData()
        {
            isServer = MyAPIGateway.Session.IsServer;
            isDedicated = MyAPIGateway.Utilities.IsDedicated;
            
            if (isServer)
            {
                config = Config.LoadConfig();

                if (config != null)
                {
                    UPDATE_RATE = config._updateRate;
                    ALERT_RATE = config._alertRate;
                    MAX_BEACONS = config._maxBeacons;

                    GetAllBeaconSubtypes();
                }

                //MyEntities.OnEntityCreate += EntityCreate;
                //MyEntities.OnEntityRemove += EntityRemoved;
            }
        }
        public override void BeforeStart()
        {
            MyAPIGateway.Multiplayer.RegisterMessageHandler(4709, Comms.MessageHandler);
            MyAPIGateway.Utilities.MessageEntered += ChatHandler;
            Instance = this;

            if (isServer)
            {
                LoadPlayerData();
                
            }
        }

        public void GetAllBeaconSubtypes()
        {
            beaconSubtypes.Clear();
            foreach(var subtypes in config._beaconSubtypes)
            {
                beaconSubtypes.Add(subtypes.subtype);
            }
                
        }

        public override void UpdateBeforeSimulation()
        {
            Init();
            if (!isServer) return;

            ticks++;
            RunAlerts();
            RunUpdate(); // ticks is reset during this method
        }

        private void Init()
        {
            if (!isServer && !init)
            {
                IMyPlayer player = MyAPIGateway.Session.LocalHumanPlayer;
                if (player == null) return;

                Comms.SendPlayerUpdate(player.IdentityId, player.DisplayName, player.SteamUserId);
                init = true;
            }
        }

        public void RunUpdate()
        {
            if (ticks % UPDATE_RATE != 0) return;
            ticks = 0;

            beaconCache.factionList.Clear();
            for (int i = beacons.Count - 1; i >= 0; i--)
            {
                IMyBeacon beacon = beacons[i];
                if (beacon.MarkedForClose) continue;
                if (beacon.CubeGrid.Physics == null)
                {
                    beacons.RemoveAt(i);
                    beacon.OnMarkForClose -= OnMarkClose;
                    continue;
                }

                IMyFaction blockFaction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(beacon.OwnerId);
                if (blockFaction != null && blockFaction.Tag.Length > 3) continue;

                FactionList data = blockFaction != null ? beaconCache.FindDataFromFactionId(blockFaction.FactionId) : beaconCache.FindDataFromOwnerId(beacon.OwnerId);
                if (data == null)
                {
                    data = new FactionList(blockFaction != null ? blockFaction.FactionId : 0, beacon.CubeGrid.CustomName, beacon.CubeGrid.EntityId, beacon.EntityId, beacon.OwnerId, 1, beacon.BlockDefinition.SubtypeName);
                    beaconCache.factionList.Add(data);
                    continue;
                }

                BeaconData bData = data.GetBeaconDataFromGridId(beacon.CubeGrid.EntityId);
                if (bData == null)
                {
                    bData = new BeaconData(beacon.CubeGrid.CustomName, beacon.CubeGrid.EntityId, beacon.EntityId, beacon.BlockDefinition.SubtypeName);
                    data.beaconData.Add(bData);
                    data.totalBeacons++;
                    data.AddBeaconSubtype(beacon.BlockDefinition.SubtypeName, beacon.EntityId);
                    continue;
                }

                data.AddBeaconSubtype(beacon.BlockDefinition.SubtypeName, beacon.EntityId);

                if (bData.beacons.ContainsKey(beacon.BlockDefinition.SubtypeName))
                    bData.beacons[beacon.BlockDefinition.SubtypeName].Add(beacon.EntityId);
                else
                    bData.beacons.Add(beacon.BlockDefinition.SubtypeName, new List<long>() { beacon.EntityId } );

                data.totalBeacons++;
            }

            foreach (var data in beaconCache.factionList)
            { 
                IMyFaction faction = MyAPIGateway.Session.Factions.TryGetFactionById(data.factionId);
                bool factionOverLimits = false;
                if (config._useMaxBeacons && data.totalBeacons > MAX_BEACONS)
                {
                    if (faction != null)
                    {
                        factionOverLimits = true;
                        if (factionAlerts.ContainsKey(data.factionId)) continue;
                        factionAlerts.TryAdd(data.factionId, 0);
                        MyVisualScriptLogicProvider.SendChatMessageColored($"Faction '{faction.Name}' is over the {MAX_BEACONS} beacon limit. Please fix ASAP", Color.Red, "[Server]", 0, "Red");

                    }
                    else
                    {
                        factionOverLimits = true;
                        if (playerAlerts.ContainsKey(data.ownerId)) continue;
                        playerAlerts.TryAdd(data.ownerId, 0);
                        string playerName;
                        allPlayers.TryGetValue(data.ownerId, out playerName);
                        MyVisualScriptLogicProvider.SendChatMessageColored($"Player '{playerName}' is over the {MAX_BEACONS} beacon limit. Please fix ASAP", Color.Red, "[Server]", 0, "Red");

                    }
                }

                foreach (var beaconType in data.totalBeaconTypes.Keys)
                {
                    foreach (var configType in config._beaconSubtypes)
                    {
                        if (configType.subtype == beaconType)
                        {
                            int limit = 0;
                            if (faction != null)
                            {
                                if (faction.Members.Count >= configType.limit.Length)
                                    limit = configType.limit.Last();
                                else
                                    limit = configType.limit[faction.Members.Count - 1];

                                if (data.totalBeaconTypes[beaconType].Count > limit)
                                {
                                    factionOverLimits = true;
                                    if (factionAlerts.ContainsKey(data.factionId)) break;
                                    factionAlerts.TryAdd(data.factionId, 0);
                                    MyVisualScriptLogicProvider.SendChatMessageColored($"Faction '{faction.Name}' is over the {limit} beacon limit for {beaconType}. Please fix ASAP", Color.Red, "[Server]", 0, "Red");
                                    break;
                                }
                            }
                            else
                            {
                                limit = configType.limit.First();
                                if (data.totalBeaconTypes[beaconType].Count > limit)
                                {
                                    factionOverLimits = true;
                                    if (playerAlerts.ContainsKey(data.ownerId)) break;
                                    playerAlerts.TryAdd(data.ownerId, 0);
                                    string playerName;
                                    allPlayers.TryGetValue(data.ownerId, out playerName);
                                    MyVisualScriptLogicProvider.SendChatMessageColored($"Player '{playerName}' is over the {limit} beacon limit for {beaconType}. Please fix ASAP", Color.Red, "[Server]", 0, "Red");
                                    break;
                                }
                            }

                            break;
                        }
                    }

                    if (factionOverLimits) break;
                }

                if (!factionOverLimits)
                {
                    if (faction != null)
                    {
                        if (factionAlerts.ContainsKey(data.factionId))
                            factionAlerts.Remove(data.factionId);

                        continue;
                    }

                    if (playerAlerts.ContainsKey(data.ownerId))
                        playerAlerts.Remove(data.ownerId);
                }
            }

            foreach (var faction in factionAlerts)
            {
                IMyFaction myFaction = MyAPIGateway.Session.Factions.TryGetFactionById(faction.Key);
                if (myFaction != null) continue;

                int value;
                factionAlerts.TryRemove(faction.Key, out value);
            }

            foreach (var player in playerAlerts)
            {
                IMyFaction faction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(player.Key);
                if (faction == null) continue;

                int value;
                playerAlerts.TryRemove(player.Key, out value);
            }
        }

        public void RunAlerts()
        {
            if (ticks % 60 != 0) return;

            foreach (var key in factionAlerts.Keys)
            {
                if (factionAlerts[key] >= ALERT_RATE)
                {
                    IMyFaction faction = MyAPIGateway.Session.Factions.TryGetFactionById(key);
                    if (faction == null) continue;

                    var data = beaconCache.FindDataFromFactionId(key);
                    if (data == null) continue;

                    if (config._useMaxBeacons && data.totalBeacons > MAX_BEACONS)
                        MyVisualScriptLogicProvider.SendChatMessageColored($"Faction '{faction.Name}' is over the {MAX_BEACONS} beacon limit. Please fix ASAP", Color.Red, "[Server]", 0, "Red");

                    foreach (var beaconType in data.totalBeaconTypes.Keys)
                    {
                        foreach (var configType in config._beaconSubtypes)
                        {
                            if (configType.subtype == beaconType)
                            {
                                int limit = 0;
                                if (faction.Members.Count >= configType.limit.Length)
                                    limit = configType.limit.Last();
                                else
                                    limit = configType.limit[faction.Members.Count - 1];

                                if (data.totalBeaconTypes[beaconType].Count > limit)
                                {
                                    MyVisualScriptLogicProvider.SendChatMessageColored($"Faction '{faction.Name}' is over the {limit} beacon limit for {beaconType}. Please fix ASAP", Color.Red, "[Server]", 0, "Red");
                                    break;
                                }

                                break;
                            }
                        }
                    }
 
                    factionAlerts[key] = 0;
                    continue;
                }

                factionAlerts[key]++;
            }

            foreach (var key in playerAlerts.Keys)
            {
                if (playerAlerts[key] >= ALERT_RATE)
                {
                    var data = beaconCache.FindDataFromOwnerId(key);
                    if (data == null) continue;

                    if (config._useMaxBeacons && data.totalBeacons > MAX_BEACONS)
                    {
                        string playerName;
                        allPlayers.TryGetValue(key, out playerName);
                        MyVisualScriptLogicProvider.SendChatMessageColored($"Player '{playerName}' is over the {MAX_BEACONS} beacon limit. Please fix ASAP", Color.Red, "[Server]", 0, "Red");
                    }

                    foreach (var beaconType in data.totalBeaconTypes.Keys)
                    {
                        foreach (var configType in config._beaconSubtypes)
                        {
                            if (configType.subtype == beaconType)
                            {
                                int limit = 0;
                                {
                                    limit = configType.limit.First();
                                    if (data.totalBeaconTypes[beaconType].Count > limit)
                                    {
                                        string playerName;
                                        allPlayers.TryGetValue(data.ownerId, out playerName);
                                        MyVisualScriptLogicProvider.SendChatMessageColored($"Player '{playerName}' is over the {limit} beacon limit for {beaconType}. Please fix ASAP", Color.Red, "[Server]", 0, "Red");
                                        break;
                                    }
                                }

                                break;
                            }
                        }
                    }

                    playerAlerts[key] = 0;
                    continue;
                }

                playerAlerts[key]++;
            }
        }
            
        public void ChatHandler(string messageText, ref bool sendToOthers)
        {
            if (messageText.StartsWith("/beaconlimit"))
            {
                sendToOthers = false;
                long? clientId = MyAPIGateway.Session.LocalHumanPlayer?.IdentityId;
                ulong? steamId = MyAPIGateway.Session.LocalHumanPlayer?.SteamUserId;
                int index = -1;

                var split = messageText.Split('.');
                messageText += "\n" + clientId.ToString();
                messageText += "\n" + steamId.ToString();

                if (split.Length == 2)
                {
                    int.TryParse(split[1], out index);
                    if (beaconCache.factionList.Count == 0 || index == -1)
                    {
                        MyVisualScriptLogicProvider.SendChatMessageColored("Index doesn't exists, please run /beaconlimit again to get the correct index", Color.Red, "[Server]", clientId ?? 0, "Red");
                        return;
                    }

                    messageText += "\n" + index.ToString();
                    Comms.SendGpsToServer(messageText, beaconCache.factionList[0]);
                    return;
                }
                               
                Comms.SendChatToServer(messageText);
            }

            if (messageText.StartsWith("/adminbeaconlimit"))
            {
                sendToOthers = false;
                long? clientId = MyAPIGateway.Session.LocalHumanPlayer?.IdentityId;
                ulong? steamId = MyAPIGateway.Session.LocalHumanPlayer?.SteamUserId;

                messageText += "\n" + clientId.ToString();
                messageText += "\n" + steamId.ToString();

                Comms.SendChatToServer(messageText);
            }
        }

        public void GetFactionBeacons(long playerId, ulong steamId, bool isAdmin)
        {
            if (!isAdmin)
            {
                FactionList factionData;
                IMyFaction myFaction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(playerId);
                if (myFaction == null)
                    factionData = beaconCache.FindDataFromOwnerId(playerId);
                else
                    factionData = beaconCache.FindDataFromFactionId(myFaction.FactionId);

                if (factionData == null)
                {
                    MyVisualScriptLogicProvider.SendChatMessageColored("You or your faction does not own any beacons at this time.", Color.Red, "[Server]", playerId, "Red");
                    return;
                }
                Comms.SendDataToClient(factionData, steamId);

                return;
            }

            Comms.SendDataToAdmin(beaconCache, steamId, allPlayers);
        }

        public void CreateGps(long playerId, int index, FactionList factionList)
        {
            IMyEntity ent;
            if (index == -1 || factionList.beaconData.Count == 0 || factionList.beaconData.Count - 1 < index) return;

            if (!MyAPIGateway.Entities.TryGetEntityById(factionList.beaconData[index].gridId, out ent))
            {
                MyVisualScriptLogicProvider.SendChatMessageColored("Unable to find grid, make sure to refresh with /beaconlimit to get an updated index", Color.Red, "[Server]", playerId, "Red");
                return;
            }

            IMyCubeGrid grid = ent as IMyCubeGrid;
            if (grid == null) return;

            var gps = MyAPIGateway.Session.GPS.Create(grid.CustomName, "Beacon GPS", grid.GetPosition(), true, true);
            MyAPIGateway.Session.GPS.AddGps(playerId, gps);
        }

        public void SavePlayerData()
        {
            var newByteData = MyAPIGateway.Utilities.SerializeToBinary(allPlayers);
            var base64string = Convert.ToBase64String(newByteData);
            MyAPIGateway.Utilities.SetVariable("AllPlayers", base64string);
        }

        public void LoadPlayerData()
        {
            string saveData;
            MyAPIGateway.Utilities.GetVariable("AllPlayers", out saveData);
            if (string.IsNullOrEmpty(saveData))
            {
                if(isServer && !isDedicated)
                {
                    allPlayers.Add(MyAPIGateway.Session.LocalHumanPlayer.IdentityId, MyAPIGateway.Session.LocalHumanPlayer.DisplayName);
                    SavePlayerData();
                    return;
                }

                return;
            }

            byte[] byteData = Convert.FromBase64String(saveData);
            allPlayers = MyAPIGateway.Utilities.SerializeFromBinary<Dictionary<long, string>>(byteData);

            if (isServer && !isDedicated)
            {
                if (allPlayers.ContainsKey(MyAPIGateway.Session.LocalHumanPlayer.IdentityId)) return;
                allPlayers.Add(MyAPIGateway.Session.LocalHumanPlayer.IdentityId, MyAPIGateway.Session.LocalHumanPlayer.DisplayName);
                Instance.SavePlayerData();
            }
        }

        public IMyPlayer GetPlayerFromId(long playerId)
        {
            List<IMyPlayer> playerList = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(playerList);

            foreach (var player in playerList)
            {
                if (player.IdentityId == playerId) return player;
            }

            return null;
        }

        public void OnMarkClose(IMyEntity ent)
        {
            IMyBeacon beacon = ent as IMyBeacon;
            if (beacon == null) return;
            //if (beacon.CubeGrid.Physics == null) return;
            //if (!beaconSubtypes.Contains(beacon.BlockDefinition.SubtypeName)) return;

            if (beacons.Contains(beacon))
                beacons.Remove(beacon);
            else
                return;

            if (beacon.CubeGrid == null || beacon.CubeGrid.MarkedForClose) return;
            long owner = beacon.CubeGrid.BigOwners.FirstOrDefault();
            IMyFaction faction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(owner);
            if (faction == null && owner != 0)
            {
                MyVisualScriptLogicProvider.SendChatMessageColored($"Beacon destroyed on grid {beacon.CubeGrid?.CustomName}", Color.Red, "[Server]", owner, "Red");
                return;
            }

            if (faction != null && faction.Tag.Length <= 3)
            {
                var members = faction.Members;
                foreach (var member in members.Keys)
                {
                    MyVisualScriptLogicProvider.SendChatMessageColored($"Beacon destroyed on grid {beacon.CubeGrid?.CustomName}", Color.Red, "[Server]", member, "Red");
                }
            }
        }

        /*private void EntityCreate(MyEntity ent)
        {
            IMyBeacon beacon = ent as IMyBeacon;
            if (beacon == null) return;
            MyAPIGateway.Utilities.InvokeOnGameThread(() =>
            {
                if (beacon.CubeGrid.Physics == null) return;
                if (!beaconSubtypes.Contains(beacon.BlockDefinition.SubtypeName)) return;
                if (!beacons.Contains(beacon))
                    beacons.Add(beacon);
            });
      
            //beacon.OwnershipChanged += Beacon_OwnershipChanged;
        }*/

        /*private void Beacon_OwnershipChanged(IMyTerminalBlock block)
        {
            throw new NotImplementedException();
        }*/

        /*public string GetPlayerNameFromId(long playerId)
        {
            players.Clear();
            MyAPIGateway.Players.GetPlayers(players);

            foreach(var player in players)
            {
                if (player.IdentityId == playerId)
                    return player.DisplayName;
            }

            return "";
        }*/

        /*private void EntityRemoved(MyEntity ent)
        {
            IMyBeacon beacon = ent as IMyBeacon;
            if (beacon == null) return;
            if (beacon.CubeGrid.Physics == null) return;
            if (!beaconSubtypes.Contains(beacon.BlockDefinition.SubtypeName)) return;

            if (beacons.Contains(beacon))
                beacons.Remove(beacon);

            if (beacon.CubeGrid == null) return;
            long owner = beacon.CubeGrid.BigOwners.FirstOrDefault();
            IMyFaction faction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(owner);
            if (faction == null && owner != 0)
            {
                MyVisualScriptLogicProvider.SendChatMessageColored($"Beacon destroyed on grid {beacon.CubeGrid?.CustomName}", Color.Red, "[Server]", owner, "Red");
                return;
            }

            if (faction != null && faction.Tag.Length <= 3)
            {
                var members = faction.Members;
                foreach (var member in members.Keys)
                {
                    MyVisualScriptLogicProvider.SendChatMessageColored($"Beacon destroyed on grid {beacon.CubeGrid?.CustomName}", Color.Red, "[Server]", member, "Red");
                }
            }

            //beacon.OwnershipChanged -= Beacon_OwnershipChanged;
        }*/

        protected override void UnloadData()
        {
            Instance = null;
            MyAPIGateway.Multiplayer.UnregisterMessageHandler(4709, Comms.MessageHandler);
            MyAPIGateway.Utilities.MessageEntered -= ChatHandler;

            if (isServer)
            {
                //MyEntities.OnEntityCreate -= EntityCreate;
                //MyEntities.OnEntityRemove -= EntityRemoved;
            }
        }
    }    
}
