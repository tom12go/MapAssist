using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GameOverlay.Drawing;
using Graphics = GameOverlay.Drawing.Graphics;
using Size = System.Drawing.Size;
using D2ToolboxCore;
using D2ToolboxCore.Files;
using MapAssist.Settings;
using MapAssist.Types;
using MapAssist.Helpers;
using GameData = MapAssist.Types.GameData;

namespace MapAssist
{
    public class MapAssist : IDisposable
    {
        private static MapAssistConfiguration _rootConfig;

        public static void Init(string confPath)
        {
            _rootConfig = MapAssistConfiguration.Load(confPath);
        }

        private uint _processId;
        private MapAssistConfiguration _config;

        public MapAssist(uint pid)
        {
            _processId = pid;
            _config = _rootConfig.DeepClone();
        }

        public void Draw(Overlay overlay, GameProcessor processor)
        {
            return;
        }

        private static readonly NLog.Logger _log = NLog.LogManager.GetCurrentClassLogger();

        private GameDataReader _gameDataReader;
        private GameData _gameData;
        private Compositor _compositor;
        private bool _show = true;

        public MapAssist()
        {
            _gameDataReader = new GameDataReader();
            _config = MapAssistConfiguration.Loaded;
        }

        public void Draw(Graphics gfx, Size windowSize)
        {
            try
            {
                (_compositor, _gameData) = _gameDataReader.Get();

                if (_compositor != null && _compositor != null && _gameData != null)
                {
                    var errorLoadingAreaData = _compositor._areaData == null;

                    var overlayHidden = !_show ||
                        errorLoadingAreaData ||
                        (_config.RenderingConfiguration.ToggleViaInGameMap && !_gameData.MenuOpen.Map) ||
                        (_config.RenderingConfiguration.ToggleViaInGamePanels && _gameData.MenuPanelOpen > 0) ||
                        (_config.RenderingConfiguration.ToggleViaInGamePanels && _gameData.MenuOpen.EscMenu) ||
                        Array.Exists(_config.HiddenAreas, area => area == _gameData.Area) ||
                        _gameData.Area == Area.None ||
                        gfx.Width == 1 ||
                        gfx.Height == 1;

                    var size = _config.RenderingConfiguration.Size;

                    var drawBounds = new Rectangle(0, 0, gfx.Width, gfx.Height * 0.8f);
                    switch (_config.RenderingConfiguration.Position)
                    {
                        case MapPosition.TopLeft:
                            drawBounds = new Rectangle(PlayerIconWidth() + 40, PlayerIconWidth() + 100, 0, PlayerIconWidth() + 100 + size);
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

                    //_compositor.DrawGameInfo(gfx, new Point(PlayerIconWidth() + 50, PlayerIconWidth() + 50), e, errorLoadingAreaData);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex);
            }
        }

        public void KeyPressHandler(char argsKeyChar)
        {
            if (argsKeyChar == _config.HotkeyConfiguration.ToggleKey)
            {
                _show = !_show;
            }

            if (argsKeyChar == _config.HotkeyConfiguration.ZoomInKey)
            {
                if (_config.RenderingConfiguration.ZoomLevel > 0.25f)
                {
                    _config.RenderingConfiguration.ZoomLevel -= 0.25f;
                    _config.RenderingConfiguration.Size +=
                        (int)(_config.RenderingConfiguration.InitialSize * 0.05f);
                }
            }

            if (argsKeyChar == _config.HotkeyConfiguration.ZoomOutKey)
            {
                if (_config.RenderingConfiguration.ZoomLevel < 4f)
                {
                    _config.RenderingConfiguration.ZoomLevel += 0.25f;
                    _config.RenderingConfiguration.Size -=
                      (int)(_config.RenderingConfiguration.InitialSize * 0.05f);
                }
            }

            if (argsKeyChar == _config.HotkeyConfiguration.GameInfoKey)
            {
                _config.GameInfo.Enabled = !_config.GameInfo.Enabled;
            }
        }

        private float PlayerIconWidth(Size size)
        {
            return size.Height / 20f;
        }

        ~MapAssist()
        {
            Dispose();
        }


        public void Dispose()
        {
            if (_compositor != null) _compositor.Dispose(); // This last so it's disposed after GraphicsWindow stops using it
            GC.SuppressFinalize(this);
        }

    }

}
