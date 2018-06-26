﻿using System;
using System.Collections.Generic;
using System.Linq;
using SystemData;
using Common;
using FrameWork;
using GameData;
using WorldServer.World.BattleFronts;
using WorldServer.World.Objects.PublicQuests;
using WorldServer.World.BattleFronts.Keeps;
using WorldServer.World.BattleFronts.Objectives;
using WorldServer.Services.World;
using WorldServer.World.Battlefronts.Apocalypse;

namespace WorldServer
{
    public sealed class ProximityProgressingBattleFront : ProximityBattleFront
    {
        public readonly BattleFrontStatus _BattleFrontStatus;

        // This is used to modify the timer of VP Update - default from Aza system was 120 seconds, 
        // so TIMER_MODIFIER should be set to 1.0f, currently we are cutting it by half, change it
        // back to 1.0f to restore default value. This also cuts the Lock timers on BOs - one const
        // to rule them all (timers).
        private const float TIMER_MODIFIER = 0.5f;

        public override string ActiveZoneName => Zones[_BattleFrontStatus?.OpenZoneIndex ?? 1].Name;

        public ProximityProgressingBattleFront(RegionMgr region, bool oRvRFront) : base(region, oRvRFront)
        {
            _BattleFrontStatus = BattleFrontService.GetStatusFor(region.RegionId);

            // RB   4/24/2016   A lot of non-RvR zones are included in the T4 regions, so those need to be discarded. And we need to do this manually.

            switch (Region.RegionId)
            {
                case 2: // T4 Dwarf vs Greenskin
                    Zones = new List<Zone_Info>
                    {
                        Region.ZonesInfo.Find(z => z.ZoneId == 3),
                        Region.ZonesInfo.Find(z => z.ZoneId == 5),
                        Region.ZonesInfo.Find(z => z.ZoneId == 9)
                    };
                    break;
                case 11: // T4 Empire vs Chaos
                    Zones = new List<Zone_Info>
                    {
                        Region.ZonesInfo.Find(z => z.ZoneId == 103),
                        Region.ZonesInfo.Find(z => z.ZoneId == 105),
                        Region.ZonesInfo.Find(z => z.ZoneId == 109)
                    };
                    break;
                case 4: // T4 High Elf vs Dark Elf
                    Zones = new List<Zone_Info>
                    {
                        Region.ZonesInfo.Find(z => z.ZoneId == 203),
                        Region.ZonesInfo.Find(z => z.ZoneId == 205),
                        Region.ZonesInfo.Find(z => z.ZoneId == 209)
                    };
                    break;
                // Here we start to implement the DoomsDay Event pairings - that is, T2 T3 T4 merge - not used :(
                case 6: // T3 Empire vs Chaos
                    Zones = new List<Zone_Info>
                    {
                        Region.ZonesInfo.Find(z => z.ZoneId == 102), // High Pass
                        Region.ZonesInfo.Find(z => z.ZoneId == 108)  // Talabecland
                    };
                    break;
                case 10: // T3 Dwarf vs Greenskin
                    Zones = new List<Zone_Info>
                    {
                        Region.ZonesInfo.Find(z => z.ZoneId == 2), // The Badlands
                        Region.ZonesInfo.Find(z => z.ZoneId == 8)  // Black Fire Pass
                    };
                    break;
                case 12: // T2 Dwarf vs Greenskin
                    Zones = new List<Zone_Info>
                    {
                        Region.ZonesInfo.Find(z => z.ZoneId == 1), // Marshes of Madness
                        Region.ZonesInfo.Find(z => z.ZoneId == 7)  // Barak V

                    };
                    break;
                case 14: // T2 Empire vs Chaos
                    Zones = new List<Zone_Info>
                    {
                        Region.ZonesInfo.Find(z => z.ZoneId == 101), // Troll Country
                        Region.ZonesInfo.Find(z => z.ZoneId == 107)  // Ostland
                    };
                    break;
                case 15: // T2 High Elf vs Dark Elf
                    Zones = new List<Zone_Info>
                    {
                        Region.ZonesInfo.Find(z => z.ZoneId == 201), // The Shadowlands
                        Region.ZonesInfo.Find(z => z.ZoneId == 207)  // Ellyrion
                    };
                    break;
                case 16: // T3 High Elf vs Dark Elf
                    Zones = new List<Zone_Info>
                    {
                        Region.ZonesInfo.Find(z => z.ZoneId == 202), // Avelorn
                        Region.ZonesInfo.Find(z => z.ZoneId == 208)  // Saphery
                    };
                    break;
            }

            // RB   4/29/2016   If this is a T4 Campaign region and we are building this BattleFront for the first time, load it after setting BOs.
            LoadPairing();
        }

        // [xx3, xx5, xx9] x [order, destro]
        private readonly byte[,] _held = new byte[3, 3];

        #region Keep Doors

        public override float GetDoorRegenFactor(Realms team, ushort zone)
        {
            switch (_held[Zones.FindIndex(z => z.ZoneId == zone), (byte)team])
            {
                case 0:
                    return 0f;
                case 1:
                    return 1f;
                case 2:
                    return 2f;
                case 3:
                    return 3f;
                case 4:
                    return 4f;
                default:
                    return 0f;
            }
        }

        public override bool CanAttackDoor(Realms realm, ushort zoneId)
        {
            if (realm == Realms.REALMS_REALM_ORDER)
                return _held[Zones.FindIndex(z => z.ZoneId == zoneId), 2] >= 3;
            return _held[Zones.FindIndex(z => z.ZoneId == zoneId), 1] >= 3;
        }

        #endregion

