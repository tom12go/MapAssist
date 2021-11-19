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

using GameOverlay.Drawing;
using GameOverlay.Windows;
using Gma.System.MouseKeyHook;
using MapAssist.Helpers;
using MapAssist.Settings;
using MapAssist.Types;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Numerics;
using System.Threading;

namespace MapAssist
{
    public class Overlay : IDisposable
    {
        private readonly GraphicsWindow _window;

        private System.Windows.Forms.NotifyIcon _trayIcon;

        private Timer _timer;
        private GameData _currentGameData;
        private Compositor _compositor;
        private AreaData _areaData;
        private MapApi _mapApi;
        private bool _show = true;
        private int _isBusy = 0;

        public Overlay(IKeyboardMouseEvents keyboardMouseEvents)
        {
            var gfx = new Graphics();

            _window = new GraphicsWindow(0, 0, 1, 1)
            {
                FPS = 60,
                IsTopmost = true,
                IsVisible = false
            };

            _window.DrawGraphics += _window_DrawGraphics;
            _window.SetupGraphics += _window_SetupGraphics;

            keyboardMouseEvents.KeyPress += (_, args) =>
            {
                if (InGame())
                {
                    if (args.KeyChar == Map.ToggleKey)
                    {
                        _show = !_show;
                    }
                    if (args.KeyChar == Map.ZoomInKey)
                    {
                        if (Map.ZoomLevel > 0.25f)
                        {
                            Map.ZoomLevel -= 0.25f;
                            Map.Size = (int)(Map.Size * 1.15f);
                        }
                    }
                    if (args.KeyChar == Map.ZoomOutKey)
                    {
                        if (Map.ZoomLevel < 4f)
                        {
                            Map.ZoomLevel += 0.25f;
                            Map.Size = (int)(Map.Size * .85f);
                        }
                    }
                }
            };

            _trayIcon = new System.Windows.Forms.NotifyIcon()
            {
                Icon = Properties.Resources.Icon1,
                ContextMenu = new System.Windows.Forms.ContextMenu(new System.Windows.Forms.MenuItem[] {
                    new System.Windows.Forms.MenuItem("Exit", Exit)
                }),
                Text = "MapAssist",
                Visible = true
            };
        }

        void Exit(object sender, EventArgs e)
        {
            _trayIcon.Visible = false;

            Dispose();
        }

        private void _window_SetupGraphics(object sender, SetupGraphicsEventArgs e)
        {
            Map.InitMapColors();

            _timer = new Timer(UpdateMap_Tick, new AutoResetEvent(false), Map.UpdateTime, Map.UpdateTime);
        }

        private void _window_DrawGraphics(object sender, DrawGraphicsEventArgs e)
        {
            var gfx = e.Graphics;

            gfx.ClearScene();

            if (_compositor != null && _currentGameData != null)
            {
                System.Drawing.Image gamemap = _compositor.Compose(_currentGameData, !Map.OverlayMode);
                var anchor = new Point(0, 0);

                if (Map.OverlayMode)
                {
                    _window.FitTo(_currentGameData.MainWindowHandle, true);

                    float w = 0;
                    float h = 0;
                    var scale = 0.0F;
                    var center = new Vector2();

                    if (ConfigurationManager.AppSettings["ZoomLevelDefault"] == null) { Map.ZoomLevel = 1; }

                    switch (Map.Position)
                    {
                        case MapPosition.Center:
                            w = _window.Width;
                            h = _window.Height;
                            scale = (1024.0F / h * w * 3f / 4f / 2.3F) * Map.ZoomLevel;
                            center = new Vector2(w / 2, h / 2 + 20);
                            break;
                        case MapPosition.TopLeft:
                            w = 640;
                            h = 360;
                            scale = (1024.0F / h * w * 3f / 4f / 3.35F + 48) * Map.ZoomLevel;
                            center = new Vector2(w / 2, h / 2);
                            break;
                        case MapPosition.TopRight:
                            w = 640;
                            h = 360;
                            scale = (1024.0F / h * w * 3f / 4f / 3.35F + 40) * Map.ZoomLevel;
                            center = new Vector2(w / 2, h / 2);
                            break;
                    }

                    System.Drawing.Point playerPosInArea = _currentGameData.PlayerPosition.OffsetFrom(_areaData.Origin).OffsetFrom(_compositor.CropOffset);

                    var playerPos = new Vector2(playerPosInArea.X, playerPosInArea.Y);
                    Vector2 Transform(Vector2 p) =>
                        center +
                        DeltaInWorldToMinimapDelta(
                            p - playerPos,
                            (float)Math.Sqrt(w * w + h * h),
                            scale,
                            0);

                    var p1 = Transform(new Vector2(0, 0));
                    var p2 = Transform(new Vector2(gamemap.Width, 0));
                    var p4 = Transform(new Vector2(0, gamemap.Height));

                    System.Drawing.PointF[] destinationPoints = {
                        new System.Drawing.PointF(p1.X, p1.Y),
                        new System.Drawing.PointF(p2.X, p2.Y),
                        new System.Drawing.PointF(p4.X, p4.Y)
                    };

                    var b = new System.Drawing.Bitmap((int) w, (int) h);

                    using (var g = System.Drawing.Graphics.FromImage(b))
                    {
                        g.DrawImage(gamemap, destinationPoints);
                    }

                    gamemap = b;
                    
                    if (Map.Position == MapPosition.TopRight)
                    {
                        anchor = new Point(_window.Width - gamemap.Width, 0);
                    }
                }
                else
                {
                    UpdateLocation();

                    switch (Map.Position)
                    {
                        case MapPosition.Center:
                            anchor = new Point(_window.Width / 2 - gamemap.Width / 2, _window.Height / 2 - gamemap.Height / 2);
                            break;
                        case MapPosition.TopRight:
                            anchor = new Point(_window.Width - gamemap.Width, 0);
                            break;
                    }
                }

                using (var image = new Image(gfx, ImageToByte(gamemap)))
                {
                    gfx.DrawImage(image, anchor, (float) Map.Opacity);
                }
            }
        }

