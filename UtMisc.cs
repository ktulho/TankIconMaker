﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using RT.Util;
using RT.Util.Dialogs;
using RT.Util.ExtensionMethods;
using RT.Util.Lingo;
using WotDataLib;

namespace TankIconMaker
{
    static partial class Ut
    {
        /// <summary>Shorthand for string.Format, with a more natural ordering (since formatting is typically an afterthought).</summary>
        public static string Fmt(this string formatString, params object[] args)
        {
            return string.Format(formatString, args);
        }

        /// <summary>Shorthand for comparing strings ignoring case. Suitable for things like filenames, but not address books.</summary>
        public static bool EqualsNoCase(this string string1, string string2)
        {
            return StringComparer.OrdinalIgnoreCase.Equals(string1, string2);
        }

        /// <summary>Returns one of the specified values based on which country this value represents.</summary>
        public static T Pick<T>(this Country country, T ussr, T germany, T usa, T france, T china, T uk, T japan, T czech, T sweden, T poland, T italy, T intunion, T none)
        {
            switch (country)
            {
                case Country.USSR: return ussr;
                case Country.Germany: return germany;
                case Country.USA: return usa;
                case Country.France: return france;
                case Country.China: return china;
                case Country.UK: return uk;
                case Country.Japan: return japan;
                case Country.Czech: return czech;
                case Country.Sweden: return sweden;
                case Country.Poland: return poland;
                case Country.Italy: return italy;
                case Country.Intunion: return intunion;
                case Country.None: return none;
                default: throw new Exception();
            }
        }

        /// <summary>Returns one of the specified values based on which tank class this value represents.</summary>
        public static T Pick<T>(this Class class_, T light, T medium, T heavy, T destroyer, T artillery, T none)
        {
            switch (class_)
            {
                case Class.Light: return light;
                case Class.Medium: return medium;
                case Class.Heavy: return heavy;
                case Class.Destroyer: return destroyer;
                case Class.Artillery: return artillery;
                case Class.None: return none;
                default: throw new Exception();
            }
        }

        /// <summary>Returns one of the specified values based on which tank category this value represents.</summary>
        public static T Pick<T>(this Category class_, T normal, T premium, T special, T collector)
        {
            switch (class_)
            {
                case Category.Normal: return normal;
                case Category.Premium: return premium;
                case Category.Special: return special;
                case Category.Collector: return collector;
                default: throw new Exception();
            }
        }

