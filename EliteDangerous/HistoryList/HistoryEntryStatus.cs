﻿/*
 * Copyright © 2016 - 2018 EDDiscovery development team
 *
 * Licensed under the Apache License, Version 2.0 (the "License"); you may not use this
 * file except in compliance with the License. You may obtain a copy of the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software distributed under
 * the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF
 * ANY KIND, either express or implied. See the License for the specific language
 * governing permissions and limitations under the License.
 * 
 * EDDiscovery is not affiliated with Frontier Developments plc.
 */

using System;
using EliteDangerousCore.JournalEvents;

namespace EliteDangerousCore
{
    public class HistoryEntryStatus
    {
        public enum TravelStateType { Docked, Landed, Hyperspace, NormalSpace, Unknown };       // simplifies and stops errors by having an enum

        public string BodyName { get; private set; }
        public int? BodyID { get; private set; }
        public string BodyType { get; private set; }
        public string StationName { get; private set; }
        public string StationType { get; private set; }
        public long? MarketId { get; private set; }
        public TravelStateType TravelState { get; private set; } = TravelStateType.Unknown;  // travel state
        public int ShipID { get; private set; } = -1;
        public string ShipType { get; private set; } = "Unknown";         // and the ship
        public string OnCrewWithCaptain { get; private set; } = null;     // if not null, your in another multiplayer ship
        public string GameMode { get; private set; } = "Unknown";         // game mode, from LoadGame event
        public string Group { get; private set; } = "";                   // group..
        public bool Wanted { get; private set; } = false;
        public bool BodyApproached { get; private set; } = false;           // set at approach body, cleared at leave body or fsd jump

        private HistoryEntryStatus()
        {
        }

        public HistoryEntryStatus(HistoryEntryStatus prevstatus)
        {
            this.BodyName = prevstatus.BodyName;
            this.BodyID = prevstatus.BodyID;
            this.BodyType = prevstatus.BodyType;
            this.StationName = prevstatus.StationName;
            this.StationType = prevstatus.StationType;
            this.MarketId = prevstatus.MarketId;
            this.TravelState = prevstatus.TravelState;
            this.ShipID = prevstatus.ShipID;
            this.ShipType = prevstatus.ShipType;
            this.OnCrewWithCaptain = prevstatus.OnCrewWithCaptain;
            this.GameMode = prevstatus.GameMode;
            this.Group = prevstatus.Group;
            this.Wanted = prevstatus.Wanted;
            this.BodyApproached = prevstatus.BodyApproached;
        }

