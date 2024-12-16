using ProtoBuf;
using Sandbox.Game;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using VRage.Game.ModAPI;
using VRage.Utils;
using static BeaconLimits.Config;

namespace BeaconLimits
{
    public enum DataType
    {
        ChatMessage,
        SendData,
        SendGps,
        SendAdminData,
        UpdatePlayerInfo,
        SendClientConfig
    }

    [ProtoContract]
    public class ObjectContainer
    {
        [ProtoMember(1)]
        public string stringData;

        [ProtoMember(2)]
        public FactionList factionData;

        [ProtoMember(3)]
        public ulong steamId;

        [ProtoMember(4)]
        public BeaconList beaconList;

        [ProtoMember(5)]
        public long longValue;

        [ProtoMember(6)]
        public Config config;

        [ProtoMember(7)]
        public Dictionary<long, string> players = new Dictionary<long, string>();
    }

    [ProtoContract]
    public class CommPackage
    {
        [ProtoMember(1)]
        public DataType Type;

        [ProtoMember(2)]
        public byte[] Data;


        public CommPackage()
        {
            Type = DataType.ChatMessage;
            Data = new byte[0];
        }

        public CommPackage(DataType type, ObjectContainer data)
        {
            Type = type;
            Data = MyAPIGateway.Utilities.SerializeToBinary(data);
        }
    }

