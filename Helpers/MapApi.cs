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

using MapAssist.Settings;
using MapAssist.Types;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

#pragma warning disable 649

namespace MapAssist.Helpers
{
    public class MapApi : IDisposable
    {
        public static readonly HttpClient Client = HttpClient(MapAssistConfiguration.Loaded.ApiConfiguration.Endpoint, MapAssistConfiguration.Loaded.ApiConfiguration.Token);
        private static readonly NLog.Logger _log = NLog.LogManager.GetCurrentClassLogger();
        private readonly Difficulty _difficulty;
        private readonly uint _mapSeed;
        private readonly ConcurrentDictionary<Area, AreaData> _cache;
        private readonly HttpClient _client;


        public MapApi(Difficulty difficulty, uint mapSeed)
        {
            _client = Client;
            _difficulty = difficulty;
            _mapSeed = mapSeed;
            // Cache for pre-fetching maps for the surrounding areas.
            _cache = new ConcurrentDictionary<Area, AreaData>();
        }

        public AreaData GetMapData(Area area)
        {
            if (!_cache.TryGetValue(area, out AreaData areaData))
            {
                // Not in the cache, block.
                _log.Info($"Requesting map data for {area} ({_mapSeed} seed, {_difficulty} difficulty)");

                areaData = GetMapDataInternal(area);
            }
            else
            {
                _log.Info($"Cache found for {area}");
            }

            if (areaData != null)
            {
                Area[] adjacentAreas = areaData.AdjacentLevels.Keys.ToArray();

                var additionalAreas = GetAdjacentLevelsForWideArea(areaData.Area);
                adjacentAreas = adjacentAreas.Concat(additionalAreas).ToArray();

                if (adjacentAreas.Length > 0)
                {
                    _log.Info($"{adjacentAreas.Length} adjacent areas to {area} found");

                    foreach (var adjacentArea in adjacentAreas)
                    {
                        if (!_cache.TryGetValue(adjacentArea, out AreaData adjAreaData))
                        {
                            _log.Info($"Requesting map data for {adjacentArea} ({_mapSeed} seed, {_difficulty} difficulty)");
                            _cache[adjacentArea] = GetMapDataInternal(adjacentArea);
                            areaData.AdjacentAreas[adjacentArea] = _cache[adjacentArea];
                        }
                        else
                        {
                            _log.Info($"Cache found for {adjacentArea}");
                            areaData.AdjacentAreas[adjacentArea] = adjAreaData;
                        }
                    }
                }
                else
                {
                    _log.Info($"No adjacent areas to {area} found");
                }
            }
            else
            {
                _log.Info($"areaData was null on {area}");
            }

            return areaData;
        }


        private Area[] GetAdjacentLevelsForWideArea(Area area)
        {
            // Improve stitching by rendering more areas than directly adjacent levels
            // Sometimes render areas 2 maps away to get a better picture
            switch (area)
            {
                case Area.BlackMarsh:
                    return new Area[] {
                        Area.MonasteryGate,
                        Area.OuterCloister,
                    };
                case Area.TamoeHighland:
                    return new Area[] {
                        Area.OuterCloister,
                        Area.Barracks,
                    };
                case Area.MonasteryGate:
                    return new Area[] {
                        Area.BlackMarsh,
                        Area.Barracks,
                    };
                case Area.OuterCloister:
                    return new Area[] {
                        Area.BlackMarsh,
                        Area.TamoeHighland,
                        Area.Barracks, // Missing adjacent area
                    };
                case Area.Barracks:
                    return new Area[] {
                        Area.TamoeHighland,
                        Area.MonasteryGate,
                        Area.OuterCloister, // Missing adjacent area
                    };
                case Area.InnerCloister:
                    return new Area[] {
                        Area.Cathedral, // Missing adjacent area
                    };
                case Area.Cathedral:
                    return new Area[] {
                        Area.InnerCloister, // Missing adjacent area
                    };
                case Area.LutGholein:
                    return new Area[] {
                        Area.DryHills,
                    };
                case Area.DryHills:
                    return new Area[] {
                        Area.LutGholein,
                        Area.LostCity,
                    };
                case Area.RockyWaste:
                    return new Area[] {
                        Area.FarOasis,
                    };
                case Area.LostCity:
                    return new Area[] {
                        Area.DryHills,
                    };
                case Area.FarOasis:
                    return new Area[] {
                        Area.RockyWaste,
                    };
                case Area.GreatMarsh:
                    return new Area[] {
                        Area.FlayerJungle,
                    };
                case Area.FlayerJungle:
                    return new Area[] {
                        Area.GreatMarsh,
                    };
                case Area.UpperKurast:
                    return new Area[] {
                        Area.Travincal,
                    };
                case Area.Travincal:
                    return new Area[] {
                        Area.UpperKurast,
                    };
                default:
                    return new Area[] { };
            }
        }


        private void Prefetch(params Area[] areas)
        {
            var prefetchBackgroundWorker = new BackgroundWorker();
            prefetchBackgroundWorker.DoWork += (sender, args) =>
            {
                // Special value telling us to exit.
                if (areas.Length == 0)
                {
                    _log.Info("Prefetch worker terminating");
                    return;
                }

                foreach (Area area in areas)
                {
                    if (_cache.ContainsKey(area)) continue;

                    _cache[area] = GetMapDataInternal(area);
                    _log.Info($"Prefetched {area}");
                }
            };
            prefetchBackgroundWorker.RunWorkerAsync();
            prefetchBackgroundWorker.Dispose();
        }

        private AreaData GetMapDataInternal(Area area)
        {
            // get /{mapSeed}/{difficulty}/{area}
            HttpResponseMessage response = _client.GetAsync(_mapSeed + "/" + _difficulty + "/" + (uint)area).GetAwaiter().GetResult();
            response.EnsureSuccessStatusCode();
            var content = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            var rawMapData = JsonConvert.DeserializeObject<RawAreaData>(content);
            return rawMapData.ToInternal(area);
        }

        private static HttpClient HttpClient(string endpoint, string token)
        {
            var client = new HttpClient(new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            })
            { BaseAddress = new Uri(endpoint) };
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
            client.DefaultRequestHeaders.AcceptEncoding.Add(
                new StringWithQualityHeaderValue("gzip"));
            client.DefaultRequestHeaders.AcceptEncoding.Add(
                new StringWithQualityHeaderValue("deflate"));
            if (!string.IsNullOrEmpty(token))
            {
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", token);
            }

            return client;
        }

        public void Dispose()
        {
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        [SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "ReSharper")]
        private class MapApiSession
        {
            public string id;
            public uint difficulty;
            public uint mapId;
        }
    }
}
