using ProtoBuf;
using Sandbox.Engine.Utils;
using System.Collections.Generic;
using System.Linq;

namespace BeaconLimits
{
    [ProtoContract]
    public class BeaconList
    {
        [ProtoMember(1)] public List<FactionList> factionList = new List<FactionList>();

        public BeaconList() { }

        public FactionList FindGridById(long entityId)
        {
            foreach (var data in factionList)
            {
                foreach (var dat2 in data.beaconData)
                {
                    if (entityId == dat2.gridId)
                        return data;
                }
            }

            return null;
        }

        /*public FactionList FindDataByBeaconId(long beaconID)
        {
            foreach (var data in factionList)
            {
                foreach (var dat2 in data.beaconData)
                {
                    foreach (var beacon in dat2.beacons)
                    {
                        if (beaconID == beacon)
                            return data;
                    }
                }
            }

            return null;
        }*/

        public FactionList FindDataFromFactionId(long factionID)
        {
            foreach (var data in factionList)
            {
                if (factionID == data.factionId)
                    return data;
            }

            return null;
        }

        public FactionList FindDataFromOwnerId(long ownerID)
        {
            foreach (var data in factionList)
            {
                if (data.factionId == 0 && ownerID == data.ownerId)
                    return data;
            }

            return null;
        }

        /*public void RemoveDataFromList(long beaconID, string beaconSubtype)
        {
            for (int i = factionList.Count - 1; i >= 0; i--)
            {
                for (int j = factionList[i].beaconData.Count - 1; j >= 0; j--)
                {
                    for(int k = factionList[i].beaconData[j].beacons.Count - 1; k >= 0; k--)
                    {
                        if (factionList[i].beaconData[j].beacons[k] == beaconID)
                        {
                            if (factionList[i].beaconData[j].beacons.Count > 1)
                                factionList[i].beaconData[j].beacons.RemoveAt(k);
                            else
                                factionList[i].beaconData.RemoveAt(j);

                            return;
                        }
                    }
                }
            }
        }*/
    }

    [ProtoContract]
    public class FactionList
    {
        [ProtoMember(1)] public List<BeaconData> beaconData;
        [ProtoMember(2)] public long factionId;
        [ProtoMember(3)] public long ownerId;
        [ProtoMember(4)] public int totalBeacons;
        [ProtoMember(5)] public Dictionary<string, List<long>> totalBeaconTypes;

        public FactionList() { }

        public FactionList(long factionID, string name, long gridId, long beaconId, long ownerID, int total, string beaconSubtype)
        {
            factionId = factionID;
            ownerId = ownerID;
            totalBeacons = total;
            BeaconData data = new BeaconData()
            {
                gridName = name,
                gridId = gridId,
                beacons = new Dictionary<string, List<long>>
                {
                    { beaconSubtype, new List<long>() { beaconId } }
                }
            };

            totalBeaconTypes = new Dictionary<string, List<long>>
            {
                { beaconSubtype, new List<long>() { beaconId } }
            };

            beaconData = new List<BeaconData>() { data };
        }

        public void AddBeaconSubtype(string beaconSubtype, long beaconId) 
        {
            if (totalBeaconTypes.ContainsKey(beaconSubtype))
                totalBeaconTypes[beaconSubtype].Add(beaconId);
            else
                totalBeaconTypes.Add(beaconSubtype, new List<long>() { beaconId } );
        }

        public BeaconData GetBeaconDataFromGridId(long gridID)
        {
            foreach(var beacon in beaconData)
            {
                if (beacon.gridId == gridID)
                    return beacon;
            }

            return null;
        }

        public Dictionary<string, int> GetGroupedBeaconCounts()
        {
            Dictionary<string, int> groupBeaconCounts = new Dictionary<string, int>();
            foreach (var beaconType in totalBeaconTypes.Keys)
            {
                List<Config.BeaconGroup> relevantGroups = Session.Instance.config._beaconGroups.Where(x => x.BeaconSubtypes.Contains(beaconType)).ToList();

                foreach (var group in relevantGroups)
                {
                    if (groupBeaconCounts.ContainsKey(group.GroupName))
                    {
                        groupBeaconCounts[group.GroupName] += totalBeaconTypes[beaconType].Count;
                    }
                    else
                    {
                        groupBeaconCounts.Add(group.GroupName, totalBeaconTypes[beaconType].Count);
                    }
                }
            }
            return groupBeaconCounts;
        }
    }

    [ProtoContract]
    public class BeaconData
    {
        [ProtoMember(1)] public string gridName;
        [ProtoMember(2)] public long gridId;
        [ProtoMember(3)] public Dictionary<string, List<long>> beacons;

        public BeaconData() { }

        public BeaconData(string subtype, long beaconId)
        {
            beacons.Add(subtype, new List<long>() { beaconId });
        }

        public BeaconData(string name, long gridID, long beaconID, string beaconSubtype)
        {
            gridName = name;
            gridId = gridID;
            beacons = new Dictionary<string, List<long>>
            {
                { beaconSubtype, new List<long>() { beaconID } }
            };
        }
    }
}
