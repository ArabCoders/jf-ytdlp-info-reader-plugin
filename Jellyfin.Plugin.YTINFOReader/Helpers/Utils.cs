﻿using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;

namespace Jellyfin.Plugin.YTINFOReader.Helpers
{
    public class Utils
    {
        public static bool IsFresh(MediaBrowser.Model.IO.FileSystemMetadata fileInfo)
        {
            if (fileInfo.Exists && DateTime.UtcNow.Subtract(fileInfo.LastWriteTimeUtc).Days <= 10)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        ///  Returns the Youtube ID from the file path. Matches last 11 character field inside square brackets.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static string GetYTID(string name)
        {
            var rxc = new Regex(Constants.CHANNEL_RX, RegexOptions.Compiled | RegexOptions.IgnoreCase);
            if (rxc.IsMatch(name))
            {
                MatchCollection match = rxc.Matches(name);
                return match[0].Groups["id"].ToString();
            }

            var rx = new Regex(Constants.VIDEO_RX, RegexOptions.Compiled | RegexOptions.IgnoreCase);
            if (rx.IsMatch(name))
            {
                MatchCollection match = rx.Matches(name);
                return match[0].Groups["id"].ToString();
            }
            return "";
        }

        /// <summary>
        /// Creates a person object of type director for the provided name.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="channel_id"></param>
        /// <returns></returns>
        public static PersonInfo CreatePerson(string name, string channel_id)
        {
            return new PersonInfo
            {
                Name = name,
                Type = PersonType.Director,
                ProviderIds = new Dictionary<string, string> { { Constants.PLUGIN_NAME, channel_id } },
            };
        }

        /// <summary>
        /// Returns path to where metadata json file should be.
        /// </summary>
        /// <param name="appPaths"></param>
        /// <param name="youtubeID"></param>
        /// <returns></returns>
        public static string GetVideoInfoPath(IServerApplicationPaths appPaths, string youtubeID)
        {
            var dataPath = Path.Combine(appPaths.CachePath, Constants.PLUGIN_NAME, youtubeID);
            return Path.Combine(dataPath, "ytvideo.info.json");
        }

        /// <summary>
        /// Reads JSON data from file.
        /// </summary>
        /// <param name="metaFile"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static YTDLData ReadYTDLInfo(string fpath, FileSystemMetadata path, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string jsonString = File.ReadAllText(fpath);
            YTDLData data = JsonSerializer.Deserialize<YTDLData>(jsonString, new JsonSerializerOptions
            {
                NumberHandling = JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString
            });
            data.file_path = path;
            return data;
        }

        /// <summary>
        /// Provides a Movie Metadata Result from a json object.
        /// </summary>
        /// <param name="json"></param>
        /// <returns></returns>
        public static MetadataResult<Movie> YTDLJsonToMovie(YTDLData json)
        {
            var item = new Movie();
            var result = new MetadataResult<Movie>
            {
                HasMetadata = true,
                Item = item
            };
            result.Item.Name = json.title;
            result.Item.Overview = json.description;
            var date = new DateTime(1970, 1, 1);
            try
            {
                date = DateTime.ParseExact(json.upload_date, "yyyyMMdd", null);
            }
            catch
            {

            }
            result.Item.ProductionYear = date.Year;
            result.Item.PremiereDate = date;
            result.AddPerson(Utils.CreatePerson(json.uploader, json.channel_id));
            result.Item.ProviderIds.Add(Constants.PLUGIN_NAME, json.id);

            return result;
        }

        /// <summary>
        /// Provides a MusicVideo Metadata Result from a json object.
        /// </summary>
        /// <param name="json"></param>
        /// <returns></returns>
        public static MetadataResult<MusicVideo> YTDLJsonToMusicVideo(YTDLData json)
        {
            var item = new MusicVideo();
            var result = new MetadataResult<MusicVideo>
            {
                HasMetadata = true,
                Item = item
            };
            result.Item.Name = String.IsNullOrEmpty(json.track) ? json.title : json.track;
            result.Item.Artists = new List<string> { json.artist };
            result.Item.Album = json.album;
            result.Item.Overview = json.description;
            var date = new DateTime(1970, 1, 1);
            try
            {
                date = DateTime.ParseExact(json.upload_date, "yyyyMMdd", null);
            }
            catch { }
            result.Item.ProductionYear = date.Year;
            result.Item.PremiereDate = date;
            result.AddPerson(Utils.CreatePerson(json.uploader, json.channel_id));
            result.Item.ProviderIds.Add(Constants.PLUGIN_NAME, json.id);

            return result;
        }

        /// <summary>
        /// Provides a Episode Metadata Result from a json object.
        /// </summary>
        /// <param name="json"></param>
        /// <returns></returns>
        public static MetadataResult<Episode> YTDLJsonToEpisode(YTDLData json)
        {
            var item = new Episode();
            var result = new MetadataResult<Episode>
            {
                HasMetadata = true,
                Item = item
            };
            result.Item.Name = json.title;
            result.Item.Overview = json.description;
            var date = new DateTime(1970, 1, 1);
            try
            {
                date = DateTime.ParseExact(json.upload_date, "yyyyMMdd", null);
            }
            catch { }
            result.Item.ProductionYear = date.Year;
            result.Item.PremiereDate = date;
            result.Item.ForcedSortName = date.ToString("yyyyMMdd") + "-" + result.Item.Name;
            result.AddPerson(Utils.CreatePerson(json.uploader, json.channel_id));
            result.Item.IndexNumber = int.Parse("1" + date.ToString("MMdd"));
            result.Item.ParentIndexNumber = int.Parse(date.ToString("yyyy"));
            result.Item.ProviderIds.Add(Constants.PLUGIN_NAME, json.id);

            // If the json data has epoch, do not bother calling file data.
            if (json.epoch != null)
            {
                result.Item.IndexNumber = int.Parse("1" + date.ToString("MMdd") + DateTimeOffset.FromUnixTimeSeconds(json.epoch ?? new long()).ToString("hhmm"));
                return result;
            }

            // if no json.epoch is found fallback to file last modification time.
            if (json.file_path != null)
            {
                result.Item.IndexNumber = int.Parse("1" + date.ToString("MMdd") + json.file_path.LastWriteTimeUtc.ToString("hhmm"));
            }

            return result;
        }
        /// <summary>
        /// Provides a MusicVideo Metadata Result from a json object.
        /// </summary>
        /// <param name="json"></param>
        /// <returns></returns>
        public static MetadataResult<Series> YTDLJsonToSeries(YTDLData json)
        {
            var item = new Series();
            var result = new MetadataResult<Series>
            {
                HasMetadata = true,
                Item = item
            };
            result.Item.Name = json.uploader;
            result.Item.Overview = json.description;
            result.Item.ProviderIds.Add(Constants.PLUGIN_NAME, json.channel_id);
            return result;
        }
    }

}
