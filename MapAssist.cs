using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using MapAssist.Helpers;
using MapAssist.Settings;
using MapAssist.Types;
using Graphics = GameOverlay.Drawing.Graphics;
using GameOverlay.Drawing;
using Size = System.Drawing.Size;
using System.Diagnostics;
using NLog;


namespace MapAssist
{
    public class MapAssist : D2ToolboxCore.IComponent
    {
        private static readonly NLog.Logger _log = NLog.LogManager.GetCurrentClassLogger();

        private GameDataReader _gameDataReader;
        private GameData _gameData;
        private Compositor _compositor;
        private uint _processId = 0;
        private bool _show = true;
        private bool _active = true;

        public MapAssist(uint pid)
        {
            _gameDataReader = new GameDataReader(pid);
            _processId = pid;
        }

        public string GameIP()
        {
            return "";
        }

        public void RegisterCommand(D2ToolboxCore.GameProcessor manager)
        {
            manager.RegisterCommand(ToggleMapShow, "set", "mh");

        }

        private void ToggleMapShow(D2ToolboxCore.GameProcessor manager, string args)
        {
            _show = !_show;

        }

        public void Draw(D2ToolboxCore.Overlay overlay, Graphics gfx, D2ToolboxCore.GameProcessor processor)
        {
            DrawGraphics(gfx, overlay.Size);
        }

        public void SetActive(bool active)
        {
            _active = active;
        }

        public void DrawGraphics(Graphics gfx, Size windowSize)
        {
            if (!_active)
            {
                return;
            }

            try
            {
                (_compositor, _gameData) = _gameDataReader.Get();

                if (_compositor != null && _gameData != null)
                {
                    var errorLoadingAreaData = _compositor._areaData == null;

                    var overlayHidden = !_show ||
                        errorLoadingAreaData ||
                        (MapAssistConfiguration.Loaded.RenderingConfiguration.ToggleViaInGameMap && !_gameData.MenuOpen.Map) ||
                        (MapAssistConfiguration.Loaded.RenderingConfiguration.ToggleViaInGamePanels && _gameData.MenuPanelOpen > 0) ||
                        (MapAssistConfiguration.Loaded.RenderingConfiguration.ToggleViaInGamePanels && _gameData.MenuOpen.EscMenu) ||
                        Array.Exists(MapAssistConfiguration.Loaded.HiddenAreas, area => area == _gameData.Area) ||
                        _gameData.Area == Area.None ||
                        gfx.Width == 1 ||
                        gfx.Height == 1;

                    var size = MapAssistConfiguration.Loaded.RenderingConfiguration.Size;

                    var drawBounds = new Rectangle(0, 0, gfx.Width, gfx.Height * 0.8f);
                    var playerIconWidth = PlayerIconWidth(windowSize);
                    switch (MapAssistConfiguration.Loaded.RenderingConfiguration.Position)
                    {
                        case MapPosition.TopLeft:
                            drawBounds = new Rectangle(playerIconWidth + 40, playerIconWidth + 100, 0, playerIconWidth + 100 + size);
                            break;
                        case MapPosition.TopRight:
                            drawBounds = new Rectangle(0, 100, gfx.Width, 100 + size);
                            break;
                    }

                    _compositor.Init(gfx, _gameData, drawBounds);

                    if (!overlayHidden)
                    {
                        _compositor.DrawGamemap(gfx);
                        _compositor.DrawOverlay(gfx);
                        _compositor.DrawBuffs(gfx);
                    }

                    if (MapAssistConfiguration.Loaded.ItemLog.Enabled)
                    {
                        _compositor.DrawItemLog(gfx, new Point(playerIconWidth + 50, playerIconWidth + 50));
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex);
            }
        }

        private int PlayerIconWidth(Size windowSize)
        {
            return windowSize.Height / 20;
        }

    }
}
