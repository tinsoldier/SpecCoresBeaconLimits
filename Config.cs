using ProtoBuf;
using Sandbox.ModAPI;
using System.Collections.Generic;
using System.Xml.Serialization;
using VRage.Game.ModAPI;
using System.Linq;
using static BeaconLimits.Config.BeaconGroup;
using static BeaconLimits.Config;
using Sandbox.Game;
using VRageMath;

namespace BeaconLimits
{
    [ProtoContract]
    public class Config
    {
        [ProtoIgnore][XmlIgnore] public string Version = "1.00";
        [ProtoMember(1)][XmlElement("USE_MAX_BEACONS")] public bool _useMaxBeacons = false;
        [ProtoMember(2)][XmlElement("MAX_BEACONS")] public int _maxBeacons = 25;
        [ProtoMember(3)][XmlElement("TICK_UPDATE_RATE")] public int _updateRate = 300;
        [ProtoMember(4)][XmlElement("ALERT_RATE_SECONDS")] public int _alertRate = 300;
        [ProtoMember(5)][XmlElement("BEACON_GROUP")] public List<BeaconGroup> _beaconGroups = new List<BeaconGroup>();

        [ProtoContract]
        public class BeaconGroup
        {
            [ProtoMember(1)][XmlElement("GroupName")] public string GroupName;
            [ProtoMember(2)][XmlElement("BeaconSubtype")] public List<string> BeaconSubtypes = new List<string>();
            [ProtoMember(3)][XmlArray("Limits")] public List<Limit> Limits = new List<Limit>();

            public int GetLimitByMemberCount(int memberCount)
            {
                var validLimits = Limits.Where(x => x.MinRequiredMembers <= memberCount);
                return validLimits.Any() ? validLimits.Max(x => x.BeaconLimit) : 0;
                //int maxLimit = 0;
                //foreach(var limit in Limits)
                //{
                //    if (memberCount >= limit.MinRequiredMembers && limit.BeaconLimit > maxLimit)
                //    {
                //        MyVisualScriptLogicProvider.SendChatMessageColored($"Found Good Limit MinRequired:{limit.MinRequiredMembers}, BeaconLimit:{limit.BeaconLimit}", Color.Green, "[Server]");
                //        maxLimit = limit.BeaconLimit;
                //    }
                //    else
                //    {
                //        MyVisualScriptLogicProvider.SendChatMessageColored($"Found Bad Limit MinRequired:{limit.MinRequiredMembers}, BeaconLimit:{limit.BeaconLimit}", Color.Red, "[Server]");
                //    }
                //}
                //return maxLimit;
            }

            public int GetSmallestFactionLimit()
            {
                return Limits.OrderBy(x => x.MinRequiredMembers).FirstOrDefault()?.BeaconLimit ?? 0;
            }

            public int GetLimitByFaction(IMyFaction faction)
            {
                if (faction != null)
                {
                    var numMembers = faction.Members.Count;
                    return GetLimitByMemberCount(numMembers);
                }
                else
                {
                    return GetSmallestFactionLimit();
                }
            }

            [ProtoContract]
            public class Limit
            {
                [ProtoMember(1)][XmlAttribute("MinRequiredMembers")] public int MinRequiredMembers;
                [ProtoMember(2)][XmlAttribute("BeaconLimit")] public int BeaconLimit;
            }
        }

        public string GetGroupBySubtype(string subtype)
        {
            foreach (var group in _beaconGroups)
            {
                if (group.BeaconSubtypes.Contains(subtype))
                    return group.GroupName;
            }

            return null;
        }

        public static Config LoadConfig()
        {
            Config config = new Config();

            if (MyAPIGateway.Utilities.FileExistsInWorldStorage("SpecCoresBeaconLimit_Config.xml", typeof(Config)))
            {
                var reader = MyAPIGateway.Utilities.ReadFileInWorldStorage("SpecCoresBeaconLimit_Config.xml", typeof(Config));
                config = MyAPIGateway.Utilities.SerializeFromXML<Config>(reader.ReadToEnd());
                reader.Close();
            }
            else
            {
                using (var writer = MyAPIGateway.Utilities.WriteFileInWorldStorage("SpecCoresBeaconLimit_Config.xml", typeof(Config)))
                {
                    writer.Write(MyAPIGateway.Utilities.SerializeToXML<Config>(config));
                    writer.Close();
                }
            }

            return config;
        }
    }
}