        /// <summary>
        /// Enumerates the full paths to each installation of World of Tanks that can be found in the registry. Enumerates only those directories which exist,
        /// but does not verify that there is a valid WoT installation at that path.
        /// </summary>
        public static IEnumerable<string> EnumerateGameInstallations()
        {
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using (var installs1 = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", writable: false))
                using (var installs2 = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall", writable: false))
                {
                    var keys = new[] { installs1, installs2 }.SelectMany(ins => ins.GetSubKeyNames().Select(name => new { Key = ins, Name = name }));
                    foreach (var item in keys.Where(k => k.Name.StartsWith("{1EAC1D02-C6AC-4FA6-9A44-96258C37")))
                    {
                        try
                        {
                            using (var key = item.Key.OpenSubKey(item.Name))
                                paths.Add(key.GetValue("InstallLocation") as string);
                        }
                        catch { }
                    }
                }
            }
            catch { }

            paths.RemoveWhere(p => p == null || !Directory.Exists(p));
            return paths;
        }

        /// <summary>
        /// Returns the first item whose <paramref name="maxOf"/> selector is maximal in this collection, or null if the collection is empty.
        /// </summary>
        public static TItem MaxOrDefault<TItem, TSelector>(this IEnumerable<TItem> collection, Func<TItem, TSelector> maxOf)
        {
            return collection.MaxAll(maxOf).FirstOrDefault();
        }

        /// <summary>
        /// Enumerates all the items whose <paramref name="maxOf"/> selector is maximal in this collection.
        /// </summary>
        public static IEnumerable<TItem> MaxAll<TItem, TSelector>(this IEnumerable<TItem> collection, Func<TItem, TSelector> maxOf)
        {
            var comparer = Comparer<TSelector>.Default;
            var largest = default(TSelector);
            var result = new List<TItem>();
            bool any = false;
            foreach (var item in collection)
            {
                var current = maxOf(item);
                var compare = comparer.Compare(current, largest);
                if (!any || compare > 0)
                {
                    any = true;
                    largest = current;
                    result.Clear();
                    result.Add(item);
                }
                else if (compare == 0)
                    result.Add(item);
            }
            return result;
        }

        /// <summary>
        /// Reduces the size of a stack trace by removing all lines which are outside this program's namespace and
        /// leaving only relative source file names.
        /// </summary>
        public static string CollapseStackTrace(string stackTrace)
        {
            var lines = stackTrace.Split('\n');
            var result = new StringBuilder();
            bool needEllipsis = true;
            string fileroot = null;
            try { fileroot = Path.GetDirectoryName(new StackFrame(true).GetFileName()) + @"\"; }
            catch { }
            foreach (var line in lines)
            {
                if (line.Contains(typeof(Ut).Namespace))
                {
                    result.AppendLine("  " + (fileroot == null ? line : line.Replace(fileroot, "")).Trim());
                    needEllipsis = true;
                }
                else if (needEllipsis)
                {
                    result.AppendLine("  ...");
                    needEllipsis = false;
                }
            }
            var str = result.ToString();
            return str.EndsWith("\r\n") ? str.Substring(0, str.Length - 2) : str;
        }

        public static int ModPositive(int value, int modulus)
        {
            int result = value % modulus;
            return result >= 0 ? result : (result + modulus);
        }

        /// <summary>
        /// Works correctly even if context is null; will simply skip trying to make the path relative to game directories.
        /// </summary>
        public static string MakeRelativePath(WotContext context, string path)
        {
            try
            {
                if (PathUtil.IsSubpathOfOrSame(path, PathUtil.AppPath))
                    return PathUtil.ToggleRelative(PathUtil.AppPath, path);
            }
            catch { }
            try
            {
                if (PathUtil.IsSubpathOfOrSame(path, Path.Combine(context.Installation.Path, Ut.ExpandPath(context, context.VersionConfig.PathMods))))
                    return PathUtil.ToggleRelative(Path.Combine(context.Installation.Path, Ut.ExpandPath(context, context.VersionConfig.PathMods)), path);
            }
            catch { }
            try
            {
                if (PathUtil.IsSubpathOfOrSame(path, context.Installation.Path))
                    return PathUtil.ToggleRelative(context.Installation.Path, path);
            }
            catch { }
            return path;
        }

        /// <summary>
        /// Determines whether the specified file contains the specified text. The file doesn’t have to exist.
        /// </summary>
        public static bool FileContains(string fileName, string content)
        {
            if (content == null)
                return false;
            try
            {
                foreach (var line in File.ReadLines(fileName))
                    if (line.Contains(content))
                        return true;
                return false;
            }
            catch (FileNotFoundException) { return false; }
            catch (DirectoryNotFoundException) { return false; }
        }

        /// <summary>
        /// Generates a representation of the specified byte array as hexadecimal numbers (“hexdump”).
        /// </summary>
        public static string ToHex(this byte[] data)
        {
            if (data == null)
                throw new ArgumentNullException("data");

            char[] charArr = new char[data.Length * 2];
            var j = 0;
            for (int i = 0; i < data.Length; i++)
            {
                byte b = (byte) (data[i] >> 4);
                charArr[j] = (char) (b < 10 ? '0' + b : 'W' + b);   // 'a'-10 = 'W'
                j++;
                b = (byte) (data[i] & 0xf);
                charArr[j] = (char) (b < 10 ? '0' + b : 'W' + b);
                j++;
            }
            return new string(charArr);
        }

        /// <summary>Copies <paramref name="len"/> bytes from one location to another. Works fastest if <paramref name="len"/> is divisible by 16.</summary>
        public static unsafe void MemSet(byte* dest, byte value, int len)
        {
            ushort ushort_ = (ushort) (value | (value << 8));
            uint uint_ = (uint) (ushort_ | (ushort_ << 16));
            ulong ulong_ = uint_ | ((ulong) uint_ << 32);
            if (len >= 16)
            {
                do
                {
                    *(ulong*) dest = ulong_;
                    *(ulong*) (dest + 8) = ulong_;
                    dest += 16;
                }
                while ((len -= 16) >= 16);
            }
            if (len > 0)
            {
                if ((len & 8) != 0)
                {
                    *(ulong*) dest = ulong_;
                    dest += 8;
                }
                if ((len & 4) != 0)
                {
                    *(uint*) dest = uint_;
                    dest += 4;
                }
                if ((len & 2) != 0)
                {
                    *(ushort*) dest = ushort_;
                    dest += 2;
                }
                if ((len & 1) != 0)
                    *dest = value;
            }
        }

        /// <summary>Copies <paramref name="len"/> bytes from one location to another. Works fastest if <paramref name="len"/> is divisible by 16.</summary>
        public static unsafe void MemCpy(byte* dest, byte* src, int len)
        {
            if (len >= 16)
            {
                do
                {
                    *(long*) dest = *(long*) src;
                    *(long*) (dest + 8) = *(long*) (src + 8);
                    dest += 16;
                    src += 16;
                }
                while ((len -= 16) >= 16);
            }
            if (len > 0)
            {
                if ((len & 8) != 0)
                {
                    *(long*) dest = *(long*) src;
                    dest += 8;
                    src += 8;
                }
                if ((len & 4) != 0)
                {
                    *(int*) dest = *(int*) src;
                    dest += 4;
                    src += 4;
                }
                if ((len & 2) != 0)
                {
                    *(short*) dest = *(short*) src;
                    dest += 2;
                    src += 2;
                }
                if ((len & 1) != 0)
                    *dest = *src;
            }
        }

        /// <summary>Copies <paramref name="len"/> bytes from one location to another. Works fastest if <paramref name="len"/> is divisible by 16.</summary>
        public static unsafe void MemCpy(byte[] dest, byte* src, int len)
        {
            if (len > dest.Length)
                throw new ArgumentOutOfRangeException("len");
            fixed (byte* destPtr = dest)
                MemCpy(destPtr, src, len);
        }

        /// <summary>Copies <paramref name="len"/> bytes from one location to another. Works fastest if <paramref name="len"/> is divisible by 16.</summary>
        public static unsafe void MemCpy(byte* dest, byte[] src, int len)
        {
            if (len > src.Length)
                throw new ArgumentOutOfRangeException("len");
            fixed (byte* srcPtr = src)
                MemCpy(dest, srcPtr, len);
        }

        public static Language GetOsLanguage()
        {
            try
            {
                var curUiLanguage = Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();

                // Special case for the languages we supply, so that if there are several with the same 2-letter code
                // we can pick the right one.
                switch (curUiLanguage)
                {
                    case "en": return Language.EnglishUK;
                    case "ru": return Language.Russian;
                    case "de": return Language.German;
                }

                var t = typeof(Language);
                foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Static))
                {
                    var a = f.GetCustomAttributes<LanguageInfoAttribute>().FirstOrDefault();
                    if (a != null && a.LanguageCode == curUiLanguage)
                        return (Language) f.GetValue(null);
                }
            }
            catch { }
            return Language.EnglishUK;
        }

