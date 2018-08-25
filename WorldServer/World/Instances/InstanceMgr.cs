﻿using Common;
using FrameWork;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SystemData;
using WorldServer.Services.World;

namespace WorldServer
{
    public class InstanceMgr
    {
        private Dictionary<ushort, Instance> _instances = new Dictionary<ushort, Instance>();

        public InstanceMgr() { }
		
        public bool ZoneIn(Player player, byte instancetyp, Zone_jump Jump = null)
        {
            // jump type 4 = realm 5 = raid 6 = group instances
            ushort zoneID;

            if (Jump == null)
                zoneID = player.Info.Value.ZoneId;
            else
                zoneID = Jump.ZoneID;

            Instance_Info II;
            InstanceService._InstanceInfo.TryGetValue(zoneID, out II);
            ushort InstanceMainID = II.Entry;
            ushort instanceid = 0;
            byte maxplayers = 6;
			
			// Group Raid Instance
			if (instancetyp == 5)
				maxplayers = 24;

			// find instance ID
			if (player.PriorityGroup != null && player.PriorityGroup.GetLeader() != player)
			{
				instanceid = Find_OpenInstanceoftheplayer(player.PriorityGroup.GetLeader(), zoneID);
			}
			else
				instanceid = Find_OpenInstanceoftheplayer(player, zoneID);

			if (instanceid == 0 && Jump == null)
				return false;

			// create new instance
			if (instanceid == 0)
				instanceid = Create_new_instance(player, Jump);
			// ZARU: Commented out groupleader check
			//if (instanceid == 0 && (player.PriorityGroup == null || player.PriorityGroup.GetLeader() == player))
			//	instanceid = Create_new_instance(player, Jump);
			
			//if (instanceid == 0)
			//{
			//	player.SendClientMessage("Your Groupleader needs to enter first", ChatLogFilters.CHATLOGFILTERS_USER_ERROR);
			//	return false;
			//}

			if (!Join_Instance(player, instanceid, Jump, maxplayers, InstanceMainID))
				return false;

			return true;
		}

		private ushort Find_OpenInstanceoftheplayer(Player player, ushort ZoneID)
        {
            lock (_instances)
            {
                foreach(KeyValuePair<ushort, Instance> ii in _instances)
                {
                    if (ii.Value.ZoneID == ZoneID && ii.Value._Players.Contains(player.Name))
                    {
                        return ii.Key;
                    }
                }
            }
            return 0;
        }

        private ushort Create_new_instance(Player player, Zone_jump Jump)
        {
            lock (_instances)
            {
                for (ushort i = 1; i < ushort.MaxValue ; i++)
                {
                    if (!_instances.ContainsKey(i))
                    {
                        if (Jump.ZoneID == 179)
                        {
                            TOTVL ints = null;
                            Instance_Lockouts deadbosses = null;
							if (player._Value.GetLockout(Jump.InstanceID) != null)
							{
								if (player.PriorityGroup == null) // solo player gets his own lockouts
									InstanceService._InstanceLockouts.TryGetValue(player._Value.GetLockout(Jump.InstanceID), out deadbosses);
								else // group players gets the lockout of the leader
									InstanceService._InstanceLockouts.TryGetValue(player.PriorityGroup.GetLeader()._Value.GetLockout(Jump.InstanceID), out deadbosses);
							}
							ints = new TOTVL(Jump.ZoneID, i, 0, deadbosses);
                            _instances.Add(i, ints);
                            return i;
                        }
                        else
                        {
                            Instance ints = null;
                            Instance_Lockouts deadbosses = null;
                            if (player._Value.GetLockout(Jump.InstanceID) != null)
							{	
								if (player.PriorityGroup == null) // solo player gets his own lockouts
									InstanceService._InstanceLockouts.TryGetValue(player._Value.GetLockout(Jump.InstanceID), out deadbosses);
								else // group players gets the lockout of the leader
									InstanceService._InstanceLockouts.TryGetValue(player.PriorityGroup.GetLeader()._Value.GetLockout(Jump.InstanceID), out deadbosses);
							}
                            ints = new Instance(Jump.ZoneID, i, 0, deadbosses);
                            _instances.Add(i, ints);
                            return i;
                        }
                    }
                }
            }
            return 0;
        }

        private bool Join_Instance(Player player, ushort Instanceid, Zone_jump Jump, int maxplayers, ushort InstancemainID)
        {
            lock (_instances)
            {
                Instance inst;
                _instances.TryGetValue(Instanceid, out inst);
				
                if (inst.Encounterinprogress)
                {
                    player.SendClientMessage("There is an Encounter in progress you cannot enter now", ChatLogFilters.CHATLOGFILTERS_USER_ERROR);
                    return false;
                }

                if (maxplayers == 0 || inst.Region.Players.Count <= maxplayers)
                {
                    if (Jump != null && Jump.ZoneID == 179)
                        ((TOTVL)inst).AddPlayer(player, Jump);
                    else
                        inst.AddPlayer(player, Jump);
                }
                else
                {
                    player.SendClientMessage("Instance is full", ChatLogFilters.CHATLOGFILTERS_USER_ERROR);
                    return false;
                }
            }
            return true;
        }

        public void closeInstance(Instance inst, ushort ID)
        {
            _instances.TryGetValue(ID, out inst);
            _instances.Remove(ID);

            inst = null;
        }

        public void sendInstanceInfo(Player plr, ushort instanceid)
        {
            if (instanceid == 0)
            {
                plr.SendClientMessage("Total instances =" + _instances.Count(), ChatLogFilters.CHATLOGFILTERS_SAY);
                lock (_instances)
                {
                    foreach (KeyValuePair<ushort, Instance> i in _instances)
                    {
                        plr.SendClientMessage("Instance id = " + i.Key + "  map= " + i.Value.Info.Name + "  Players: " + i.Value.Region.Players.Count, ChatLogFilters.CHATLOGFILTERS_SAY);
                    }
                }
            }
            else
            {
                Instance i;
                _instances.TryGetValue(instanceid, out i);
                if (i == null)
                {
                    plr.SendClientMessage("Instance id = " + instanceid + "not found", ChatLogFilters.CHATLOGFILTERS_SAY);
                    return;
                }
                plr.SendClientMessage("Instance id = " + instanceid + "  Map= " + i.Info.Name + "  Players: " + i.Region.Players.Count, ChatLogFilters.CHATLOGFILTERS_SAY);
                string players = string.Empty;
                foreach (Player pl in i.Region.Players)
                {
                    players += pl.Name + "  ,";
                }
                plr.SendClientMessage("Players: " + players, ChatLogFilters.CHATLOGFILTERS_SAY);
            }
        }
    }
}