    public static class Comms
    {
        public static void MessageHandler(byte[] data)
        {
            try
            {
                var package = MyAPIGateway.Utilities.SerializeFromBinary<CommPackage>(data);
                if (package == null) return;

                if (package.Type == DataType.ChatMessage)
                {
                    var encasedData = MyAPIGateway.Utilities.SerializeFromBinary<ObjectContainer>(package.Data);
                    if (encasedData == null) return;

                    var split = encasedData.stringData.Split('\n');
                    string message = split[0];
                    long clientId = 0;
                    ulong steamId = 0;
                    long.TryParse(split[1], out clientId);
                    ulong.TryParse(split[2], out steamId);
                    IMyPlayer player = Session.Instance.GetPlayerFromId(clientId);
                    if (clientId == 0 || steamId == 0 || player == null) return;

                    bool IsAdmin = player.PromoteLevel == MyPromoteLevel.Admin || player.PromoteLevel == MyPromoteLevel.Owner;
                    if (message.StartsWith("/beaconlimit"))
                        Session.Instance.GetFactionBeacons(clientId, steamId, false);

                    if (message.StartsWith("/adminbeaconlimit"))
                        Session.Instance.GetFactionBeacons(clientId, steamId, IsAdmin);

                    return;
                }

                if (package.Type == DataType.SendData)
                {
                    var encasedData = MyAPIGateway.Utilities.SerializeFromBinary<ObjectContainer>(package.Data);
                    if (encasedData == null) return;
                    
                    Session.Instance.beaconCache.factionList.Clear();
                    Session.Instance.beaconCache.factionList.Add(encasedData.factionData);

                    IMyFaction myFaction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(MyAPIGateway.Session.LocalHumanPlayer.IdentityId);
                    int index = 0;
                    string message = "Index | Grid Name | Subtype | Group | # Beacon Subtype On Grid\n ------------------------------------------------\n\n";
                    foreach (var faction in encasedData.factionData.beaconData)
                    {
                        message += $" {index} {faction.gridName} - ";
                        if (faction.beacons.Keys.Count <= 1)
                        {
                            foreach (var beaconType in faction.beacons.Keys)
                            {
                                var groupType = Session.Instance.config.GetGroupBySubtype(beaconType);
                                message += $"{beaconType} - {groupType} - {faction.beacons[beaconType].Count}\n";
                            }
                        }
                        else
                        {
                            List<string> keys = new List<string>(faction.beacons.Keys);
                            for (int i = 0; i < faction.beacons.Keys.Count; i++)
                            {
                                if (i == 0)
                                    message += $"{keys[i]} - {faction.beacons[keys[i]].Count}";
                                else
                                    message += $", {keys[i]} - {faction.beacons[keys[i]].Count}";
                            }

                            message += "\n\n";
                        }
                        

                        index++;
                    }
                    
                    var groupedBeaconCounts = encasedData.factionData.GetGroupedBeaconCounts();

                    message += $"\n\n Beacon Subtypes/Totals:\n";
                    foreach (var beaconGroup in Session.Instance.config._beaconGroups)
                    {
                        int limit = beaconGroup.GetLimitByFaction(myFaction);

                        List<long> list = new List<long>();
                        encasedData.factionData.totalBeaconTypes.TryGetValue(beaconGroup.GroupName, out list);
                        int count;
                        groupedBeaconCounts.TryGetValue(beaconGroup.GroupName, out count);
                        //if (list == null)
                        //    list = new List<long>();

                        message += $" {beaconGroup.GroupName} = {count}/{limit}\n";
                    }

                    if (Session.Instance.config._useMaxBeacons)
                        message += $"\n Total Beacons = {encasedData.factionData.totalBeacons}/{Session.Instance.MAX_BEACONS}\n\n";

                    message += " \n*** Type in the index number that is before the grid name to place a gps on that grid. Ex: /beaconlimit.2 ***";

                    if (myFaction != null)
                        MyAPIGateway.Utilities.ShowMissionScreen($"{myFaction.Name}'s Beacon Limits", "", null, message, null, "Ok");
                    else
                        MyAPIGateway.Utilities.ShowMissionScreen($"Your Beacon Limits", "", null, message, null, "Ok");

                    MyClipboardHelper.SetClipboard(message);
                    return;
                }

                if (package.Type == DataType.SendAdminData)
                {
                    var encasedData = MyAPIGateway.Utilities.SerializeFromBinary<ObjectContainer>(package.Data);
                    if (encasedData == null) return;

                    if(encasedData.beaconList.factionList.Count > 1)
                    {
                        /*encasedData.beaconList.factionList.Sort(delegate (FactionList x, FactionList y) {
                            return y.totalBeacons.CompareTo(x.totalBeacons);
                        

                        encasedData.beaconList.factionList.Sort(delegate (FactionList x, FactionList y) {
                            return y.totalBeaconTypes.Count.CompareTo(x.totalBeaconTypes.Count);
                        });*/
                    }
                    if (encasedData.players == null) return;
                    MyLog.Default.WriteLineAndConsole($"AllPlayers = {encasedData.players.Count}"); 
                    foreach(var player in encasedData.players.Keys)
                    {
                        MyLog.Default.WriteLineAndConsole($"AllPlayers = {player} - {encasedData.players[player]}");
                    }

                    string message = " Grid Name | Beacon Subtype | # Beacon Subtype On Grid\n ------------------------------------------------------------------------\n\n";
                    foreach (var faction in encasedData.beaconList.factionList)
                    {
                        message += " -----------------------\n";
                        IMyFaction myFaction = MyAPIGateway.Session.Factions.TryGetFactionById(faction.factionId);

                        if (myFaction != null)
                        {
                            message += $" {myFaction.Name} ({myFaction.Tag})\n -----------------------\n";
                        }
                        else
                        {
                            string playerName;
                            encasedData.players.TryGetValue(faction.ownerId, out playerName);
                            message += $" {playerName}\n -----------------------\n";
                        }

                        foreach (var bData in faction.beaconData)
                        {
                            message += $" {bData.gridName} - ";

                            if (bData.beacons.Keys.Count <= 1)
                            {
                                foreach (var beaconType in bData.beacons.Keys)
                                    message += $"{beaconType} - {bData.beacons[beaconType].Count}\n";
                            }
                            else
                            {
                                List<string> keys = new List<string>(bData.beacons.Keys);
                                for (int i = 0; i < bData.beacons.Keys.Count; i++)
                                {
                                    if (i == 0)
                                        message += $"{keys[i]} - {bData.beacons[keys[i]].Count}";
                                    else
                                        message += $", {keys[i]} - {bData.beacons[keys[i]].Count}";
                                }
                            }
                        }

                        message += $"\n Beacon Subtype Totals:\n";

                        foreach (var beaconGroup in Session.Instance.config._beaconGroups)
                        {
                            int limit = beaconGroup.GetLimitByFaction(myFaction);

                            List<long> list = new List<long>();
                            encasedData.factionData.totalBeaconTypes.TryGetValue(beaconGroup.GroupName, out list);
                            if (list == null)
                                list = new List<long>();

                            message += $" {beaconGroup.GroupName} = {list.Count}/{limit}\n";
                        }

                        if (Session.Instance.config._useMaxBeacons)
                            message += $"\n Total Beacons = {faction.totalBeacons}/{Session.Instance.MAX_BEACONS}\n\n";
                    }

                    MyAPIGateway.Utilities.ShowMissionScreen($"Global Beacon Limits", "", null, message, null, "Ok");
                    MyClipboardHelper.SetClipboard(message);
                    return;
                }

                if (package.Type == DataType.SendGps)
                {
                    var encasedData = MyAPIGateway.Utilities.SerializeFromBinary<ObjectContainer>(package.Data);
                    if (encasedData == null) return;

                    var split = encasedData.stringData.Split('\n');
                    string message = split[0];
                    long clientId = 0;
                    ulong steamId = 0;
                    int index = -1;
                    long.TryParse(split[1], out clientId);
                    ulong.TryParse(split[2], out steamId);
                    int.TryParse(split[3], out index);
                    
                    if (clientId == 0 || steamId == 0 || index == -1) return;
                    if (message.StartsWith("/beaconlimit"))
                        Session.Instance.CreateGps(clientId, index, encasedData.factionData);
                }

                if (package.Type == DataType.UpdatePlayerInfo)
                {
                    var encasedData = MyAPIGateway.Utilities.SerializeFromBinary<ObjectContainer>(package.Data);
                    if (encasedData == null) return;

                    SendClientConfig(encasedData.steamId);

                    if (Session.Instance.allPlayers.ContainsKey(encasedData.longValue)) return;
                    Session.Instance.allPlayers.Add(encasedData.longValue, encasedData.stringData);
                    Session.Instance.SavePlayerData();

                    return;
                }

                if (package.Type == DataType.SendClientConfig)
                {
                    var encasedData = MyAPIGateway.Utilities.SerializeFromBinary<ObjectContainer>(package.Data);
                    if (encasedData == null) return;

                    Session.Instance.config = encasedData.config;

                    List<BeaconGroup> temp = new List<BeaconGroup>();
                    for (int i = Session.Instance.config._beaconGroups.Count - 1; i >= 0; i--)
                    {
                        if (Session.Instance.config._beaconGroups[i].GroupName != "Name")
                            temp.Add(Session.Instance.config._beaconGroups[i]);
                    }

                    Session.Instance.config._beaconGroups = temp.ToList();
                    Session.Instance.UPDATE_RATE = encasedData.config._updateRate;
                    Session.Instance.ALERT_RATE = encasedData.config._alertRate;
                    Session.Instance.MAX_BEACONS = encasedData.config._maxBeacons;

                    return;
                }
            }
            catch (Exception ex)
            {

            }
        }