        #region Capture Events
        public override void ObjectiveCaptured(Realms oldRealm, Realms newRealm, ushort zoneId)
        {
            _held[Zones.FindIndex(z => z.ZoneId == zoneId), (byte)newRealm]++;

            _held[Zones.FindIndex(z => z.ZoneId == zoneId), (byte)oldRealm]--;

            /*if (oldRealm != Realms.REALMS_REALM_NEUTRAL)
            {
                _held[Zones.FindIndex(z => z.ZoneId == zoneId), (byte)oldRealm - 1]--;
                HeldObjectives[(byte)oldRealm - 1]--;
            }*/

            int arr;
            if (Constants.DoomsdaySwitch == 2)
                arr = (int)pairing;
            else
                arr = Tier;
            // Look here before push, it was == 2 before
            if (_held[Zones.FindIndex(z => z.ZoneId == zoneId), 1] + _held[Zones.FindIndex(z => z.ZoneId == zoneId), 2] > 0)
            {
                if (BattleFrontList.ActiveFronts[arr - 1] == null)
                {
                    lock (BattleFrontList.ActiveFronts)
                    {
                        if (BattleFrontList.ActiveFronts[arr - 1] == null)
                            BattleFrontList.ActiveFronts[arr - 1] = this;
                    }

                    if (BattleFrontList.ActiveFronts[arr - 1] == this)
                        EnableSupplies();
                }
            }

            CountRealmObjectives();

            UpdateStateOfTheRealm();

            //CheckZoneLock(newRealm, zoneId, true);
        }

        #endregion

        #region Progression

        // This is fucked
        new void CountRealmObjectives()
        {
            // Spaghetti in progress... This is weird, because I cannot get the correct numer of HeldObjectives from other parts of code

            if (Tier == 4)
            {
                HeldObjectives[0] = 0;
                HeldObjectives[1] = 0;
                HeldObjectives[2] = 0;
                foreach (ProximityFlag flag in _Objectives)
                {
                    if (Zones[_BattleFrontStatus.OpenZoneIndex].ZoneId == flag.ZoneId && flag._state == StateFlags.Secure)
                    {
                        switch (flag.OwningRealm)
                        {
                            case Realms.REALMS_REALM_NEUTRAL:
                                HeldObjectives[0]++;
                                break;

                            case Realms.REALMS_REALM_ORDER:
                                HeldObjectives[1]++;
                                break;

                            case Realms.REALMS_REALM_DESTRUCTION:
                                HeldObjectives[2]++;
                                break;
                        }
                    }
                }
            }
        }

        public override void UpdateVictoryPoints()
        {
            if (Constants.DoomsdaySwitch == 2)
            {
                foreach (ProximityFlag flag in Objectives)
                {
                    if (flag != null && !flag.Ruin)
                        EvtInterface.AddEvent(flag.GrantT2SecureRewards, 6000, 10);
                }
            }

            int time = TCPManager.GetTimeStamp();

            // First we check if this time is a draw time!
            if (PairingDrawTime != 0 && (PairingDrawTime < time || PairingDrawTime - time < 0))
            {
                Random random = new Random();
                Realms realm;

                switch (random.Next(1, 3))
                {
                    case 1:
                        realm = Realms.REALMS_REALM_ORDER;
                        break;
                    case 2:
                        realm = Realms.REALMS_REALM_DESTRUCTION;
                        break;
                    default:
                        realm = Realms.REALMS_REALM_ORDER;
                        break;
                }

                LockZone(realm, Zones[_BattleFrontStatus.OpenZoneIndex].ZoneId, true, false, false, true);
            }

            CountRealmObjectives();

            int delta = (HeldObjectives[1] - HeldObjectives[2]) / 2;

            int maxShift = 50 + (delta * 15);

            if (_Keeps.Count(keep => keep.Realm == Realms.REALMS_REALM_ORDER && keep.KeepStatus != KeepStatus.KEEPSTATUS_LOCKED) == 2)
            {
                maxShift = Math.Min(100, maxShift + 35);
                // If Order controls 2 Keeps we increase Order VPs by another +1%
                delta += 1;
            }

            else if (_Keeps.Count(keep => keep.Realm == Realms.REALMS_REALM_DESTRUCTION && keep.KeepStatus != KeepStatus.KEEPSTATUS_LOCKED) == 2)
            {
                maxShift = Math.Max(0, maxShift - 35);
                // If Destro controls 2 Keeps we increase Destro VPs by another +1%
                delta -= 1;
            }

            // This gives +1% VPs per 60s to Order if Order controls all 4 BOs
            if (HeldObjectives[1] == 4)
                delta += 1;

            // This gives +1% VPs per 60s to Destro if Destro controls all 4 BOs
            if (HeldObjectives[2] == 4)
                delta -= 1;

#if DEBUG
            //delta *= 30;
#endif

            // Reloading siege if holding more than 2 BOs
            foreach (Keep keep in _Keeps)
            {
                float ammo = WorldMgr.WorldSettingsMgr.GetAmmoRefresh() / 10f;
                if (keep.Realm == Realms.REALMS_REALM_ORDER && keep.KeepStatus != KeepStatus.KEEPSTATUS_LOCKED)
                {
                    keep.ProximityReloadSiege((int)(ammo * HeldObjectives[1]));
                }
                if (keep.Realm == Realms.REALMS_REALM_DESTRUCTION && keep.KeepStatus != KeepStatus.KEEPSTATUS_LOCKED)
                {
                    keep.ProximityReloadSiege((int)(ammo * HeldObjectives[2]));
                }
            }

            // Order has overall control
            /*if (delta > 0)
            {
                // If less than max shift, increase VP and check for lock / advancement of message
                if (VictoryPoints < maxShift)
                {
                    VictoryPoints = Math.Min(maxShift, VictoryPoints + delta);

                    if (VictoryPoints == 100)
                        LockZone(Realms.REALMS_REALM_ORDER, Zones[_BattleFrontStatus.OpenZoneIndex].ZoneId, true, false);

                    else if (VictoryPoints - LastAnnouncedVictoryPoints >= 15)
                    {
                        LastAnnouncedVictoryPoints += 15;
                        Broadcast("Order has gained " + LastAnnouncedVictoryPoints + "% control of " + ActiveZoneName + "!", Realms.REALMS_REALM_ORDER);
                    }
                }

                // Decay VP if present value is higher than maximum allowed
                else if (VictoryPoints > maxShift + 15)
                {
                    --VictoryPoints;

                    if (LastAnnouncedVictoryPoints - VictoryPoints >= 15)
                    {
                        LastAnnouncedVictoryPoints -= 15;
                        Broadcast("Destruction has gained " + (100 - LastAnnouncedVictoryPoints) + "% control of " + ActiveZoneName + "!", Realms.REALMS_REALM_DESTRUCTION);
                    }
                }
            }

            // Destruction has overall control
            else if (delta < 0)
            {
                if (VictoryPoints > maxShift)
                {
                    VictoryPoints = Math.Max(maxShift, VictoryPoints + delta);
                    if (VictoryPoints == 0)
                        LockZone(Realms.REALMS_REALM_DESTRUCTION, Zones[_BattleFrontStatus.OpenZoneIndex].ZoneId, true, false);

                    else if (LastAnnouncedVictoryPoints - VictoryPoints >= 15)
                    {
                        LastAnnouncedVictoryPoints -= 15;
                        Broadcast("Destruction has gained " + (100 - LastAnnouncedVictoryPoints) + "% control of " + ActiveZoneName + "!", Realms.REALMS_REALM_DESTRUCTION);
                    }
                }

                // Add Order VP if present value is lower than minimum currently allowed
                else if (VictoryPoints < maxShift - 15)
                {
                    ++VictoryPoints;

                    if (VictoryPoints - LastAnnouncedVictoryPoints >= 15)
                    {
                        LastAnnouncedVictoryPoints += 15;
                        Broadcast("Order has gained " + LastAnnouncedVictoryPoints + "% control of " + ActiveZoneName + "!", Realms.REALMS_REALM_ORDER);
                    }
                }
            }

            // Equal control, tend Victory Points to max shift range
            else
            {
                if (VictoryPoints < maxShift - 15)
                {
                    ++VictoryPoints;

                    if (VictoryPoints - LastAnnouncedVictoryPoints >= 15)
                    {
                        LastAnnouncedVictoryPoints += 15;
                        Broadcast("Order has gained " + LastAnnouncedVictoryPoints + "% control of " + ActiveZoneName + "!", Realms.REALMS_REALM_ORDER);
                    }
                }

                else if (VictoryPoints > maxShift + 15)
                {
                    --VictoryPoints;

                    if (LastAnnouncedVictoryPoints - VictoryPoints >= 15)
                    {
                        LastAnnouncedVictoryPoints -= 15;
                        Broadcast("Destruction has gained " + (100 - LastAnnouncedVictoryPoints) + "% control of " + ActiveZoneName + "!", Realms.REALMS_REALM_DESTRUCTION);
                    }
                }
            }*/
        }

