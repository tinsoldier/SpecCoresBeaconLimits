using ProtoBuf;
using Sandbox.ModAPI;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace BeaconLimits
{
    [ProtoContract]
    public class Config
    {
        [ProtoIgnore] [XmlIgnore] public string Version = "1.00";
        [ProtoMember(1)] [XmlElement("USE_MAX_BEACONS")] public bool _useMaxBeacons = false;
        [ProtoMember(2)] [XmlElement("MAX_BEACONS")] public int _maxBeacons = 25;
        [ProtoMember(3)] [XmlElement("TICK_UPDATE_RATE")] public int _updateRate = 300;
        [ProtoMember(4)] [XmlElement("ALERT_RATE_SECONDS")] public int _alertRate = 300;
        [ProtoMember(5)] [XmlElement("BEACON_SUBTYPE")] public BeaconSubtype[] _beaconSubtypes;



        public Config()
        {
            List<BeaconSubtype> list = new List<BeaconSubtype>
            {
                GetNewBeaconSubtypes(),
                GetNewBeaconSubtypes()
            };

            _beaconSubtypes = list.ToArray();
        }

        [ProtoContract]
        public struct BeaconSubtype
        {
            [ProtoMember(1)] [XmlElement("BeaconSubtype")] public string subtype;
            [ProtoMember(2)] [XmlElement("Limit")] public int[] limit;
        }

        public BeaconSubtype GetNewBeaconSubtypes()
        {
            BeaconSubtype beaconSubtype = new BeaconSubtype()
            {
                subtype = "Name",
                limit = new int[4] { 1, 2, 3, 4 }
            };

            return beaconSubtype;
        }

        public static Config LoadConfig()
        {
            Config config = new Config();

            if (MyAPIGateway.Utilities.FileExistsInWorldStorage("SpecCoresBeaconLimit_Config.xml", typeof(Config)) == true)
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