        public static void SendChatToServer(string message)
        {
            ObjectContainer objectContainer = new ObjectContainer()
            {
                stringData = message
            };

            CommPackage package = new CommPackage(DataType.ChatMessage, objectContainer);
            var sendData = MyAPIGateway.Utilities.SerializeToBinary(package);
            MyAPIGateway.Multiplayer.SendMessageToServer(4709, sendData);
        }

        public static void SendDataToClient(FactionList data, ulong steamId)
        {
            ObjectContainer objectContainer = new ObjectContainer()
            {
                factionData = data,
                steamId = steamId
            };

            CommPackage package = new CommPackage(DataType.SendData, objectContainer);
            var sendData = MyAPIGateway.Utilities.SerializeToBinary(package);
            MyAPIGateway.Multiplayer.SendMessageTo(4709, sendData, steamId);
        }

        public static void SendDataToAdmin(BeaconList data, ulong steamId, Dictionary<long, string> players)
        {
            ObjectContainer objectContainer = new ObjectContainer()
            {
                beaconList = data,
                steamId = steamId,
                players = new Dictionary<long, string>(players)
            };

            CommPackage package = new CommPackage(DataType.SendAdminData, objectContainer);
            var sendData = MyAPIGateway.Utilities.SerializeToBinary(package);
            MyAPIGateway.Multiplayer.SendMessageTo(4709, sendData, steamId);
        }

        public static void SendGpsToServer(string message, FactionList factionList)
        {
            ObjectContainer objectContainer = new ObjectContainer()
            {
                stringData = message,
                factionData = factionList
            };

            CommPackage package = new CommPackage(DataType.SendGps, objectContainer);
            var sendData = MyAPIGateway.Utilities.SerializeToBinary(package);
            MyAPIGateway.Multiplayer.SendMessageToServer(4709, sendData);
        }

        public static void SendPlayerUpdate(long playerId, string playerName, ulong steamId)
        {
            ObjectContainer objectContainer = new ObjectContainer()
            {
                longValue = playerId,
                stringData = playerName,
                steamId = steamId
            };

            CommPackage package = new CommPackage(DataType.UpdatePlayerInfo, objectContainer);
            var sendData = MyAPIGateway.Utilities.SerializeToBinary(package);
            MyAPIGateway.Multiplayer.SendMessageToServer(4709, sendData);
        }

        public static void SendClientConfig(ulong steamdId)
        {
            ObjectContainer objectContainer = new ObjectContainer()
            {
                config = Session.Instance.config
            };

            CommPackage package = new CommPackage(DataType.SendClientConfig, objectContainer);
            var sendData = MyAPIGateway.Utilities.SerializeToBinary(package);
            MyAPIGateway.Multiplayer.SendMessageTo(4709, sendData, steamdId);
        }
    }
}