        public int GetZoneOwnership(ushort zoneId)
        {
            const int ZONE_STATUS_CONTESTED = 0;
            const int ZONE_STATUS_ORDER_LOCKED = 1;
            const int ZONE_STATUS_DESTRO_LOCKED = 2;
            // const int ZONE_STATUS_UNLOCKABLE    = 3;

            byte orderKeepsOwned = 0;
            byte destroKeepsOwned = 0;

            foreach (Keep keep in _Keeps)
            {
                if (keep.Realm == Realms.REALMS_REALM_ORDER && keep.Info.ZoneId == zoneId && keep.KeepStatus == KeepStatus.KEEPSTATUS_LOCKED)
                {
                    orderKeepsOwned++;
                }
                else if (keep.Realm == Realms.REALMS_REALM_DESTRUCTION && keep.Info.ZoneId == zoneId && keep.KeepStatus == KeepStatus.KEEPSTATUS_LOCKED)
                {
                    destroKeepsOwned++;
                }
            }

            if (orderKeepsOwned == 2 /*&& _held[Zones.FindIndex(z => z.ZoneId == zoneId), 0] == 4*/)
            {
                return ZONE_STATUS_ORDER_LOCKED;
            }
            if (destroKeepsOwned == 2 /*&& _held[Zones.FindIndex(z => z.ZoneId == zoneId), 1] == 4*/)
            {
                return ZONE_STATUS_DESTRO_LOCKED;
            }
            return ZONE_STATUS_CONTESTED;
        }

        public void CheckZoneLock(Realms realm, int zoneId, bool announce)
        {
            if (realm == Realms.REALMS_REALM_NEUTRAL)
                return;

            if (GetZoneOwnership((ushort)zoneId) == (int)realm)
                LockZone(realm, zoneId, announce, false);
        }