        public static string AppendExpandableFilename(string folderPath, SaveType saveType)
        {
            switch (saveType)
            {
                case SaveType.Icons:
                    return folderPath.TrimEnd('\\') + @"\{TankId}{Ext}";
                case SaveType.BattleAtlas:
                    return folderPath.TrimEnd('\\') + @"\" + AtlasBuilder.battleAtlas + ".png";
                case SaveType.VehicleMarkerAtlas:
                    return folderPath.TrimEnd('\\') + @"\" + AtlasBuilder.vehicleMarkerAtlas + ".png";
                case SaveType.CustomAtlas:
                    return folderPath.TrimEnd('\\') + @"\" + AtlasBuilder.customAtlas + ".png";
                default:
                    throw new ArgumentOutOfRangeException("saveType");
            }
        }

        public static string GetSafeFilename(string filename)
        {
            return string.Join("_", filename.Split(Path.GetInvalidPathChars()));
        }

        /// <summary>Expands a Tank Icon Maker-style path, which may have expandable tokens like "VersionName".</summary>
        public static string ExpandPath(WotContext context, string path)
        {
            if (path == null)
                return null;
            path = path.Replace("\"VersionName\"", context.Installation.GameVersionName);
            if (path.Contains('"'))
                throw new Exception("The path “{0}” contains double-quote characters after expanding all known tokens. Did you mean one of: \"VersionName\"?".Fmt(path));
            return path;
        }

