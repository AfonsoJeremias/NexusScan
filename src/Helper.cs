using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace BetterScan
{
    public class Helper
    {
        public static string WildCardToRegular(string value)
        {
            value = value.ToLower();
            return "^" + Regex.Escape(value).Replace("\\?", ".").Replace("\\*", ".*") + "$";
        }

        public static bool IsMatch(string s, IEnumerable<Regex> plist)
        {
            s = s.ToLower();
            foreach (var p in plist)
                if (p.IsMatch(s)) return true;
            return false;
        }

        public static List<FileInfo> Search(string path,
            IEnumerable<string> excludedFolderList,
            IEnumerable<string> pattern,
            IEnumerable<string> skipPattern,
            bool recursive = true)
        {
            var patternList = new List<Regex>();
            foreach (string s in pattern)
                patternList.Add(new Regex(WildCardToRegular(s)));

            var skipList = new List<Regex>();
            foreach (string s in skipPattern)
                skipList.Add(new Regex(WildCardToRegular(s)));

            return Search(path, excludedFolderList, patternList, skipList, recursive);
        }

        private static List<FileInfo> Search(string path,
            IEnumerable<string> excludedFolderList,
            IEnumerable<Regex> patternList,
            IEnumerable<Regex> skipList,
            bool recursive)
        {
            var res = new List<FileInfo>();
            var di = new DirectoryInfo(path);

            if (excludedFolderList.Contains(di.FullName))
                return res;

            foreach (var fi in di.EnumerateFiles())
            {
                try
                {
                    if (!IsMatch(fi.FullName, skipList) && IsMatch(fi.FullName, patternList))
                        res.Add(fi);
                }
                catch { }
            }

            if (recursive)
            {
                foreach (var sub in di.EnumerateDirectories())
                {
                    try
                    {
                        if (!excludedFolderList.Contains(sub.FullName))
                            res.AddRange(Search(sub.FullName, excludedFolderList, patternList, skipList, recursive));
                    }
                    catch { }
                }
            }
            return res;
        }

        public static string ToRelPath(string s1, string root, string varname)
        {
            char[] sep = { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };
            string[] sp1 = s1.Split(sep, StringSplitOptions.RemoveEmptyEntries);
            string[] sp2 = root.Split(sep, StringSplitOptions.RemoveEmptyEntries);
            int j = -1;
            for (int i = 0; i < sp1.Length && i < sp2.Length; i++)
            {
                if (sp1[i] != sp2[i]) break;
                j = i + 1;
            }
            if (j == -1) return s1;
            string res = varname;
            for (int n = sp2.Length - j; n > 0; n--) res += Path.DirectorySeparatorChar + "..";
            for (int i = j; i < sp1.Length; i++) res += Path.DirectorySeparatorChar + sp1[i];
            return res;
        }

        public static string ConcatPath(string root, string fpname) => root + Path.DirectorySeparatorChar + fpname;
        public static string ResolveRelPath(string basePath, string relPath) => Path.GetFullPath(ConcatPath(basePath, relPath));
        public static string ResolveRelPath(string rawPath) => Path.GetFullPath(rawPath);

        public static string SearchIconFile(string fp)
        {
            string folderName = Path.GetDirectoryName(fp);
            var di = new DirectoryInfo(folderName);
            foreach (var fi in di.EnumerateFiles())
                if (fi.Extension.ToLower() == ".ico")
                    return fi.FullName;
            return null;
        }
    }
}