        public void LockZone(Realms realm, int zoneId, bool announce, bool reset, bool noRewards = false, bool draw = false)
        {
            Log.Debug("BattleFront.LockT4Zone", "Locking zone " + zoneId + " for " + realm);

            foreach (ProximityFlag flag in _Objectives.ToList())
            {
                if (flag.Ruin)
                {
                    _Objectives.Remove(flag);
                    flag.RemoveFromWorld();
                    continue;
                }

                if (flag.ZoneId == zoneId)
                { 
                    flag.LockObjective(realm, announce);
                    flag._owningRealm = realm;
                    flag.BroadcastFlagInfo(false);
                }
            }

            if (realm == Realms.REALMS_REALM_ORDER)
            {
                _held[Zones.FindIndex(z => z.ZoneId == zoneId), 0] = 0;
                _held[Zones.FindIndex(z => z.ZoneId == zoneId), 1] = 4;
                _held[Zones.FindIndex(z => z.ZoneId == zoneId), 2] = 0;
                HeldObjectives[0] = 0;
                HeldObjectives[1] = 4;
                HeldObjectives[2] = 0;
            }
            else
            {
                _held[Zones.FindIndex(z => z.ZoneId == zoneId), 0] = 0;
                _held[Zones.FindIndex(z => z.ZoneId == zoneId), 1] = 0;
                _held[Zones.FindIndex(z => z.ZoneId == zoneId), 2] = 4;
                HeldObjectives[0] = 0;
                HeldObjectives[1] = 0;
                HeldObjectives[2] = 4;
            }

            foreach (Keep keep in _Keeps)
            {
                if (keep.Info.ZoneId == zoneId)
                {
                    Realms targetRealm = keep.GetContestedRealm();
                    Dictionary<uint, ContributionInfo> contributors = GetContributorsFromRealm(targetRealm);

                    if (contributors.Count > 0 && !noRewards)
                    {
                        if (DefenderPopTooSmall)
                            WinnerShare = 0.0f;

                        if (draw)
                        {
                            WinnerShare = 0.1f;
                            LoserShare = 0.1f;
                        }

                        Log.Success("Logging keep rewards...", "");
                        Log.Success("Zone", ActiveZoneName);
                        Log.Success("Is defender pop too small to award rewards", DefenderPopTooSmall.ToString());
                        Log.Success("ProgressingBattleFront", $"Creating gold chest for {keep.Info.Name} for {contributors.Count} {(targetRealm == Realms.REALMS_REALM_ORDER ? "Order" : "Destruction")} contributors");
                        GoldChest.Create(Region, keep.Info.PQuest, ref contributors, targetRealm == realm ? WinnerShare : LoserShare);
                    }

                    keep.Realm = realm;
                    keep.LockKeep(realm, announce, reset);
                }
            }

            if (DefenderPopTooSmall)
                Broadcast($"The forces of {(realm == Realms.REALMS_REALM_ORDER ? "Order " : "Destruction ")} conquered abandoned keep, no spoils of war were found!");

            if (draw)
                Broadcast("As forces of Order and Destruction were reluctant to trade final blows the war moved elsewhere!");

            if (!announce)
                return;

            DisableSupplies();

            // Where 0 = Destromost zone, and 2 = Ordermost zone
            int campaignProgress = Zones.FindIndex(z => z.ZoneId == zoneId);

            string message;
            float winnerRewardScale;

            if (realm == Realms.REALMS_REALM_DESTRUCTION)
                campaignProgress = 2 - campaignProgress;

            switch (campaignProgress)
            {
                case 0:
                    // The realm has captured the zone closest to the enemy fort, leave the last zone locked and give them a reward
                    UpdateStateOfTheRealm();
                    PairingLocked = true;

#if DEBUG
                    EvtInterface.AddEvent(EndGrace, 90 * 1000, 1);
#else
                    EvtInterface.AddEvent(EndGrace, 10 * 60 * 1000, 1);
#endif


                    Log.Info("BattleFront.LockT4Zone", "Locked all of region " + Region.RegionId);

                    winnerRewardScale = 1.25f;

                    if (realm == Realms.REALMS_REALM_ORDER)
                        message = Zones[0].Name + " has been liberated by the forces of Order! The forces of Destruction have been cleansed from this region, but the fighting continues on elsewhere!";
                    else
                        message = Zones[2].Name + " has been conquered by the forces of Destruction! The forces of Order flee like cowards, but the fighting continues on elsewhere!";

                    LockingRealm = realm;

                    WorldMgr.EvaluateT4CampaignStatus(Region.RegionId);
                    break;

                case 1:
                    // The realm has captured the middle zone, unlock zone closest to enemy fort.
                    if (Constants.DoomsdaySwitch > 0)
                        //LiftZoneLock(Zones[realm == Realms.REALMS_REALM_ORDER ? 0 : 2].ZoneId, true);
                        CheckUnlockZone(true, Zones[realm == Realms.REALMS_REALM_ORDER ? 0 : 2].ZoneId, true);
                    else
                        //LiftZoneLock(Zones[realm == Realms.REALMS_REALM_ORDER ? 0 : 2].ZoneId, false);
                        CheckUnlockZone(true, Zones[realm == Realms.REALMS_REALM_ORDER ? 0 : 2].ZoneId, false);

                    winnerRewardScale = 1f;

                    if (realm == Realms.REALMS_REALM_ORDER)
                    {
                        message = Zones[1].Name + " has been reclaimed by the forces of Order! The forces of Destruction have retreated to " + Zones[0].Name + ", to build up for a renewed assault.";
                        DefendingRealm = Realms.REALMS_REALM_DESTRUCTION;
                    }
                    else
                    {
                        message = Zones[1].Name + " has been seized by the forces of Destruction! The forces of Order have fallen back to " + Zones[2].Name + " to make a last stand.";
                        DefendingRealm = Realms.REALMS_REALM_ORDER;
                    }

                    UpdateStateOfTheRealm();
                    break;

                case 2:
                    // The realm has recaptured the zone closest to their fort, unlock the middle zone
                    if (Constants.DoomsdaySwitch > 0)
                        CheckUnlockZone(true, Zones[1].ZoneId, true);
                        //LiftZoneLock(Zones[1].ZoneId, true);
                    else
                        CheckUnlockZone(true, Zones[1].ZoneId, false);
                        //LiftZoneLock(Zones[1].ZoneId, false);

                    winnerRewardScale = 0.75f;

                    if (realm == Realms.REALMS_REALM_ORDER)
                        message = Zones[2].Name + " has been saved by the forces of Order! The forces of Destruction fall back to " + Zones[1].Name + "!";
                    else
                        message = "The forces of Destruction tighten their grip on " + Zones[0].Name + ", and the forces of Order fall back to " + Zones[1].Name + "!";

                    DefendingRealm = Realms.REALMS_REALM_NEUTRAL;

                    UpdateStateOfTheRealm();
                    break;

                default:
                    Log.Error("BattleFront.LockT4Zone", "The campaign progress was somehow at stage " + campaignProgress + ". This is not supposed to happen.");
                    return;
            }

            VictoryPoints = 50;
            LastAnnouncedVictoryPoints = 50;

            new ApocCommunications().SendCampaignStatus(null, null);

            winnerRewardScale *= RelativeActivityFactor;

            try
            {
                Log.Info("Zone Lock", ZoneService.GetZone_Info((ushort)zoneId).Name);
                HandleLockReward(realm, winnerRewardScale, message, zoneId);
            }
            catch (Exception e)
            {
                Log.Error("HandleLockReward", "Exception thrown: " + e);
            }

            TotalContribFromRenown = (ulong)(Tier * 50);
            PlayerContributions.Clear();

            PlayerContributions = new Dictionary<uint, ContributionInfo>();

            int arr;
            if (Constants.DoomsdaySwitch == 2)
                arr = (int)pairing;
            else
                arr = Tier;

            if (BattleFrontList.ActiveFronts[arr - 1] == this)
                BattleFrontList.ActiveFronts[arr - 1] = null;

            // This should be 2 to make it work correct with 3 open zones, codeword c4rr0t
            if (Constants.DoomsdaySwitch != 2)
            {
                int i = 0;
                foreach (IBattleFront b in BattleFrontList.BattleFronts[Tier - 1])
                {
                    if (i > 0)
                    {
                        ProximityBattleFront front = b as ProximityBattleFront;
                        if (front != this && !front.PairingLocked)
                            front.EvtInterface.AddEvent(front.SupplyLineReset, 1, 1);
                    }
                    i++;
                }
            }
            /*else
            {
                foreach (ProximityBattleFront b in BattleFrontList.RegionManagers[arr - 1])
                    if (b != this && !b.BattleFrontLocked && b.pairing == pairing)
                        //b.EvtInterface.AddEvent(b.SupplyLineReset, 1, 1);
        }*/
            DefenderPopTooSmall = false;
            _totalMaxOrder = 0;
            _totalMaxDestro = 0;

            PairingDrawTime = 0;
        }