        public static byte[] ImageToByte(System.Drawing.Image img)
        {
            using (var stream = new MemoryStream())
            {
                img.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                return stream.ToArray();
            }
        }

        public void Run()
        {
            _window.Create();
            _window.Join();
        }

        private void UpdateMap_Tick(object stateInfo)
        {
            if (Interlocked.CompareExchange(ref _isBusy, 1, 0) == 1)
            {
                return;
            }

            var autoEvent = (AutoResetEvent)stateInfo;

            try
            {
                GameData gameData = GameMemory.GetGameData();
                if (gameData != null)
                {
                    if (gameData.HasGameChanged(_currentGameData))
                    {
                        Console.WriteLine($"Game changed: {gameData}");
                        _mapApi?.Dispose();
                        _mapApi = new MapApi(MapApi.Client, gameData.Difficulty, gameData.MapSeed);
                    }

                    if (gameData.HasMapChanged(_currentGameData))
                    {
                        Console.WriteLine($"Area changed: {gameData.Area}");
                        if (gameData.Area != Area.None)
                        {
                            _areaData = _mapApi.GetMapData(gameData.Area);
                            List<PointOfInterest> pointsOfInterest = PointOfInterestHandler.Get(_mapApi, _areaData);
                            _compositor = new Compositor(_areaData, pointsOfInterest);
                        }
                        else
                        {
                            _compositor = null;
                        }
                    }
                }

                _currentGameData = gameData;

                if (ShouldHideMap())
                {
                    _window.Hide();
                }
                else
                {
                    if (!_window.IsVisible)
                    {
                        _window.Show();
                        if (Map.AlwaysOnTop) _window.PlaceAbove(_currentGameData.MainWindowHandle);
                    }
                }
            }
            finally
            {
                _isBusy = 0;
            }

            autoEvent.Set();
        }

        private bool ShouldHideMap()
        {
            if (!_show) return true;
            if (!InGame()) return true;
            if (_currentGameData.Area == Area.None) return true;
            if (Array.Exists(Map.HiddenAreas, element => element == _currentGameData.Area)) return true;
            if (Map.ToggleViaInGameMap && !_currentGameData.MapShown) return true;
            return false;
        }

        private bool InGame()
        {
            return _currentGameData != null && _currentGameData.MainWindowHandle != IntPtr.Zero &&
                   WindowsExternal.GetForegroundWindow() == _currentGameData.MainWindowHandle;
        }
        public Vector2 DeltaInWorldToMinimapDelta(Vector2 delta, double diag, float scale, float deltaZ = 0)
        {
            var CAMERA_ANGLE = -26F * 3.14159274F / 180;

            var cos = (float)(diag * Math.Cos(CAMERA_ANGLE) / scale);
            var sin = (float)(diag * Math.Sin(CAMERA_ANGLE) /
                               scale);

            return new Vector2((delta.X - delta.Y) * cos, deltaZ - (delta.X + delta.Y) * sin);
        }

        /// <summary>
        /// Resize overlay to currently active screen
        /// </summary>
        private void UpdateLocation()
        {
            var screen = System.Windows.Forms.Screen.FromHandle(_currentGameData.MainWindowHandle);
            _window.Move(screen.WorkingArea.X, screen.WorkingArea.Y);
            _window.Resize(screen.WorkingArea.Width, screen.WorkingArea.Height);
        }

        ~Overlay()
        {
            Dispose(false);
        }

        private bool disposedValue;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                _timer?.Dispose();
                _mapApi?.Dispose();
                _window.Dispose();

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