        /// <summary>Expands a Tank Icon Maker-style path, which may have expandable tokens like "VersionName".</summary>
        public static string ExpandIconPath(string path, WotContext context, Style style, WotTank tank,
            bool fragment = false, SaveType saveType = SaveType.Icons)
        {
            if (tank != null)
            {
                var country = tank.Country;
                var class_ = tank.Class;
                var tankId = tank.TankId;
                var fullName = tank.ClientData != null ? tank.ClientData.FullName : tank.TankId;
                var shortName = tank.ClientData != null ? tank.ClientData.ShortName : tank.TankId;
                var tier = tank.Tier;
                return ExpandIconPath(path, context, style,
                    country.Pick("ussr", "germany", "usa", "france", "china", "uk", "japan", "czech", "sweden", "poland", "italy", "intunion", "none"),
                    class_.Pick("light", "medium", "heavy", "destroyer", "artillery", "none"), tankId, fullName,
                    shortName, tier,
                    fragment, saveType);
            }
            else
            {
                return ExpandIconPath(path, context, style, "none", "none", "none", "none", "none", 0, fragment, saveType);
            }
        }

        /// <summary>Expands a Tank Icon Maker-style path, which may have expandable tokens like "VersionName".</summary>
        public static string ExpandIconPath(string path, WotContext context, Style style, Country country,
            Class class_, bool fragment = false, SaveType saveType = SaveType.Icons)
        {
            return ExpandIconPath(path, context, style,
                country.Pick("ussr", "germany", "usa", "france", "china", "uk", "japan", "czech", "sweden", "poland", "italy", "intunion", "none"),
                class_.Pick("light", "medium", "heavy", "destroyer", "artillery", "none"), fragment, saveType);
        }

        /// <summary>Expands a Tank Icon Maker-style path, which may have expandable tokens like "VersionName".</summary>
        public static string ExpandIconPath(string path, WotContext context, Style style, string country, string class_,
            bool fragment = false, SaveType saveType = SaveType.Icons)
        {
            return ExpandIconPath(path, context, style, country, class_, null, null, null, 0, fragment, saveType);
        }