        public void CheckUnlockZone(bool zoneLock = false, int zoneId = 0, bool reset = false)
        {
            int maxOpenZones = 1;
            int currentOpenZones = 0;
            int totalRvRPlayers = Player._Players.Where(p => p.CbtInterface.IsPvp == true && p.Level > 15 && p.ScnInterface.Scenario == null).Count();
            if (totalRvRPlayers < 201)
                maxOpenZones = 1;
            else if (totalRvRPlayers > 200 && totalRvRPlayers < 401)
                maxOpenZones = 2;
            else
                maxOpenZones = 3;

            /*foreach (IBattleFront t4front in BattleFrontList.ActiveFronts)
            {
                if (t4front != null)
                    currentOpenZones++;
            }*/

            for (int i = 0; i < 4; i++)
            {
                foreach (IBattleFront b in BattleFrontList.BattleFronts[i])
                {
                    ProximityBattleFront front = b as ProximityBattleFront;
                    if (front != null && !front.PairingLocked && front.Tier > 1)
                        currentOpenZones++;
                }
            }
            if (zoneLock)
            {
                // Spaghetti...
                currentOpenZones = 0;
                for (int i = 0; i < 4; i++)
                {
                    foreach (IBattleFront b in BattleFrontList.BattleFronts[i])
                    {
                        ProximityProgressingBattleFront front = b as ProximityProgressingBattleFront;
                        if (front != null && !front.PairingLocked && front.Tier > 1 && front != this)
                            currentOpenZones++;
                    }
                }

                if (currentOpenZones == 0 || currentOpenZones < maxOpenZones)
                    LiftZoneLock(zoneId, reset);
                else
                    Broadcast("Not enough warriors to support offensive in new region!");
            }
            else
            {
                if (currentOpenZones < maxOpenZones)
                {
                    int dwarfPairingsOpen = 0; // 1
                    int empirePairingsOpen = 0; // 2
                    int elfPairingsOpen = 0; // 3

                    for (int i = 1; i < 4; i++)
                    {
                        foreach (ProximityBattleFront b in BattleFrontList.BattleFronts[i])
                        {
                            if ((int)b.pairing == 1 && !b.PairingLocked)
                            {
                                dwarfPairingsOpen++;
                            }
                            if ((int)b.pairing == 2 && !b.PairingLocked)
                            {
                                empirePairingsOpen++;
                            }
                            if ((int)b.pairing == 3 && !b.PairingLocked)
                            {
                                elfPairingsOpen++;
                            }
                        }
                    }

                    bool end = false;
                    int newPairing = (int)pairing;

                    while (newPairing == (int)pairing)
                    {
                        newPairing = random.Next(1, 4);
                        while (newPairing == 1 && dwarfPairingsOpen > 0)
                        {
                            newPairing = random.Next(1, 4);
                        }

                        while (newPairing == 2 && empirePairingsOpen > 0)
                        {
                            newPairing = random.Next(1, 4);
                        }

                        while (newPairing == 3 && elfPairingsOpen > 0)
                        {
                            newPairing = random.Next(1, 4);
                        }
                    }

                    for (int i = 0; i < 4; i++)
                    {
                        if (i != (int)pairing - 1)
                        {
                            foreach (IBattleFront b in BattleFrontList.BattleFronts[i])
                            {
                                ProximityBattleFront front = b as ProximityBattleFront;
                                if (front != null && front.PairingLocked && front.Tier == 2 && front.pairing != pairing && (int)front.pairing == newPairing)
                                {
                                    front.LoadMidTierPairing(true);
                                    end = true;
                                    break;
                                }
                            }
                            if (end) break;
                        }
                    }
                }
            }
        }

