﻿using System.Collections.Generic;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.IO;
using Microsoft.Extensions.Logging;
using MediaBrowser.Controller.Entities.TV;
using Microsoft.Extensions.FileSystemGlobbing;
using System;
using System.Text.RegularExpressions;
using Jellyfin.Plugin.YTINFOReader.Helpers;

namespace Jellyfin.Plugin.YTINFOReader.Provider
{
    public class LocalSeriesImageProvider : ILocalImageProvider
    {
        private readonly IFileSystem _fileSystem;
        private readonly ILogger<LocalImageProvider> _logger;
        public string Name => Constants.PLUGIN_NAME;

        public LocalSeriesImageProvider(IFileSystem fileSystem, ILogger<LocalImageProvider> logger)
        {
            _logger = logger;
            Utils.Logger = logger;
            _fileSystem = fileSystem;
        }
        public bool Supports(BaseItem item) => item is Series;

        private string GetSeriesInfo(string path)
        {
            _logger.LogDebug("YTLocalImageSeries GetSeriesInfo: {Path}", path);
            Matcher matcher = new();
            matcher.AddInclude("**/*.jpg");
            matcher.AddInclude("**/*.png");
            matcher.AddInclude("**/*.webp");
            Regex rxc = new Regex(Constants.CHANNEL_RX, RegexOptions.Compiled | RegexOptions.IgnoreCase);
            Regex rxp = new Regex(Constants.PLAYLIST_RX, RegexOptions.Compiled | RegexOptions.IgnoreCase);
            string infoPath = "";
            foreach (string file in matcher.GetResultsInFullPath(path))
            {
                if (rxc.IsMatch(file) || rxp.IsMatch(file))
                {
                    infoPath = file;
                    break;
                }
            }
            _logger.LogDebug("YTLocalImageSeries GetSeriesInfo Result: {InfoPath}", infoPath);
            return infoPath;
        }

        /// <summary>
        /// Retrieves Image.
        /// </summary>
        /// <param name="item"></param>
        /// <param name="directoryService"></param>
        /// <returns></returns>
        public IEnumerable<LocalImageInfo> GetImages(BaseItem item, IDirectoryService directoryService)
        {
            _logger.LogDebug("YTLocalImageSeries GetImages: {Name}", item.Name);
            var list = new List<LocalImageInfo>();
            string jpgPath = GetSeriesInfo(item.Path);
            if (string.IsNullOrEmpty(jpgPath))
            {
                return list;
            }
            var localImg = new LocalImageInfo();
            var fileInfo = _fileSystem.GetFileSystemInfo(jpgPath);
            localImg.FileInfo = fileInfo;
            list.Add(localImg);
            _logger.LogDebug("YTLocalImageSeries GetImages Result: {Result}", list.ToString());
            return list;
        }

    }
}