        public static HistoryEntryStatus Update(HistoryEntryStatus prev, JournalEntry je, string curStarSystem)
        {
            if (prev == null)
            {
                prev = new HistoryEntryStatus();
            }

            switch (je.EventTypeID)
            {
                case JournalTypeEnum.Location:
                    JournalLocation jloc = je as JournalLocation;
                    TravelStateType t = jloc.Docked ? TravelStateType.Docked : (jloc.Latitude.HasValue ? TravelStateType.Landed : TravelStateType.NormalSpace);

                    return new HistoryEntryStatus(prev)     // Bodyapproach copy over we should be in the same state as last..
                    {
                        TravelState = t,
                        MarketId = jloc.MarketID,
                        BodyID = jloc.BodyID,
                        BodyType = jloc.BodyType,
                        BodyName = jloc.Body,
                        Wanted = jloc.Wanted,
                        StationName = jloc.StationName.Alt(null),       // if empty string, set to null
                        StationType = jloc.StationType.Alt(null),
                    };
                case JournalTypeEnum.FSDJump:
                    JournalFSDJump jfsd = (je as JournalFSDJump);
                    return new HistoryEntryStatus(prev)
                    {
                        TravelState = TravelStateType.Hyperspace,
                        MarketId = null,
                        BodyID = -1,
                        BodyType = "Star",
                        BodyName = jfsd.StarSystem,
                        Wanted = jfsd.Wanted,
                        StationName = null,
                        StationType = null,
                        BodyApproached = false,
                    };
                case JournalTypeEnum.LoadGame:
                    JournalLoadGame jlg = je as JournalLoadGame;
                    bool isbuggy = ShipModuleData.IsSRV(jlg.ShipFD);
                    string shiptype = isbuggy ? prev.ShipType : (je as JournalLoadGame).Ship;
                    int shipid = isbuggy ? prev.ShipID : (je as JournalLoadGame).ShipId;

                    return new HistoryEntryStatus(prev) // Bodyapproach copy over we should be in the same state as last..
                    {
                        OnCrewWithCaptain = null,    // can't be in a crew at this point
                        GameMode = jlg.GameMode,      // set game mode
                        Group = jlg.Group,            // and group, may be empty
                        TravelState = (jlg.StartLanded || isbuggy) ? TravelStateType.Landed : prev.TravelState,
                        ShipType = shiptype,
                        ShipID = shipid,
                    };
                case JournalTypeEnum.Docked:
                    JournalDocked jdocked = (JournalDocked)je;
                    return new HistoryEntryStatus(prev)
                    {
                        TravelState = TravelStateType.Docked,
                        MarketId = jdocked.MarketID,
                        Wanted = jdocked.Wanted,
                        StationName = jdocked.StationName,
                        StationType = jdocked.StationType,
                    };
                case JournalTypeEnum.Undocked:
                    return new HistoryEntryStatus(prev)
                    {
                        TravelState = TravelStateType.NormalSpace,
                        MarketId = null,
                        StationName = null,
                        StationType = null,
                    };
                case JournalTypeEnum.Touchdown:
                    if (((JournalTouchdown)je).PlayerControlled == true)        // can get this when not player controlled
                    {
                        return new HistoryEntryStatus(prev)
                        {
                            TravelState = TravelStateType.Landed,
                        };
                    }
                    else
                        return prev;

                case JournalTypeEnum.Liftoff:
                    if (((JournalLiftoff)je).PlayerControlled == true)         // can get this when not player controlled
                    {
                        return new HistoryEntryStatus(prev)
                        {
                            TravelState = TravelStateType.NormalSpace,
                        };
                    }
                    else
                        return prev;

                case JournalTypeEnum.SupercruiseExit:
                    JournalSupercruiseExit jsexit = (JournalSupercruiseExit)je;
                    return new HistoryEntryStatus(prev)
                    {
                        TravelState = TravelStateType.NormalSpace,
                        BodyName = (prev.BodyApproached) ? prev.BodyName : jsexit.Body,
                        BodyType = (prev.BodyApproached) ? prev.BodyType : jsexit.BodyType,
                        BodyID = (prev.BodyApproached) ? prev.BodyID : jsexit.BodyID,
                    };
                case JournalTypeEnum.SupercruiseEntry:
                    return new HistoryEntryStatus(prev)
                    {
                        TravelState = TravelStateType.Hyperspace,
                        BodyName = !prev.BodyApproached ? curStarSystem : prev.BodyName,
                        BodyType = !prev.BodyApproached ? "Star" : prev.BodyType,
                        BodyID = !prev.BodyApproached ? -1 : prev.BodyID,
                    };
                case JournalTypeEnum.ApproachBody:
                    JournalApproachBody jappbody = (JournalApproachBody)je;
                    return new HistoryEntryStatus(prev)
                    {
                        BodyApproached = true,
                        BodyType = jappbody.BodyType,
                        BodyName = jappbody.Body,
                        BodyID = jappbody.BodyID,
                    };
                case JournalTypeEnum.LeaveBody:
                    JournalLeaveBody jlbody = (JournalLeaveBody)je;
                    return new HistoryEntryStatus(prev)
                    {
                        BodyApproached = false,
                        BodyType = "Star",
                        BodyName = curStarSystem,
                        BodyID = -1,
                    };
                case JournalTypeEnum.StartJump:
                    if (prev.TravelState != TravelStateType.Hyperspace) // checking we are into hyperspace, we could already be if in a series of jumps
                    {
                        return new HistoryEntryStatus(prev)
                        {
                            TravelState = TravelStateType.Hyperspace,
                        };
                    }
                    else
                        return prev;

                case JournalTypeEnum.ShipyardBuy:
                    return new HistoryEntryStatus(prev)
                    {
                        ShipID = -1,
                        ShipType = ((JournalShipyardBuy)je).ShipType  // BUY does not have ship id, but the new entry will that is written later - journals 8.34
                    };
                case JournalTypeEnum.ShipyardNew:
                    JournalShipyardNew jsnew = (JournalShipyardNew)je;
                    return new HistoryEntryStatus(prev)
                    {
                        ShipID = jsnew.ShipId,
                        ShipType = jsnew.ShipType
                    };
                case JournalTypeEnum.ShipyardSwap:
                    JournalShipyardSwap jsswap = (JournalShipyardSwap)je;
                    return new HistoryEntryStatus(prev)
                    {
                        ShipID = jsswap.ShipId,
                        ShipType = jsswap.ShipType
                    };
                case JournalTypeEnum.JoinACrew:
                    return new HistoryEntryStatus(prev)
                    {
                        OnCrewWithCaptain = ((JournalJoinACrew)je).Captain
                    };
                case JournalTypeEnum.QuitACrew:
                    return new HistoryEntryStatus(prev)
                    {
                        OnCrewWithCaptain = null
                    };
                case JournalTypeEnum.Died:
                    return new HistoryEntryStatus(prev)
                    {
                        BodyName = "Unknown",
                        BodyID = -1,
                        BodyType = "Unknown",
                        StationName = "Unknown",
                        StationType = "Unknown",
                        MarketId = null,
                        TravelState = TravelStateType.Docked,
                        OnCrewWithCaptain = null,
                        BodyApproached = false,     // we have to clear this, we can't tell if we are going back to another place..
                    };
                case JournalTypeEnum.Loadout:
                    var jloadout = (JournalLoadout)je;
                    if (!ShipModuleData.IsSRV(jloadout.ShipFD))     // just double checking!
                    {
                        return new HistoryEntryStatus(prev)
                        {
                            ShipID = jloadout.ShipId,
                            ShipType = jloadout.Ship,
                        };
                    }
                    else
                        return prev;

                default:
                    return prev;
            }
        }
    }

}