        public void LiftZoneLock(int zoneId, bool reset)
        {
            long _BOLockTime = (int)(10 * 60 * 1000 * TIMER_MODIFIER);

#if (DEBUG)
            _BOLockTime = (1 * 60 * 1000);
#endif

            PairingLocked = false;
            GraceDisabled = false;

            RealmRank[0] = 0;
            RealmRank[1] = 0;
            RealmCurrentResources[0] = 0;
            RealmCurrentResources[1] = 0;
            RealmMaxResource[0] = 0;
            RealmMaxResource[1] = 0;
            RealmLastReturnSeconds[0] = 0;
            RealmLastReturnSeconds[1] = 0;
            RealmLostKeep[0] = false;
            RealmLostKeep[1] = false;
            RealmDeployedRam[0] = 0;
            RealmDeployedRam[1] = 0;
            RealmCannon[0] = 0;
            RealmCannon[1] = 0;

            PairingDrawTime = 0;
            DefenderPopTooSmall = false;
            _totalMaxOrder = 0;
            _totalMaxDestro = 0;

            _Objectives.Clear();
            LoadObjectives();

            DefendingRealm = Realms.REALMS_REALM_NEUTRAL;

            if (Constants.DoomsdaySwitch > 0)
            {
                _BattleFrontStatus.OpenZoneIndex = Zones.FindIndex(z => z.ZoneId == zoneId);
                _BattleFrontStatus.ActiveRegionOrZone = zoneId;
                WorldMgr.Database.SaveObject(_BattleFrontStatus);
            }
            else
            {
                if (!reset)
                {
                    _BattleFrontStatus.OpenZoneIndex = Zones.FindIndex(z => z.ZoneId == zoneId);
                    _BattleFrontStatus.ActiveRegionOrZone = zoneId;
                    WorldMgr.Database.SaveObject(_BattleFrontStatus);
                }
            }

            _held[Zones.FindIndex(z => z.ZoneId == zoneId), 0] = 4;
            _held[Zones.FindIndex(z => z.ZoneId == zoneId), 1] = 0;
            _held[Zones.FindIndex(z => z.ZoneId == zoneId), 2] = 0;

            HeldObjectives[0] = 4;
            HeldObjectives[1] = 0;
            HeldObjectives[2] = 0;

            foreach (ProximityFlag flag in _Objectives.ToList())
            {
                if (flag.Ruin)
                {
                    _Objectives.Remove(flag);
                    flag.RemoveFromWorld();
                    continue;
                }

                if (flag.ZoneId == zoneId)
                    flag.UnlockObjective();
            }

            Random coinFlip = new Random();
            int flip = coinFlip.Next(1, 3);

            foreach (Keep keep in _Keeps)
                if (keep.Info.ZoneId == zoneId)
                {
                    keep.NotifyPairingUnlocked();

                    if (flip == 2)
                    {
                        if (keep.Realm == Realms.REALMS_REALM_ORDER)
                            keep.Realm = Realms.REALMS_REALM_DESTRUCTION;
                        else
                            keep.Realm = Realms.REALMS_REALM_ORDER;
                    }

                    foreach (KeepDoor door in keep.Doors)
                    {
                        door.GameObject?.SetAttackable(keep.KeepStatus != KeepStatus.KEEPSTATUS_LOCKED && CanAttackDoor(keep.Realm, keep.Info.ZoneId));
                        
                    }

                    keep.LockKeep(keep.Realm, false,false);
                    keep.SendKeepStatus(null);
                }

            // Where 0 = Destromost zone, and 2 = Ordermost zone
            int campaignProgress = Zones.FindIndex(z => z.ZoneId == zoneId);

            lock (Player._Players)
            {
                foreach (Player plr in Player._Players)
                {
                    if (!plr.ValidInTier(Tier, true))
                        continue;

                    plr.SendLocalizeString(Zones[campaignProgress].Name + " battlefield objectives will soon be open for capture!", ChatLogFilters.CHATLOGFILTERS_RVR, Localized_text.CHAT_TAG_DEFAULT);
                    plr.SendLocalizeString(Zones[campaignProgress].Name + " battlefield objectives will soon be open for capture!", plr.Realm == Realms.REALMS_REALM_ORDER ? ChatLogFilters.CHATLOGFILTERS_C_ORDER_RVR_MESSAGE : ChatLogFilters.CHATLOGFILTERS_C_DESTRUCTION_RVR_MESSAGE, Localized_text.CHAT_TAG_DEFAULT);
                }
            }

            new ApocCommunications().SendCampaignStatus(null, null);
        }

