/**
 *   Copyright (C) 2021 okaygo
 *
 *   https://github.com/misterokaygo/MapAssist/
 *
 *  This program is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.
 *
 *  This program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License
 *  along with this program.  If not, see <https://www.gnu.org/licenses/>.
 **/

using System;
using System.Collections.Generic;
using System.Text;
using MapAssist.Types;

namespace MapAssist.Helpers
{
    public partial  class GameManager
    {
        private uint _lastMapSeed = 0;

        public GameData GetGameData()
        {
            try
            {
                using (var processContext = GetProcessContext())
                {
                    if (processContext == null)
                    {
                        return null;
                    }

                    var playerUnit = PlayerUnit;

                    if (!Equals(playerUnit, default(UnitAny)))
                    {
                        playerUnit = playerUnit.Update();
                    }
                        
                    if (!Equals(playerUnit, default(UnitAny)))
                    {
                        var mapSeed = playerUnit.Act.MapSeed;

                        if (mapSeed <= 0 || mapSeed > 0xFFFFFFFF)
                        {
                            throw new Exception("Map seed is out of bounds.");
                        }
                        
                        if (mapSeed != _lastMapSeed)
                        {
                            _lastMapSeed = mapSeed;
                            //dispose leftover timers in this process if we started a new game
                            if(Items.ItemLogTimers.TryGetValue(_currentProcessId, out var _))
                            {
                                foreach (var timer in Items.ItemLogTimers[_currentProcessId])
                                {
                                    timer.Dispose();
                                }
                            }
                            if (!Items.ItemUnitHashesSeen.TryGetValue(_currentProcessId, out var _))
                            {
                                Items.ItemUnitHashesSeen.Add(_currentProcessId, new HashSet<string>());
                                Items.ItemUnitIdsSeen.Add(_currentProcessId, new HashSet<uint>());
                                Items.ItemLog.Add(_currentProcessId, new List<UnitAny>());
                            }
                            else
                            {
                                Items.ItemUnitHashesSeen[_currentProcessId].Clear();
                                Items.ItemUnitIdsSeen[_currentProcessId].Clear();
                                Items.ItemLog[_currentProcessId].Clear();
                            }
                        }

                        var session = new Session(this, GameIPOffset);

                        var menuOpen = processContext.Read<byte>(MenuOpenOffset);
                        var menuData = processContext.Read<Structs.MenuData>(MenuDataOffset);

                        var actId = playerUnit.Act.ActId;

                        var gameDifficulty = playerUnit.Act.ActMisc.GameDifficulty;

                        if (!gameDifficulty.IsValid())
                        {
                            throw new Exception("Game difficulty out of bounds.");
                        }

                        var levelId = playerUnit.Path.Room.RoomEx.Level.LevelId;

                        if (!levelId.IsValid())
                        {
                            throw new Exception("Level id out of bounds.");
                        }

                        var monsterList = new HashSet<UnitAny>();
                        var itemList = new HashSet<UnitAny>();
                        var objectList = new HashSet<UnitAny>();
                        var playerList = new Dictionary<uint, UnitAny>();
                        GetUnits(ref monsterList, ref itemList, ref playerList, ref objectList);
                        Items.CurrentItemLog = Items.ItemLog[_currentProcessId];

                        var roster = new Roster(this, RosterDataOffset);

                        return new GameData
                        {
                            PlayerPosition = playerUnit.Position,
                            MapSeed = mapSeed,
                            Area = levelId,
                            Difficulty = gameDifficulty,
                            PlayerName = playerUnit.Name,
                            Monsters = monsterList,
                            Items = itemList,
                            Objects = objectList,
                            Players = playerList,
                            Session = session,
                            Roster = roster,
                            PlayerUnit = playerUnit,
                            MenuOpen = menuData,
                            MenuPanelOpen = menuOpen
                        };
                    }
                }
            }
            catch (Exception exception)
            {
                if (exception.Message == "Game process not found.")
                {
                    _log.Debug(exception);
                }
                else
                {
                    _log.Error(exception);
                }
            }

            ResetPlayerUnit();
            return null;
        }

        private void GetUnits(ref HashSet<UnitAny> monsterList, ref HashSet<UnitAny> itemList, ref Dictionary<uint, UnitAny> playerList, ref HashSet<UnitAny> objectList)
        {
            for (var i = 0; i <= 4; i++)
            {
                var unitType = (UnitType)i;
                var unitHashTable = new Structs.UnitHashTable();
                if(unitType == UnitType.Missile)
                {
                    //missiles are contained in a different table
                    unitHashTable = UnitHashTable(128 * 8 * (i + 6));
                } else
                {
                    unitHashTable = UnitHashTable(128 * 8 * i);
                }
                foreach (var pUnitAny in unitHashTable.UnitTable)
                {
                    var unitAny = new UnitAny(this, pUnitAny);
                    while (unitAny.IsValidUnit())
                    {
                        switch (unitType)
                        {
                            case UnitType.Monster:
                                if (!monsterList.Contains(unitAny) && unitAny.IsMonster())
                                {
                                    monsterList.Add(unitAny);
                                }
                                break;
                            case UnitType.Item:
                                if (!itemList.Contains(unitAny))
                                {
                                    itemList.Add(unitAny);
                                }
                                break;
                            case UnitType.Object:
                                if (!objectList.Contains(unitAny))
                                {
                                    objectList.Add(unitAny);
                                }
                                break;
                            case UnitType.Player:
                                if (!playerList.TryGetValue(unitAny.UnitId, out var _))
                                {
                                    playerList.Add(unitAny.UnitId, unitAny);
                                }
                                break;
                        }
                        unitAny = unitAny.ListNext;
                    }
                }
            }
        }

        private void GetUnits(HashSet<Room> rooms, ref List<UnitAny> monsterList, ref List<UnitAny> itemList)
        {
            foreach (var room in rooms)
            {
                var unitAny = room.UnitFirst;
                while (unitAny.IsValidUnit())
                {
                    switch (unitAny.UnitType)
                    {
                        case UnitType.Monster:
                            if (!monsterList.Contains(unitAny) && unitAny.IsMonster())
                            {
                                monsterList.Add(unitAny);
                            }

                            break;
                        case UnitType.Item:
                            if (!itemList.Contains(unitAny))
                            {
                                itemList.Add(unitAny);
                            }
                            break;
                    }

                    unitAny = unitAny.RoomNext;
                }
            }
        }

        private HashSet<Room> GetRooms(Room startingRoom, ref HashSet<Room> roomsList)
        {
            var roomsNear = startingRoom.RoomsNear;
            foreach (var roomNear in roomsNear)
            {
                if (!roomsList.Contains(roomNear))
                {
                    roomsList.Add(roomNear);
                    GetRooms(roomNear, ref roomsList);
                }
            }

            if (!roomsList.Contains(startingRoom.RoomNextFast))
            {
                roomsList.Add(startingRoom.RoomNextFast);
                GetRooms(startingRoom.RoomNextFast, ref roomsList);
            }

            return roomsList;
        }
    }
}
