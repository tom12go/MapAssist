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
using System.Diagnostics;
using System.Text;
using MapAssist.Structs;

namespace MapAssist.Helpers
{
    public partial class GameManager
    {
        private static readonly NLog.Logger _log = NLog.LogManager.GetCurrentClassLogger();
        private static readonly string ProcessName = Encoding.UTF8.GetString(new byte[] { 68, 50, 82 });

        private Process _gameProcess;
        private ProcessContext _processContext;
        private int _currentProcessId;

        private Types.UnitAny _PlayerUnit = default;
        private IntPtr _UnitHashTableOffset;
        private IntPtr _ExpansionCheckOffset;
        private IntPtr _GameIPOffset;
        private IntPtr _MenuPanelOpenOffset;
        private IntPtr _MenuDataOffset;
        private IntPtr _RosterDataOffset;

        private bool _playerNotFoundErrorThrown = false;

        public Types.UnitAny CurrentPlayerUnit { get { return _PlayerUnit; } }

        public static GameManager AttachManagerToProcess(int pid)
        {
            Process process;
            try // The process can end before this block is done, hence wrap it in a try catch
            {
                process = Process.GetProcessById(pid); // If closing another non-foreground window, Process.GetProcessById can fail
                if (process.ProcessName != ProcessName) // Not a valid game process
                {
                    _log.Info($"Process is not a game (pid: {pid})");
                    return null;
                }
            }
            catch
            {
                _log.Info($"Process now closed (pid: {pid})");
                return null;
            }

            // is a new game process
            _log.Info($"Attach to game process (pid: {pid})");


            var manager = new GameManager() { _currentProcessId = pid, _gameProcess = process };

            manager.ResetPlayerUnit();

            return manager;
        }


        public ProcessContext GetProcessContext()
        {
            if (_processContext != null && _processContext.OpenContextCount > 0)
            {
                _processContext.OpenContextCount += 1;
                return _processContext;
            }
            else if (_gameProcess != null)
            {
                try
                {
                    _processContext = new ProcessContext(_gameProcess); // Rarely, the VirtualMemoryRead will cause an error, in that case return a null instead of a runtime error. The next frame will try again.
                    return _processContext;
                }
                catch(Exception ex)
                {
                    return null;
                }
            }

            return null;
        }


        public Types.UnitAny PlayerUnit
        {
            get
            {
                if (Equals(_PlayerUnit, default(Types.UnitAny)))
                {
                    foreach (var pUnitAny in UnitHashTable().UnitTable)
                    {
                        var unitAny = new Types.UnitAny(this, pUnitAny);

                        while (unitAny.IsValidUnit())
                        {
                            if (unitAny.IsPlayerUnit())
                            {
                                _playerNotFoundErrorThrown = false;
                                _PlayerUnit = unitAny;
                                return _PlayerUnit;
                            }

                            unitAny = unitAny.ListNext;
                        }
                    }
                }
                else
                {
                    _playerNotFoundErrorThrown = false;
                    return _PlayerUnit;
                }

                if (!_playerNotFoundErrorThrown)
                {
                    _playerNotFoundErrorThrown = true;
                    throw new Exception("Player unit not found.");
                }
                else
                {
                    return default(Types.UnitAny);
                }
            }
        }

        public UnitHashTable UnitHashTable(int offset = 0)
        {
            using (var processContext = GetProcessContext())
            {
                if (_UnitHashTableOffset == IntPtr.Zero)
                {

                    _UnitHashTableOffset = processContext.GetUnitHashtableOffset();
                }

                return processContext.Read<UnitHashTable>(IntPtr.Add(_UnitHashTableOffset, offset));
            }
        }

        public IntPtr ExpansionCheckOffset
        {
            get
            {
                if (_ExpansionCheckOffset != IntPtr.Zero)
                {
                    return _ExpansionCheckOffset;
                }

                using (var processContext = GetProcessContext())
                {
                    _ExpansionCheckOffset = processContext.GetExpansionOffset();
                }

                return _ExpansionCheckOffset;
            }
        }
        public IntPtr GameIPOffset
        {
            get
            {
                if (_GameIPOffset != IntPtr.Zero)
                {
                    return _GameIPOffset;
                }

                using (var processContext = GetProcessContext())
                {
                    _GameIPOffset = (IntPtr)processContext.GetGameIPOffset();

                }

                return _GameIPOffset;
            }
        }
        public IntPtr MenuOpenOffset
        {
            get
            {
                if (_MenuPanelOpenOffset != IntPtr.Zero)
                {
                    return _MenuPanelOpenOffset;
                }

                using (var processContext = GetProcessContext())
                {
                    _MenuPanelOpenOffset = (IntPtr)processContext.GetMenuOpenOffset();
                }

                return _MenuPanelOpenOffset;
            }
        }
        public IntPtr MenuDataOffset
        {
            get
            {
                if (_MenuDataOffset != IntPtr.Zero)
                {
                    return _MenuDataOffset;
                }

                using (var processContext = GetProcessContext())
                {
                    _MenuDataOffset = (IntPtr)processContext.GetMenuDataOffset();
                }

                return _MenuDataOffset;
            }
        }
        public IntPtr RosterDataOffset
        {
            get
            {
                if (_RosterDataOffset != IntPtr.Zero)
                {
                    return _RosterDataOffset;
                }

                using (var processContext = GetProcessContext())
                {
                    _RosterDataOffset = processContext.GetRosterDataOffset();
                }

                return _RosterDataOffset;
            }
        }

        public void ResetPlayerUnit()
        {
            _PlayerUnit = default;
        }
        
        public void Dispose()
        {
            if (_gameProcess != null)
            {
                _gameProcess.Dispose();
            }
        }
    }
}