        private void LoadPairing()
        {
            Log.Info("BattleFront.LoadPairing", " Region: " + Region.RegionId + " | LOADING CAMPAIGN");

            if (_BattleFrontStatus == null)
            {
                Log.Error("BattleFront.LoadPairing", "No BattleFront Status - campaign resetting.");
                ResetPairing();
                return;
            }

            if (Constants.DoomsdaySwitch > 0)
            {
                PairingLocked = true;
                GraceDisabled = true;
            }
            else
            {
                PairingLocked = false;
                GraceDisabled = false;
            }

            for (int i = 0; i < _BattleFrontStatus.OpenZoneIndex; ++i)
            {
                Log.Info("LoadPairing", "Setting ownership of " + Zones[i].Name + " to Destruction");
                LockZone(Realms.REALMS_REALM_DESTRUCTION, Zones[i].ZoneId, false, true);
                foreach (Keep keep in _Keeps)
                {
                    if (keep.Info.ZoneId == Zones[i].ZoneId)
                        keep.Realm = Realms.REALMS_REALM_DESTRUCTION;
                }
            }


            if (Constants.DoomsdaySwitch > 0)
                LockZone(Realms.REALMS_REALM_NEUTRAL, Zones[_BattleFrontStatus.OpenZoneIndex].ZoneId, false, true);
            else
            {
                LiftZoneLock(Zones[_BattleFrontStatus.OpenZoneIndex].ZoneId, true);
                Log.Info("LoadPairing", "Setting ownership of " + Zones[_BattleFrontStatus.OpenZoneIndex].Name + " to Contested");
            }



            for (int i = _BattleFrontStatus.OpenZoneIndex + 1; i < 3; ++i)
            {
                LockZone(Realms.REALMS_REALM_ORDER, Zones[i].ZoneId, false, true);
                Log.Info("LoadPairing", "Setting ownership of " + Zones[i].Name + " to Order");
                foreach (Keep keep in _Keeps)
                {
                    if (keep.Info.ZoneId == Zones[i].ZoneId)
                        keep.Realm = Realms.REALMS_REALM_ORDER;
                }
            }

            LockingRealm = Realms.REALMS_REALM_NEUTRAL;
            DefendingRealm = Realms.REALMS_REALM_NEUTRAL;
            new ApocCommunications().SendCampaignStatus(null, null);
        }

        public override void ResetPairing()
        {
            Log.Info("BattleFront.ResetT4Campaign", " Region: " + Region.RegionId + " | RESETTING CAMPAIGN");
            //Log.Info("LockTimer: ", "Timer was " + PairingDrawTime.ToString() + " and its now " + TCPManager.GetTimeStamp().ToString() + " ProximityProgressingBattleFront");
            PairingLocked = false;
            GraceDisabled = false;

            RealmRank[0] = 0;
            RealmRank[1] = 0;
            RealmCurrentResources[0] = 0;
            RealmCurrentResources[1] = 0;
            RealmMaxResource[0] = 0;
            RealmMaxResource[1] = 0;
            RealmLastReturnSeconds[0] = 0;
            RealmLastReturnSeconds[1] = 0;
            RealmLostKeep[0] = false;
            RealmLostKeep[1] = false;
            RealmDeployedRam[0] = 0;
            RealmDeployedRam[1] = 0;
            RealmCannon[0] = 0;
            RealmCannon[1] = 0;

            PairingDrawTime = 0;
            DefenderPopTooSmall = false;
            _totalMaxOrder = 0;
            _totalMaxDestro = 0;

            foreach (ProximityFlag flag in _Objectives.ToList())
            {
                if (flag != null && flag.Ruin)
                {
                    _Objectives.Remove(flag);
                    flag.RemoveFromWorld();
                }
            }

            LockZone(Realms.REALMS_REALM_DESTRUCTION, Zones[0].ZoneId, false, true);
            LiftZoneLock(Zones[1].ZoneId, true);
            LockZone(Realms.REALMS_REALM_ORDER, Zones[2].ZoneId, false, true);
            LockingRealm = Realms.REALMS_REALM_NEUTRAL;
            DefendingRealm = Realms.REALMS_REALM_NEUTRAL;
            new ApocCommunications().SendCampaignStatus(null, null);

            if (_BattleFrontStatus != null)
            {
                _BattleFrontStatus.OpenZoneIndex = 1;
                WorldMgr.Database.SaveObject(_BattleFrontStatus);
            }
        }

        public override void SupplyLineReset()
        {
            foreach (ProximityFlag flag in _Objectives.ToList())
            {
                if (flag != null && flag.Ruin)
                {
                    _Objectives.Remove(flag);
                    flag.RemoveFromWorld();
                }
            }

            foreach (var obj in _Objectives)
            {
                if (obj.FlagState != ObjectiveFlags.ZoneLocked)
                    obj.UnlockObjective();
            }

            HeldObjectives[0] = 4;
            HeldObjectives[1] = 0;
            HeldObjectives[2] = 0;

            _held[_BattleFrontStatus.OpenZoneIndex, 0] = 4;
            _held[_BattleFrontStatus.OpenZoneIndex, 1] = 0;
            _held[_BattleFrontStatus.OpenZoneIndex, 2] = 0;
        }

        #endregion

