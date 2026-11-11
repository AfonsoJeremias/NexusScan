using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Playnite.SDK;
using Playnite.SDK.Models;

namespace BetterScan
{
    public struct VerifyRes
    {
        public int ndir;
        public int nrom;
        public int nact;
    }

    public class Impl
    {
        static public string TagInvalidDir = "__INVALID_DIR";
        static public string TagInvalidRom = "__INVALID_ROM";
        static public string TagInvalidAction = "__INVALID_ACTION";

        private static void addTag(IPlayniteAPI api, Game game, Guid tid)
        {
            if (game.TagIds is null) game.TagIds = new List<Guid>();
            if (!game.TagIds.Contains(tid)) game.TagIds.Add(tid);
            api.Database.Games.Update(game);
        }

        private static void removeTag(IPlayniteAPI api, Game game, Guid tid)
        {
            if (game.TagIds is null) return;
            game.TagIds.Remove(tid);
            api.Database.Games.Update(game);
        }

        private static bool VerifyFolder(IPlayniteAPI api, Game game)
        {
            string path = api.ExpandGameVariables(game, game.InstallDirectory);
            var tag = api.Database.Tags.Add(TagInvalidDir);
            if (!Directory.Exists(path))
            {
                addTag(api, game, tag.Id);
                return false;
            }
            removeTag(api, game, tag.Id);
            return true;
        }

        private static bool VerifyRom(IPlayniteAPI api, Game game)
        {
            if (game.Roms == null) return true;
            var tag = api.Database.Tags.Add(TagInvalidRom);
            foreach (var rom in game.Roms)
            {
                string path = api.ExpandGameVariables(game, rom.Path);
                if (!File.Exists(path)) { addTag(api, game, tag.Id); return false; }
            }
            removeTag(api, game, tag.Id);
            return true;
        }

        private static bool VerifyAction(IPlayniteAPI api, Game game)
        {
            if (game.GameActions == null) return true;
            var tag = api.Database.Tags.Add(TagInvalidAction);
            foreach (var action in game.GameActions)
            {
                string path = api.ExpandGameVariables(game, action.Path);
                if (!File.Exists(path)) { addTag(api, game, tag.Id); return false; }
            }
            removeTag(api, game, tag.Id);
            return true;
        }

        public static VerifyRes VerifyPath(IPlayniteAPI api)
        {
            int ndir = 0, nrom = 0, nact = 0;
            foreach (var game in api.Database.Games.Where(g => g.IsInstalled))
            {
                ndir += !VerifyFolder(api, game) ? 1 : 0;
                nrom += !VerifyRom(api, game) ? 1 : 0;
                nact += !VerifyAction(api, game) ? 1 : 0;
            }
            return new VerifyRes { ndir = ndir, nrom = nrom, nact = nact };
        }
    }
}