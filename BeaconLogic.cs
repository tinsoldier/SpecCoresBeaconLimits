using Sandbox.Common.ObjectBuilders;
using Sandbox.Game;
using Sandbox.ModAPI;
using System.Linq;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;
using static BeaconLimits.Config;

namespace BeaconLimits
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_Beacon), false)]
    public class BeaconLogic : MyGameLogicComponent
    {
        public IMyBeacon beacon;
        private bool isServer;



        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);

            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            isServer = MyAPIGateway.Session.IsServer;
            beacon = Entity as IMyBeacon;
            if (beacon == null) return;
            if (!isServer) return;

            if (!Session.Instance.beaconSubtypes.Contains(beacon.BlockDefinition.SubtypeName)) return;
            if (!Session.Instance.beacons.Contains(beacon))
                Session.Instance.beacons.Add(beacon);

            beacon.OnMarkForClose += Session.Instance.OnMarkClose;
        }

        /*public override void MarkForClose()
        {
            base.MarkForClose();

            //Unregister any handlers here
            if (!isServer) return;
            if (beacon == null) return;
            //if (beacon.CubeGrid?.Physics == null) return;
            if (!Session.Instance.beacons.Contains(beacon)) return;
            Session.Instance.beacons.Remove(beacon);

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
        }*/
    }
}