        // This is place in code where Campaign starts in a pairing - Hargrim
        public override void EnableSupplies()
        {
            if (!NoSupplies)
                return;

            CountRealmObjectives();

            if (HeldObjectives[1] + HeldObjectives[2] < 4)
            {
                Keep orderKeep = _Keeps.First(keep => keep.Realm == Realms.REALMS_REALM_ORDER && keep.Info.ZoneId == Zones[_BattleFrontStatus.OpenZoneIndex].ZoneId);

                if (orderKeep == null)
                {
                    Log.Error("ProgressingBattleFront", "Unable to find an Order keep?");
                    return;
                }

                Keep destroKeep = _Keeps.First(keep => keep.Realm == Realms.REALMS_REALM_DESTRUCTION && keep.Info.ZoneId == Zones[_BattleFrontStatus.OpenZoneIndex].ZoneId);

                if (destroKeep == null)
                {
                    Log.Error("ProgressingBattleFront", "Unable to find a Destruction keep?");
                    return;
                }

                if (Constants.DoomsdaySwitch != 2)
                {
                    while (HeldObjectives[0] < 2)
                    {
                        ProximityFlag flag = GetClosestNeutralFlagTo(orderKeep.WorldPosition);
#if DEBUG
                        flag.UnlockObjective();
#else
                    flag.UnlockObjective();
#endif
                    }


                    while (HeldObjectives[1] < 2)
                    {
                        ProximityFlag flag = GetClosestNeutralFlagTo(destroKeep.WorldPosition);
#if DEBUG
                        flag.UnlockObjective();
#else
                    flag.UnlockObjective();
#endif
                    }
                }
            }

            _NoSupplies = !_NoSupplies;

            if (Constants.DoomsdaySwitch != 2)
            {
                foreach (var objective in _Objectives)
                {
                    if (objective.FlagState == ObjectiveFlags.Open || objective.FlagState == ObjectiveFlags.Locked)
                    {
                        objective.UnlockObjective();
                        objective.StartSupplyRespawnTimer(SupplyEvent.ZoneActiveStatusChanged);
                    }
                }
            }
            else
            {
                foreach (var objective in _Objectives)
                    objective.ActivatePortals();
            }


            string message = "The forces of Order and Destruction direct their supply lines towards " + Zones[_BattleFrontStatus.OpenZoneIndex].Name + "!";

            Log.Info("ProgressingBattleFront", message);

            lock (Player._Players)
            {
                foreach (Player player in Player._Players)
                {
                    if (player.ValidInTier(Tier, true))
                    {
                        player.SendClientMessage(message, player.Realm == Realms.REALMS_REALM_ORDER ? ChatLogFilters.CHATLOGFILTERS_C_ORDER_RVR_MESSAGE : ChatLogFilters.CHATLOGFILTERS_C_DESTRUCTION_RVR_MESSAGE);
                        player.SendClientMessage(message, ChatLogFilters.CHATLOGFILTERS_RVR);
                    }
                }
            }

            ActiveSupplyLine = 1;

            if (PairingDrawTime == 0)
                PairingDrawTime = TCPManager.GetTimeStamp() + 14400;

            //Log.Info("LockTimer: ", "set to: " + PairingDrawTime.ToString() + " on zone " + Zones[_BattleFrontStatus.OpenZoneIndex].Name);
        }

        public override void DisableSupplies()
        {
            if (NoSupplies)
                return;

            _NoSupplies = true;

            //foreach (var objective in _Objectives)
            //  objective.BlockSupplySpawn();

            string message = "The forces of Order and Destruction have pulled their supply lines out of " + Zones[_BattleFrontStatus.OpenZoneIndex].Name + "!";

            lock (Player._Players)
            {
                foreach (Player player in Player._Players)
                {
                    if (player.ValidInTier(Tier, true))
                    {
                        player.SendClientMessage(message, player.Realm == Realms.REALMS_REALM_ORDER ? ChatLogFilters.CHATLOGFILTERS_C_ORDER_RVR_MESSAGE : ChatLogFilters.CHATLOGFILTERS_C_DESTRUCTION_RVR_MESSAGE);
                        player.SendClientMessage(message, ChatLogFilters.CHATLOGFILTERS_RVR);
                    }
                }
            }

            ActiveSupplyLine = 0;

            RemoveSiege();
        }

        public override void WriteBattleFrontStatus(PacketOut Out)
        {
            Out.WriteByte((byte)GetZoneOwnership(Zones[2].ZoneId));
            Out.WriteByte((byte)GetZoneOwnership(Zones[1].ZoneId));
            Out.WriteByte((byte)GetZoneOwnership(Zones[0].ZoneId));
        }

        public override void CampaignDiagnostic(Player plr, bool localZone)
        {
            plr.SendClientMessage("***** Campaign Status : Region " + Region.RegionId + " *****", ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);

            // Discard any regions not part of the T4 campaign
            if (Region.RegionId == 2 || Region.RegionId == 4 || Region.RegionId == 11)
            {
                plr.SendClientMessage("The pairing is " + (PairingLocked ? "locked for " + ((PairingUnlockTime - TCPManager.GetTimeStampMS()) / 60000) + " more minutes." : "contested."));

                plr.SendClientMessage("Ration factors:  Order " + RationFactor[0] + ", Destruction " + RationFactor[1]);

                for (int count = 0; count < Zones.Count; count++)
                {
                    if (localZone && Zones[count].ZoneId != plr.Zone.ZoneId)
                        continue;

                    plr.SendClientMessage(Zones[count].Name + " is owned by " + GetZoneOwnership(Zones[count].ZoneId) + " (0: Contested | 1: Order | 2: Destro)", ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);
                    plr.SendClientMessage("BOs Held: " + "[ORDER: " + _held[count, 1] + ", DESTRO: " + _held[count, 2] + "]");

                    foreach (Keep k in _Keeps)
                        if (k.Info.ZoneId == Zones[count].ZoneId)
                            k.SendDiagnostic(plr);

                    plr.SendClientMessage("", ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);

                    foreach (ProximityFlag flag in _Objectives)
                    {
                        if (flag != null && flag.ZoneId == Zones[count].ZoneId)
                            flag.SendDiagnostic(plr);
                    }

                    plr.SendClientMessage("---------", ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);
                }

                if (Constants.DoomsdaySwitch == 2)
                    plr.SendClientMessage("Currently active BattleFront: " + (BattleFrontList.ActiveFronts[(int)pairing - 1] != null ? ((ProximityProgressingBattleFront)BattleFrontList.ActiveFronts[(int)pairing - 1]).Zones[0].Name : "None"));
                else
                    plr.SendClientMessage("Currently active BattleFront: " + (BattleFrontList.ActiveFronts[Tier - 1] != null ? ((ProximityProgressingBattleFront)BattleFrontList.ActiveFronts[Tier - 1]).Zones[0].Name : "None"));
            }
            else
                plr.SendClientMessage("Region " + Region.RegionId + " is not part of the Tier 4 Campaign.", ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);

            plr.SendClientMessage("GraceDisabled: " + GraceDisabled, ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);
            plr.SendClientMessage("DefenderPopToSmall: " + DefenderPopTooSmall, ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);
            plr.SendClientMessage("_totalMaxOrder: " + _totalMaxOrder, ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);
            plr.SendClientMessage("_totalMaxDestro: " + _totalMaxDestro, ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);
        }
    }
}