        /// <summary>Expands a Tank Icon Maker-style path, which may have expandable tokens like "VersionName".</summary>
        public static string ExpandIconPath(string path, WotContext context, Style style, string country, string class_,
            string tankId, string tankFullName, string tankShortName, int tankTier, bool fragment = false, SaveType saveType = SaveType.Icons)
        {
            if (string.IsNullOrEmpty(path))
            {
                switch (saveType)
                {
                    case SaveType.Icons:
                        path = "{IconsPath}\\{TankId}{Ext}";
                        break;
                    case SaveType.BattleAtlas:
                        path = "{AtlasPath}\\" + AtlasBuilder.battleAtlas + ".png";
                        break;
                    case SaveType.VehicleMarkerAtlas:
                        path = "{AtlasPath}\\" + AtlasBuilder.vehicleMarkerAtlas + ".png";
                        break;
                    case SaveType.CustomAtlas:
                        path = "{AtlasPath}\\" + AtlasBuilder.customAtlas + ".png";
                        break;
                }
            }

            path = path.Replace("{IconsPath}", Ut.ExpandPath(context, context.VersionConfig.PathDestination) + @"\");
            path = path.Replace("{AtlasPath}", Ut.ExpandPath(context, context.VersionConfig.PathDestinationAtlas) + @"\");
            path = path.Replace("{TimPath}", PathUtil.AppPath + @"\");
            path = path.Replace("{GamePath}", context.Installation.Path + @"\");
            path = path.Replace("{GameVersion}", context.Installation.GameVersionName);
            if (class_ != null)
            {
                path = path.Replace("{TankClass}", class_);
            }

            if (country != null)
            {
                path = path.Replace("{TankCountry}", country);
            }

            if (tankId != null)
            {
                path = path.Replace("{TankId}", tankId);
            }

            if (tankFullName != null)
            {
                path = path.Replace("{TankFullName}", tankFullName);
            }

            if (tankShortName != null)
            {
                path = path.Replace("{TankShortName}", tankShortName);
            }
            path = path.Replace("{TankTier}", tankTier.ToString());

            path = path.Replace("{StyleName}", style.Name);
            path = path.Replace("{StyleAuthor}", style.Author);
            path = path.Replace("{Ext}", context.VersionConfig.TankIconExtension);
            path = Environment.ExpandEnvironmentVariables(path);
            path = path.Replace(@"\\", @"\").Replace(@"\\", @"\").Replace(@"\\", @"\");
            if (path.EndsWith(@"\") && !path.EndsWith(@":\"))
            {
                path = path.Substring(0, path.Length - 1);
            }

            return fragment ? path : Path.GetFullPath(Path.Combine(context.Installation.Path, path));
        }

        public static void RemoveWhere<T>(this ICollection<T> collection, Func<T, bool> predicate)
        {
            foreach (var item in collection.Where(predicate).ToList())
                collection.Remove(item);
        }

        /// <summary>Selects a string from the specified dictionary based on the current application language.</summary>
        public static string StringForCurrentLanguage(IDictionary<string, string> dictionary)
        {
            if (dictionary.Count == 0)
                return "";
            string lang;
            if (App.Translation.Language == Language.EnglishUK || App.Translation.Language == Language.EnglishUS)
                lang = "En";
            else
                lang = App.Translation.Language.GetIsoLanguageCode();
            if (dictionary.ContainsKey(lang))
                return dictionary[lang];
            else if (dictionary.ContainsKey("En"))
                return dictionary["En"];
            else
                return dictionary.Values.First();
        }

        /// <summary>
        /// Attempts to copy the specified text to clipboard, correctly handling the case where the clipboard may be
        /// temporarily locked by another application (such as TeamViewer). Waits for the clipboard to become available
        /// for up to 1 second; shows a dialog on failure with the option to retry.
        /// </summary>
        public static bool ClipboardSet(string text)
        {
            while (true)
            {
                for (int retries = 0; retries < 10; retries++)
                {
                    try { Clipboard.SetText(text, TextDataFormat.UnicodeText); return true; }
                    catch { Thread.Sleep(100); }
                }
                if (1 == DlgMessage.ShowWarning(App.Translation.Error.ClipboardError, App.Translation.Error.ClipboardError_Retry, App.Translation.Prompt.Cancel))
                    return false;
            }
        }

        /// <summary>
        /// Constructs a string describing an exception for the purpose of sending an error report to developers.
        /// </summary>
        public static string ExceptionToDebugString(object exception)
        {
            var errorInfo = new StringBuilder("Thread: " + Thread.CurrentThread.Name + "\n");
            while (exception != null)
            {
                errorInfo.AppendFormat("\nException: {0}", exception.GetType());
                var excp = exception as Exception;
                if (excp != null)
                {
                    errorInfo.AppendFormat("\nMessage: {0}\n", excp.Message);
                    errorInfo.AppendLine(Ut.CollapseStackTrace(excp.StackTrace));
                    exception = excp.InnerException;
                }
            }
            return errorInfo.ToString();
        }
    }

    /// <summary>
    /// Enables scheduling tasks to execute in a thread of a different priority than Normal. Only non-critical tasks
    /// should be scheduled using this scheduler because any remaining queued tasks will be dropped on program exit.
    /// </summary>
    public class PriorityScheduler : TaskScheduler
    {
        // http://stackoverflow.com/a/9056702/33080
        public static PriorityScheduler AboveNormal = new PriorityScheduler(ThreadPriority.AboveNormal);
        public static PriorityScheduler BelowNormal = new PriorityScheduler(ThreadPriority.BelowNormal);
        public static PriorityScheduler Lowest = new PriorityScheduler(ThreadPriority.Lowest);

        private BlockingCollection<Task> _tasks = new BlockingCollection<Task>();
        private Thread[] _threads;
        private ThreadPriority _priority;
        private readonly int _maximumConcurrencyLevel = Math.Max(1, Environment.ProcessorCount);

        public PriorityScheduler(ThreadPriority priority)
        {
            _priority = priority;
        }

        public override int MaximumConcurrencyLevel
        {
            get { return _maximumConcurrencyLevel; }
        }

        protected override IEnumerable<Task> GetScheduledTasks()
        {
            return _tasks;
        }

        protected override void QueueTask(Task task)
        {
            _tasks.Add(task);

            if (_threads == null)
            {
                _threads = new Thread[_maximumConcurrencyLevel];
                for (int i = 0; i < _threads.Length; i++)
                {
                    _threads[i] = new Thread(() =>
                    {
                        foreach (Task t in _tasks.GetConsumingEnumerable())
                            base.TryExecuteTask(t);
                    });
                    _threads[i].Name = string.Format("PriorityScheduler: {0}", i);
                    _threads[i].Priority = _priority;
                    _threads[i].IsBackground = true;
                    _threads[i].Start();
                }
            }
        }

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            return false; // we might not want to execute task that should schedule as high or low priority inline
        }
    }

    sealed class TypeInfo<T>
    {
        public Type Type;
        public Func<T> Constructor;
        public string Name { get; set; }
        public string Description { get; set; }
    }

    interface IHasTypeNameDescription
    {
        string TypeName { get; }
        string TypeDescription { get; }
    }

    /// <summary>
    /// Encapsulates a file name which may refer to a file inside a container file, or just to a file directly. The parts
    /// are separated with a "|". Helps avoid issues with Path.Combine complaining about invalid filenames.
    /// </summary>
    struct CompositePath
    {
        /// <summary>The main path part.</summary>
        public string File { get; private set; }
        /// <summary>The inner path part, where present.</summary>
        public string InnerFile { get; private set; }

        public override string ToString() { return File + (InnerFile == null ? "" : ("|" + InnerFile)); }

        public CompositePath(WotContext context, params string[] path)
            : this()
        {
            var builder = new StringBuilder(256);
            string first = null;
            foreach (var part in path)
            {
                int colon = part.IndexOf(':');
                int pipe = part.IndexOf('|');
                if (colon != -1 && colon != 1)
                    throw new StyleUserError("Invalid composite file path: \":\" is only allowed in a drive letter specification at the start of the path.");
                if (pipe != -1 && (colon > pipe || first != null))
                    throw new StyleUserError("Invalid composite file path: \":\" is not allowed in the path anywhere after the \"|\".");
                // now the colon is either absent or at the right place, in the first half of the composite path
                if (pipe != -1 && first != null)
                    throw new StyleUserError("Invalid composite file path: \"|\" must not occur more than once.");
                // now we know that the colon and pipe characters are used correctly in the path so far

                if (colon > 0 || part.StartsWith("/") || part.StartsWith("\\"))
                    builder.Clear();
                else if (builder.Length > 0 && builder[builder.Length - 1] != '/' && builder[builder.Length - 1] != '\\')
                    builder.Append('\\');

                if (pipe == -1)
                    builder.Append(part);
                else
                {
                    builder.Append(part.Substring(0, pipe));
                    first = builder.ToString();
                    builder.Clear();
                    builder.Append(part.Substring(pipe + 1));
                }
            }
            var second = builder.ToString();
            if (context != null)
            {
                File = Ut.ExpandPath(context, first ?? second);
                InnerFile = first == null ? null : Ut.ExpandPath(context, second);
            }
            else
            {
                // Used for testing only
                File = first ?? second;
                InnerFile = first == null ? null : second;
            }
        }

        #region Tests

        internal static void Tests()
        {
            test(new CompositePath(null, @""), @"", null);
            test(new CompositePath(null, @"foo"), @"foo", null);
            test(new CompositePath(null, @"foo/bar"), @"foo/bar", null);
            test(new CompositePath(null, @"\foo\bar"), @"\foo\bar", null);
            test(new CompositePath(null, @"C:\foo\bar"), @"C:\foo\bar", null);

            test(new CompositePath(null, @"foo\bar", @"thingy\blah"), @"foo\bar\thingy\blah", null);
            test(new CompositePath(null, @"foo\bar\", @"thingy\blah"), @"foo\bar\thingy\blah", null);
            test(new CompositePath(null, @"foo\bar", @"thingy\blah", @"stuff"), @"foo\bar\thingy\blah\stuff", null);
            test(new CompositePath(null, @"foo\bar", @"thingy\blah", @"D:\stuff"), @"D:\stuff", null);

            test(new CompositePath(null, @"C:\foo\bar", @"thingy"), @"C:\foo\bar\thingy", null);
            test(new CompositePath(null, @"C:\foo\bar", @"thingy", @"stuff"), @"C:\foo\bar\thingy\stuff", null);
            test(new CompositePath(null, @"C:\foo\bar", @"thingy", @"D:\stuff"), @"D:\stuff", null);


            test(new CompositePath(null, @"|"), @"", @"");
            test(new CompositePath(null, @"fo|o"), @"fo", @"o");
            test(new CompositePath(null, @"fo|o/bar"), @"fo", @"o/bar");
            test(new CompositePath(null, @"foo/b|ar"), @"foo/b", @"ar");
            test(new CompositePath(null, @"C:\fo|o\bar"), @"C:\fo", @"o\bar");
            test(new CompositePath(null, @"C:\foo\b|ar"), @"C:\foo\b", @"ar");

            test(new CompositePath(null, @"foo\b|ar", @"thingy\blah"), @"foo\b", @"ar\thingy\blah");
            test(new CompositePath(null, @"foo\b|ar\", @"thingy\blah"), @"foo\b", @"ar\thingy\blah");
            test(new CompositePath(null, @"foo\b|ar", @"thingy\blah", @"stuff"), @"foo\b", @"ar\thingy\blah\stuff");
            test(new CompositePath(null, @"D:\foo\b|ar", @"thingy\blah", @"stuff"), @"D:\foo\b", @"ar\thingy\blah\stuff");

            test(new CompositePath(null, @"foo\bar", @"thin|gy\blah"), @"foo\bar\thin", @"gy\blah");
            test(new CompositePath(null, @"foo\bar\", @"thin|gy\blah"), @"foo\bar\thin", @"gy\blah");
            test(new CompositePath(null, @"foo\bar", @"thin|gy\blah", @"stuff"), @"foo\bar\thin", @"gy\blah\stuff");
            test(new CompositePath(null, @"foo\bar", @"D:\thin|gy\blah", @"stuff"), @"D:\thin", @"gy\blah\stuff");

            test(new CompositePath(null, @"foo\bar", @"thingy\blah", @"stu|ff"), @"foo\bar\thingy\blah\stu", @"ff");
            test(new CompositePath(null, @"foo\bar", @"thingy\blah", @"D:\stu|ff"), @"D:\stu", @"ff");

            test(new CompositePath(null, @"C:\fo|o\bar", @"thingy"), @"C:\fo", @"o\bar\thingy");
            test(new CompositePath(null, @"C:\foo\bar", @"thin|gy"), @"C:\foo\bar\thin", @"gy");
            test(new CompositePath(null, @"C:\fo|o\bar", @"thingy", @"stuff"), @"C:\fo", @"o\bar\thingy\stuff");
            test(new CompositePath(null, @"C:\foo\bar", @"thin|gy", @"stuff"), @"C:\foo\bar\thin", @"gy\stuff");
            test(new CompositePath(null, @"C:\foo\bar", @"thingy", @"stu|ff"), @"C:\foo\bar\thingy\stu", @"ff");
            test(new CompositePath(null, @"C:\foo\bar", @"thingy", @"D:\stu|ff"), @"D:\stu", @"ff");
        }

        private static void test(CompositePath cf, string expectedPath, string expectedInnerPath)
        {
            if (cf.File != expectedPath || cf.InnerFile != expectedInnerPath)
                throw new Exception("CompositePath test failed.");
        }

        #endregion
    }

    [TypeConverter(typeof(BoolWithPassthroughTranslation.Conv))]
    enum BoolWithPassthrough
    {
        No,
        Yes,
        Passthrough,
    }
}
