﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using RT.Util.ExtensionMethods;
using RT.Util.Lingo;
using RT.Util.Xml;

namespace TankIconMaker
{
    /// <summary>
    /// Encapsulates all the available information about a tank to be rendered. Exposes methods to retrieve standard images for this tank.
    /// </summary>
    class Tank
    {
        /// <summary>WoT seems to consistently identify tanks by this string, which is unique for each tank and is also used in many filenames.</summary>
        public string SystemId { get; protected set; }
        /// <summary>Tank's country.</summary>
        public Country Country { get; protected set; }
        /// <summary>Tank's tier.</summary>
        public int Tier { get; protected set; }
        /// <summary>Tank's class: light/medium/heavy tank, artillery or tank destroyer.</summary>
        public Class Class { get; protected set; }
        /// <summary>Tank's category: normal (buyable for silver), premium (buyable for gold), or special (not for sale).</summary>
        public Category Category { get; protected set; }

        private Dictionary<ExtraPropertyId, string> _extras;

        private GameInstallationSettings _gameInstall;
        private GameVersion _gameVersion;
        private Action<string> _addWarning;

        protected Tank() { }

        /// <summary>Creates an instance ready to pass on to a Maker.</summary>
        /// <param name="tank">Tank data to copy into this instance.</param>
        /// <param name="extras">All the extra properties available for this tank and this game version.</param>
        /// <param name="gameInstall">Game install settings (to allow loading standard tank images).</param>
        /// <param name="gameVersion">Game version info (to allow loading standard tank images).</param>
        /// <param name="addWarning">The method to be used to add warnings about this tank's rendering.</param>
        public Tank(TankData tank, IEnumerable<KeyValuePair<ExtraPropertyId, string>> extras,
            GameInstallationSettings gameInstall, GameVersion gameVersion, Action<string> addWarning)
        {
            if (gameInstall == null || gameVersion == null) throw new ArgumentNullException();
            SystemId = tank.SystemId;
            Country = tank.Country;
            Tier = tank.Tier;
            Class = tank.Class;
            Category = tank.Category;
            _extras = new Dictionary<ExtraPropertyId, string>();
            if (extras != null)
                foreach (var extra in extras)
                    _extras.Add(extra.Key, extra.Value);
            _gameInstall = gameInstall;
            _gameVersion = gameVersion;
            _addWarning = addWarning;
        }

        /// <summary>
        /// Gets the value of an "extra" property. This getter takes the same values that  the <see cref="DataSourceEditor"/> drop-down uses.
        /// If the referenced property doesn't exist, a null value is returned. The maker must not crash just because some data files are missing,
        /// and hence must handle these nulls properly.
        /// </summary>
        public virtual string this[ExtraPropertyId property]
        {
            get
            {
                if (property.Equals(ExtraPropertyId.TierArabic))
                    return Tier.ToString();
                else if (property.Equals(ExtraPropertyId.TierRoman))
                    return Ut.RomanNumerals[Tier].ToString();
                string result;
                if (property == null || !_extras.TryGetValue(property, out result))
                    return null;
                return result;
            }
        }

        /// <summary>
        /// Gets the value of an "extra" property by property name. This is suitable for quick hacks or development/testing. The property
        /// will prefer the currently selected language and the author specified in <see cref="Settings"/>, but will fall back onto other
        /// languages and authors if necessary. If no matching property can be found, a null value is returned. The maker must not crash
        /// just because some data files are missing, and hence must handle these nulls properly.
        /// </summary>
        public virtual string this[string name]
        {
            get
            {
                if (name == null)
                    return null;
                var matches = _extras.Keys.Where(k => k.Name.EqualsNoCase(name)).ToArray();
                if (matches.Length == 0)
                    return null;
                if (matches.Length == 1)
                    return _extras[matches[0]];
                // Otherwise need to pick one
                var match = matches.FirstOrDefault(k => k.Language.EqualsNoCase(App.Settings.Lingo.GetIsoLanguageCode()) && k.Author.EqualsNoCase(App.Settings.DefaultPropertyAuthor));
                if (match != null) return _extras[match];
                match = matches.FirstOrDefault(k => k.Language.EqualsNoCase(App.Settings.Lingo.GetIsoLanguageCode()));
                if (match != null) return _extras[match];
                return _extras[matches[0]];
            }
        }

        /// <summary>
        /// Adds a warning about this tank's rendering. The user will see a big warning icon telling them to look for warnings on specific tanks,
        /// and each image with warnings will have a little warning icon shown in it.
        /// </summary>
        public virtual void AddWarning(string warning)
        {
            _addWarning(warning);
        }

        /// <summary>For debugging.</summary>
        public override string ToString()
        {
            return "Tank: " + SystemId;
        }

        /// <summary>Gets a built-in image for this tank. Returns null if the image file does not exist. Throws on format errors.</summary>
        public virtual BitmapBase GetImageBuiltIn(ImageBuiltInStyle style)
        {
            switch (style)
            {
                case ImageBuiltInStyle.Contour:
                    return ImageCache.GetImage(new CompositeFilename(_gameInstall.Path, _gameVersion.PathSourceContour, SystemId + _gameVersion.TankIconExtension));
                case ImageBuiltInStyle.ThreeD:
                    return ImageCache.GetImage(new CompositeFilename(_gameInstall.Path, _gameVersion.PathSource3D, SystemId + _gameVersion.TankIconExtension));
                case ImageBuiltInStyle.ThreeDLarge:
                    return ImageCache.GetImage(new CompositeFilename(_gameInstall.Path, _gameVersion.PathSource3DLarge, SystemId + _gameVersion.TankIconExtension));
                case ImageBuiltInStyle.Country:
                    return ImageCache.GetImage(new CompositeFilename(_gameInstall.Path, _gameVersion.PathSourceCountry[Country]));
                case ImageBuiltInStyle.Class:
                    return ImageCache.GetImage(new CompositeFilename(_gameInstall.Path, _gameVersion.PathSourceClass[Class]));
                default:
                    throw new Exception("9174876");
            }
        }

        /// <summary>Gets the currently saved icon image for this tank. Returns null if the image file does not exist. Throws on format errors.</summary>
        public virtual BitmapBase GetImageCurrent()
        {
            return ImageCache.GetImage(new CompositeFilename(_gameInstall.Path, _gameVersion.PathDestination, SystemId + _gameVersion.TankIconExtension))
                ?? GetImageBuiltIn(ImageBuiltInStyle.Contour);
        }
    }

    /// <summary>Used to test makers for bugs in handling missing data.</summary>
    class TankTest : Tank
    {
        /// <summary>Constructor.</summary>
        public TankTest(string systemId, int tier, Country country, Class class_, Category category)
        {
            SystemId = systemId;
            Tier = tier;
            Country = country;
            Class = class_;
            Category = category;
        }

        public string PropertyValue;
        public BitmapBase LoadedImage;

        public override string this[string name] { get { return PropertyValue; } }
        public override string this[ExtraPropertyId property] { get { return PropertyValue; } }
        public override BitmapBase GetImageBuiltIn(ImageBuiltInStyle style) { return LoadedImage; }
        public override BitmapBase GetImageCurrent() { return LoadedImage; }
        public override void AddWarning(string warning) { }
    }

    /// <summary>
    /// Encapsulates information about a tank read from the built-in data file. This is a separate type from <see cref="Tank"/> to ensure
    /// that it is not accidentally passed on to a maker without filling out the extra properties or linking to a game version/install.
    /// </summary>
    class TankData : Tank
    {
        /// <summary>Parses a CSV row from a built-in data file.</summary>
        public TankData(string[] fields)
        {
            if (fields.Length < 5)
                throw new Exception(App.Translation.Error.DataFile_TooFewFields.Fmt(App.Translation, 5));

            SystemId = fields[0];

            switch (fields[1])
            {
                case "ussr": Country = Country.USSR; break;
                case "germany": Country = Country.Germany; break;
                case "usa": Country = Country.USA; break;
                case "china": Country = Country.China; break;
                case "france": Country = Country.France; break;
                case "uk": Country = Country.UK; break;
                default: throw new Exception(App.Translation.Error.DataFile_UnrecognizedCountry.Fmt(fields[1],
                    new[] { "ussr", "germany", "usa", "china", "france", "uk" }.JoinString(", ", "\"", "\"")));
            }

            int tier;
            if (!int.TryParse(fields[2], out tier))
                throw new Exception(string.Format(App.Translation.Error.DataFile_TankTierValue, fields[2]));
            if (tier < 1 || tier > 10)
                throw new Exception(string.Format(App.Translation.Error.DataFile_TankTierValue, fields[2]));
            Tier = tier;

            switch (fields[3])
            {
                case "light": Class = Class.Light; break;
                case "medium": Class = Class.Medium; break;
                case "heavy": Class = Class.Heavy; break;
                case "destroyer": Class = Class.Destroyer; break;
                case "artillery": Class = Class.Artillery; break;
                default: throw new Exception(App.Translation.Error.DataFile_UnrecognizedClass.Fmt(fields[3],
                    new[] { "light", "medium", "heavy", "destroyer", "artillery" }.JoinString(", ", "\"", "\"")));
            }

            switch (fields[4])
            {
                case "normal": Category = Category.Normal; break;
                case "premium": Category = Category.Premium; break;
                case "special": Category = Category.Special; break;
                default: throw new Exception(App.Translation.Error.DataFile_UnrecognizedCategory.Fmt(fields[4],
                    new[] { "normal", "premium", "special" }.JoinString(", ", "\"", "\"")));
            }
        }
    }

    /// <summary>
    /// Represents a built-in data file, including its properties and the actual data it holds.
    /// </summary>
    class DataFileBuiltIn
    {
        /// <summary>Which game version this data file was made for.</summary>
        public int GameVersion { get; private set; }
        /// <summary>The file version of this file.</summary>
        public int FileVersion { get; private set; }

        /// <summary>All the data held in this data file. Note that this list is read-only.</summary>
        public IList<TankData> Data { get; private set; }

        /// <summary>For debugging.</summary>
        public override string ToString()
        {
            return "BuiltIn-#{0:0000}-{1}".Fmt(GameVersion, FileVersion);
        }

        /// <summary>Constructs an instance and parses the data from the specified file.</summary>
        public DataFileBuiltIn(int gameVersion, int fileVersion, string filename)
        {
            GameVersion = gameVersion;
            FileVersion = fileVersion;

            var lines = Ut.ReadCsvLines(filename).ToArray();
            if (lines.Length == 0)
                throw new Exception(App.Translation.Error.DataFile_EmptyFile);
            var header = lines[0].Item2;
            if (header.Length < 2)
                throw new Exception(App.Translation.Error.DataFile_TooFewFieldsFirstLine);
            if (header[0] != "WOT-BUILTIN-DATA")
                throw new Exception(App.Translation.Error.DataFile_ExpectedSignature.Fmt("WOT-BUILTIN-DATA"));
            if (header[1] != "1")
                throw new Exception(App.Translation.Error.DataFile_ExpectedV1);

            Data = lines.Skip(1).Select(lp =>
            {
                try { return new TankData(lp.Item2); }
                catch (Exception e) { throw new Exception(App.Translation.Error.DataFile_LineNum.Fmt(lp.Item1, e.Message)); }
            }).ToList().AsReadOnly();
        }

        /// <summary>Constructs an instance using the specified parameters and data.</summary>
        public DataFileBuiltIn(int gameVersion, int fileVersion, IEnumerable<TankData> data)
        {
            GameVersion = gameVersion;
            FileVersion = fileVersion;
            Data = data.ToList().AsReadOnly();
        }
    }

    /// <summary>
    /// Represents an "extra" property data file, including its properties and the actual data it holds.
    /// Note: extra data files come with complex inheritance rules, documented on the project website.
    /// </summary>
    class DataFileExtra
    {
        public string Name { get; private set; }
        public string Language { get; private set; }
        public string Author { get; private set; }
        public int GameVersion { get; private set; }
        public int FileVersion { get; private set; }
        public string Description { get; private set; }
        public string InheritsFromName { get; private set; }
        public string InheritsFromAuthor { get; private set; }
        public string InheritsFromLanguage { get; private set; }

        public IList<ExtraData> Data { get; private set; }

        public override string ToString()
        {
            return "{0}-{1}-{2}-{3}-{4}".Fmt(Name, Language, Author, GameVersion, FileVersion);
        }

        public DataFileExtra(string name, string language, string author, int gameVersion, int fileVersion, string filename)
        {
            Name = name;
            Language = language;
            Author = author;
            GameVersion = gameVersion;
            FileVersion = fileVersion;

            var lines = Ut.ReadCsvLines(filename).ToArray();
            if (lines.Length == 0)
                throw new Exception(App.Translation.Error.DataFile_EmptyFile);
            var header = lines[0].Item2;
            if (header.Length < 2)
                throw new Exception(App.Translation.Error.DataFile_TooFewFieldsFirstLine);
            if (header[0] != "WOT-DATA")
                throw new Exception(App.Translation.Error.DataFile_ExpectedSignature.Fmt("WOT-DATA"));
            if (header[1] != "1")
                throw new Exception(App.Translation.Error.DataFile_ExpectedV1);
            if (header.Length >= 3)
                Description = header[2];
            if (header.Length >= 4)
                InheritsFromName = header[3];
            if (header.Length >= 5)
                InheritsFromAuthor = header[4];
            if (header.Length >= 6)
                InheritsFromLanguage = header[5];

            if (InheritsFromName == "")
                InheritsFromName = null;
            if (InheritsFromAuthor == "")
                InheritsFromAuthor = null;
            if (InheritsFromLanguage == "")
                InheritsFromLanguage = null;

            Data = lines.Skip(1).Select(lp =>
            {
                try { return new ExtraData(lp.Item2); }
                catch (Exception e) { throw new Exception(App.Translation.Error.DataFile_LineNum.Fmt(lp.Item1, e.Message)); }
            }).ToList().AsReadOnly();
        }

        public DataFileExtra(DataFileExtra properties, IEnumerable<ExtraData> data)
        {
            Name = properties.Name;
            Language = properties.Language;
            Author = properties.Author;
            GameVersion = properties.GameVersion;
            FileVersion = properties.FileVersion;
            Description = properties.Description;
            InheritsFromName = properties.InheritsFromName;
            InheritsFromAuthor = properties.InheritsFromAuthor;
            InheritsFromLanguage = properties.InheritsFromLanguage;
            Data = data.ToList().AsReadOnly();
        }

        protected DataFileExtra(string name, string language, string author, int gameVersion, string description, string inhName, string inhAuthor, string inhLanguage)
        {
            Name = name;
            Language = language;
            Author = author;
            GameVersion = gameVersion;
            FileVersion = 1;
            Description = description;
            InheritsFromName = inhName;
            InheritsFromAuthor = inhAuthor;
            InheritsFromLanguage = inhLanguage;
            Data = new List<ExtraData>().AsReadOnly();
        }
    }

    /// <summary>
    /// Represents a single value of an "extra" property.
    /// </summary>
    class ExtraData
    {
        /// <summary>System Id of the tank that this property is for.</summary>
        public string TankSystemId { get; private set; }
        /// <summary>Property value; can be anything at all.</summary>
        public string Value { get; private set; }

        /// <summary>Parses an "extra" property from an extra data file CSV row.</summary>
        public ExtraData(string[] fields)
        {
            if (fields.Length < 2)
                throw new Exception(App.Translation.Error.DataFile_TooFewFields.Fmt(App.Translation, 2));
            TankSystemId = fields[0];
            Value = fields[1];
        }

        /// <summary>For debugging.</summary>
        public override string ToString()
        {
            return "{0} = {1}".Fmt(TankSystemId, Value);
        }
    }

    /// <summary>
    /// Identifies an "extra" property. Suitable for use as dictionary keys.
    /// </summary>
    sealed class ExtraPropertyId : IEquatable<ExtraPropertyId>, IXmlClassifyProcess
    {
        public string Name { get; private set; }
        public string Language { get; private set; }
        public string Author { get; private set; }

        public static readonly ExtraPropertyId TierArabic = new ExtraPropertyId { Name = "Tier (Arabic)", Author = "(built-in)" };
        public static readonly ExtraPropertyId TierRoman = new ExtraPropertyId { Name = "Tier (Roman)", Author = "(built-in)" };

        public ExtraPropertyId(string name, string language, string author)
        {
            if (name == null || language == null || author == null)
                throw new ArgumentNullException();
            Name = name;
            Language = language;
            Author = author;
        }

        private ExtraPropertyId() { } // for XmlClassify

        public override bool Equals(object obj) { return Equals(obj as ExtraPropertyId); }

        public bool Equals(ExtraPropertyId other)
        {
            return other != null && Name == other.Name && Language == other.Language && Author == other.Author;
        }

        [XmlIgnore]
        private int _hash = 0;

        public override int GetHashCode()
        {
            if (_hash == 0)
            {
                _hash = unchecked((Name ?? "").GetHashCode() + (Language ?? "").GetHashCode() * 1049 + (Author ?? "").GetHashCode() * 5507);
                if (_hash == 0)
                    _hash = 1;
            }
            return _hash;
        }

        public override string ToString() { return Name + "/" + Language + "/" + Author; }

        void IXmlClassifyProcess.AfterXmlDeclassify()
        {
            if (Name == "NameShortWG" && Author == "Romkyns")
            {
                Name = "NameShort";
                Author = "Wargaming";
            }
            else if (Name == "NameFullWG" && Author == "Romkyns")
            {
                Name = "NameFull";
                Author = "Wargaming";
            }
            else if (Name == "NameImproved" && Author == "Romkyns")
            {
                Name = "NameShort";
            }
        }

        void IXmlClassifyProcess.BeforeXmlClassify()
        {
        }
    }

    /// <summary>
    /// Encapsulates all of the World of Tanks data available to the program, and implements methods
    /// to load the data off disk and retrieve a list of warnings indicating problems with the data.
    /// </summary>
    sealed class WotData : INotifyPropertyChanged
    {
        /// <summary>
        /// Gets a list of all the built-in data files available. Each file contains all the tanks, including those
        /// inherited from earlier game version files. This list is read-only.
        /// </summary>
        public IList<DataFileBuiltIn> BuiltIn { get { return _builtIn.AsReadOnly(); } }
        private readonly List<DataFileBuiltIn> _builtIn = new List<DataFileBuiltIn>();

        /// <summary>
        /// Gets a list of all the extra property data files available. Each file contains all the properties,
        /// including those it inherited from other files. This list is read-only.
        /// </summary>
        public IList<DataFileExtra> Extra { get { return _extra.AsReadOnly(); } }
        private readonly List<DataFileExtra> _extra = new List<DataFileExtra>();

        /// <summary>
        /// Gets a dictionary of all the game version information available. This dictionary is read-only.
        /// </summary>
        public IList<GameVersion> Versions { get { return _versions.AsReadOnly(); } }
        private readonly List<GameVersion> _versions = new List<GameVersion>();

        /// <summary>
        /// Gets a value indicating whether the bare minimum of files are available.
        /// </summary>
        public bool FilesAvailable { get; private set; }

        /// <summary>
        /// Gets a list of warnings issued while loading the data. These can be serious and help understand
        /// why data might be missing. This list is read-only.
        /// </summary>
        public IList<string> Warnings { get { return _warnings.AsReadOnly(); } }
        private List<string> _warnings = new List<string>();

        /// <summary>
        /// Gets game version information for a specific game version. Returns null if there is no exact match for this version.
        /// </summary>
        public GameVersion GetVersion(VersionId version)
        {
            return _versions.SingleOrDefault(v => v.Version == version);
        }

        /// <summary>Gets game version information for the latest defined version. Returns null if none are defined.</summary>
        public GameVersion GetLatestVersion()
        {
            GameVersion max = null;
            foreach (var gv in _versions)
                if (max == null || gv.Version > max.Version)
                    max = gv;
            return max;
        }

        /// <summary>Gets game version information for whichever version appears to be installed at the specified path (or latest version if can’t determine that).</summary>
        public GameVersion GetGuessedVersion(string path)
        {
            return _versions
                .Where(v => File.Exists(Path.Combine(path, v.CheckFileName)) && Ut.FileContains(Path.Combine(path, v.CheckFileName), v.CheckFileContent))
                .OrderByDescending(v => v.Version)
                .FirstOrDefault() ?? GetLatestVersion();
        }

        /// <summary>Represents an "extra" data file during loading, including all the extra info required to properly resolve inheritance.</summary>
        private class DataFileExtra2 : DataFileExtra
        {
            public List<DataFileExtra2> ImmediateParents = new List<DataFileExtra2>();
            public HashSet<DataFileExtra2> TransitiveChildren = new HashSet<DataFileExtra2>();
            public int NearestRoot;
            public DataFileExtra Result;

            public DataFileExtra2(string name, string language, string author, int gameVersion, int fileVersion, string filename)
                : base(name, language, author, gameVersion, fileVersion, filename) { }

            public DataFileExtra2(string name, string language, string author, int gameVersion, string description, string inhName, string inhAuthor, string inhLanguage)
                : base(name, language, author, gameVersion, description, inhName, inhAuthor, inhLanguage) { }
        }

        /// <summary>Clears all the data and performs a fresh load off disk.</summary>
        public void Reload(string path)
        {
            _builtIn.Clear();
            _extra.Clear();
            _versions.Clear();
            _warnings.Clear();

            if (Directory.Exists(path))
            {
                readGameVersions(path);
                readDataFiles(path);
            }

            // Remove versions that have no built-in data files available
            if (_builtIn.Any() && _versions.Any())
            {
                var minBuiltin = _builtIn.Min(b => b.GameVersion);
                foreach (var version in _versions.Where(v => v.Version < minBuiltin).ToArray())
                {
                    _versions.Remove(version);
                    _warnings.Add(App.Translation.Error.DataDir_Skip_NoBuiltIns.Fmt(version, "Data\\GameVersion-{0}.xml".Fmt(version)));
                }
            }

            FilesAvailable = _builtIn.Any() && _versions.Any();
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs("FilesAvailable"));

            if (!FilesAvailable)
                _warnings.Add(App.Translation.Error.DataDir_NoFilesAvailable);
        }

        private void readGameVersions(string path)
        {
            foreach (var fi in new DirectoryInfo(path).GetFiles("GameVersion-*.xml"))
            {
                var parts = fi.Name.Substring(0, fi.Name.Length - 4).Split('-');

                if (parts.Length != 2)
                {
                    _warnings.Add(App.Translation.Error.DataDir_Skip_WrongParts.Fmt(fi.Name, "2", parts.Length));
                    continue;
                }

                int gameVersion;
                if (!parts[1].StartsWith("#") || !int.TryParse(parts[1].Substring(1), out gameVersion))
                {
                    _warnings.Add(App.Translation.Error.DataDir_Skip_GameVersion.Fmt(fi.Name, parts[1]));
                    continue;
                }

                try
                {
                    var ver = XmlClassify.LoadObjectFromXmlFile<GameVersion>(fi.FullName);
                    ver.Version = new VersionId(gameVersion, "");
                    _versions.Add(ver);
                }
                catch (Exception e)
                {
                    _warnings.Add(App.Translation.Error.DataDir_Skip_FileError.Fmt(fi.Name, e.Message));
                    continue;
                }
            }
        }

        private void readDataFiles(string path)
        {
            var builtin = new List<DataFileBuiltIn>();
            var extra = new List<DataFileExtra2>();
            var origFilenames = new Dictionary<object, string>();
            foreach (var fi in new DirectoryInfo(path).GetFiles("Data-*.csv"))
            {
                var parts = fi.Name.Substring(0, fi.Name.Length - 4).Split('-');
                var partsr = parts.Reverse().ToArray();

                if (parts.Length != 4 && parts.Length != 6)
                {
                    _warnings.Add(App.Translation.Error.DataDir_Skip_WrongParts.Fmt(fi.Name, "4/6", parts.Length));
                    continue;
                }
                if (parts[1].EqualsNoCase("BuiltIn") && parts.Length != 4)
                {
                    _warnings.Add(App.Translation.Error.DataDir_Skip_WrongParts.Fmt(fi.Name, "4", parts.Length));
                    continue;
                }
                if (parts.Length == 4 && !parts[1].EqualsNoCase("BuiltIn"))
                {
                    _warnings.Add(App.Translation.Error.DataDir_Skip_WrongParts.Fmt(fi.Name, "6", parts.Length));
                    continue;
                }

                int gameVersion;
                if (!partsr[1].StartsWith("#") || !int.TryParse(partsr[1].Substring(1), out gameVersion))
                {
                    _warnings.Add(App.Translation.Error.DataDir_Skip_GameVersion.Fmt(fi.Name, partsr[1]));
                    continue;
                }

                int fileVersion;
                if (!int.TryParse(partsr[0], out fileVersion))
                {
                    _warnings.Add(App.Translation.Error.DataDir_Skip_FileVersion.Fmt(fi.Name, partsr[0]));
                    continue;
                }

                if (parts.Length == 4)
                {
                    try
                    {
                        var df = new DataFileBuiltIn(gameVersion, fileVersion, fi.FullName);
                        builtin.Add(df);
                        origFilenames[df] = fi.Name;
                    }
                    catch (Exception e)
                    {
                        _warnings.Add(App.Translation.Error.DataDir_Skip_FileError.Fmt(fi.Name, e.Message));
                        continue;
                    }
                }
                else
                {
                    string author = partsr[2].Trim();
                    if (author.Length == 0)
                    {
                        _warnings.Add(App.Translation.Error.DataDir_Skip_Author.Fmt(fi.Name));
                        continue;
                    }

                    string extraName = parts[1].Trim();
                    if (extraName.Length == 0)
                    {
                        _warnings.Add(App.Translation.Error.DataDir_Skip_PropName.Fmt(fi.Name));
                        continue;
                    }

                    string languageName = parts[2].Trim();
                    if (languageName != "X" && languageName != "En" && Lingo.LanguageFromIsoCode(languageName.ToLowerInvariant()) == null)
                    {
                        _warnings.Add(App.Translation.Error.DataDir_Skip_Lang.Fmt(fi.Name, languageName));
                        continue;
                    }

                    try
                    {
                        var df = new DataFileExtra2(extraName, languageName, author, gameVersion, fileVersion, fi.FullName);
                        extra.Add(df);
                        origFilenames[df] = fi.Name;
                    }
                    catch (Exception e)
                    {
                        _warnings.Add(App.Translation.Error.DataDir_Skip_FileError.Fmt(fi.Name, e.Message));
                        continue;
                    }
                }
            }

            // Conjure up blank "extra" files for all "missing" versions
            var versions = builtin.Select(f => f.GameVersion).Concat(extra.Select(f => f.GameVersion)).Distinct().OrderBy(v => v).ToArray();
            foreach (var ex in extra.ToArray())
                foreach (var ver in versions.Where(v => v > ex.GameVersion))
                    if (!extra.Any(e => e.Name == ex.Name && e.Language == ex.Language && e.Author == ex.Author && e.GameVersion == ver))
                    {
                        var newextra = new DataFileExtra2(ex.Name, ex.Language, ex.Author, ver, ex.Description,
                            ex.InheritsFromName, ex.InheritsFromAuthor, ex.InheritsFromLanguage);
                        extra.Add(newextra);
                        origFilenames[newextra] = "(none)";
                    }

            resolveBuiltIn(builtin);
            resolveExtras(extra, origFilenames);
        }

        private void resolveBuiltIn(List<DataFileBuiltIn> builtin)
        {
            foreach (var group in builtin.GroupBy(df => new { gamever = df.GameVersion }).OrderBy(g => g.Key.gamever))
            {
                var tanks = new Dictionary<string, TankData>();

                // Inherit from the earlier game versions
                var earlierVer = _builtIn.OrderByDescending(df => df.GameVersion).FirstOrDefault();
                if (earlierVer != null)
                    foreach (var row in earlierVer.Data)
                        tanks[row.SystemId] = row;

                // Inherit from all the file versions for this game version
                foreach (var row in group.OrderBy(df => df.FileVersion).SelectMany(df => df.Data))
                    tanks[row.SystemId] = row;

                // Create a new data file with all the tanks
                _builtIn.Add(new DataFileBuiltIn(group.Key.gamever, group.Max(df => df.FileVersion), tanks.Values));
            }
        }

        private void resolveExtras(List<DataFileExtra2> extra, Dictionary<object, string> origFilenames)
        {
            while (true) // circular dependency removal requires us to iterate again if something got removed
            {
                // Make sure the explicit inheritance is resolvable, and complain if not
                var ignore = new List<DataFileExtra>();
                do
                {
                    ignore.Clear();
                    foreach (var e in extra.Where(e => e.InheritsFromName != null))
                    {
                        var p = extra.Where(df => df.Name == e.InheritsFromName).ToList();
                        if (p.Count == 0)
                        {
                            _warnings.Add(App.Translation.Error.DataDir_Skip_InhNoProp.Fmt(origFilenames[e], e.InheritsFromName));
                            ignore.Add(e);
                            continue;
                        }
                        if (e.InheritsFromLanguage != null)
                        {
                            p = p.Where(df => df.Language == e.InheritsFromLanguage).ToList();
                            if (p.Count == 0)
                            {
                                _warnings.Add(App.Translation.Error.DataDir_Skip_InhNoLang.Fmt(origFilenames[e], e.InheritsFromName, e.InheritsFromLanguage));
                                ignore.Add(e);
                                continue;
                            }
                        }
                        if (e.InheritsFromAuthor != null)
                        {
                            p = p.Where(df => df.Author == e.InheritsFromAuthor).ToList();
                            if (p.Count == 0)
                            {
                                _warnings.Add(App.Translation.Error.DataDir_Skip_InhNoAuth.Fmt(origFilenames[e], e.InheritsFromName, e.InheritsFromAuthor));
                                ignore.Add(e);
                                continue;
                            }
                        }
                        p = p.Where(df => df.GameVersion <= e.GameVersion).ToList();
                        if (p.Count == 0)
                        {
                            _warnings.Add(App.Translation.Error.DataDir_Skip_InhNoGameVer.Fmt(origFilenames[e], e.InheritsFromName, e.InheritsFromLanguage, e.GameVersion));
                            ignore.Add(e);
                            continue;
                        }
                    }
                    extra.RemoveAll(f => ignore.Contains(f));
                } while (ignore.Count > 0);

                // Determine all the immediate parents
                foreach (var e in extra)
                {
                    var sameNEL = extra.Where(df => df.Name == e.Name && df.Author == e.Author && df.Language == e.Language).ToList();

                    // Inherit from the explicitly specified file
                    if (e.InheritsFromName != null)
                    {
                        var p = extra.Where(df => df.GameVersion <= e.GameVersion && df.Name == e.InheritsFromName)
                            .OrderByDescending(df => df.GameVersion).AsEnumerable();
                        if (e.InheritsFromLanguage != null)
                            p = p.Where(df => df.Language == e.InheritsFromLanguage);
                        e.ImmediateParents.Add(p.Where(df => df.Author == e.InheritsFromAuthor).OrderByDescending(df => df.FileVersion).First());
                    }

                    // Inherit from the latest version of the same file for an earlier game version
                    var earlierGameVersion = sameNEL.Where(df => df.GameVersion < e.GameVersion).MaxAll(df => df.GameVersion).MaxOrDefault(df => df.FileVersion);
                    if (earlierGameVersion != null)
                        e.ImmediateParents.Add(earlierGameVersion);

                    // Inherit from an earlier version of this same file
                    var earlierVersionOfSameFile = sameNEL.Where(df => df.GameVersion == e.GameVersion && df.FileVersion < e.FileVersion)
                        .MaxOrDefault(df => df.FileVersion);
                    if (earlierVersionOfSameFile != null)
                        e.ImmediateParents.Add(earlierVersionOfSameFile);
                }

                // Compute the transitive closure
                bool added;
                foreach (var e in extra)
                    foreach (var p in e.ImmediateParents)
                        p.TransitiveChildren.Add(e);
                // Keep adding children's children until no further changes (quite a brute-force algorithm... potential bottleneck)
                do
                {
                    added = false;
                    foreach (var e in extra)
                        foreach (var c1 in e.TransitiveChildren.ToArray())
                            foreach (var c2 in c1.TransitiveChildren)
                                if (!e.TransitiveChildren.Contains(c2))
                                {
                                    e.TransitiveChildren.Add(c2);
                                    added = true;
                                }
                } while (added);

                // Detect dependency loops and remove them
                var looped = extra.Where(e => e.TransitiveChildren.Contains(e)).ToArray();
                foreach (var item in looped.ToArray())
                {
                    _warnings.Add(App.Translation.Error.DataDir_Skip_InhCircular.Fmt(origFilenames[item]));
                    extra.Remove(item);
                }
                if (looped.Length == 0)
                    break;

                // Removed some data files. Other files could depend on them, so redo the whole resolution process with the reduced set.
            }

            // Compute the distance to nearest "root"
            foreach (var e in extra)
                e.NearestRoot = -1;
            while (extra.Any(e => e.NearestRoot == -1))
            {
                foreach (var e in extra.Where(e => e.NearestRoot == -1 && e.ImmediateParents.All(p => p.NearestRoot != -1)))
                    e.NearestRoot = e.ImmediateParents.Any() ? e.ImmediateParents.Min(p => p.NearestRoot) + 1 : 0;
            }

            // Get the full list of properties for every data file
            foreach (var e in extra.OrderBy(df => df.NearestRoot))
            {
                var tanks = new Dictionary<string, ExtraData>();

                // Inherit the properties (all the hard work is already done and the files to inherit from are in the correct order)
                foreach (var p in e.ImmediateParents)
                    foreach (var d in p.Result.Data)
                        tanks[d.TankSystemId] = d;
                foreach (var d in e.Data)
                    tanks[d.TankSystemId] = d;

                // Create a new data file with all the tanks
                e.Result = new DataFileExtra(e, tanks.Values);
            }

            // Keep only the latest file version of each file
            foreach (var e in extra.GroupBy(df => new { name = df.Name, language = df.Language, author = df.Author, gamever = df.GameVersion }))
                _extra.Add(e.Single(k => k.FileVersion == e.Max(m => m.FileVersion)).Result);
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }

    /// <summary>Represents one of the WoT countries.</summary>
    enum Country
    {
        USSR,
        Germany,
        USA,
        France,
        China,
        UK,
    }

    /// <summary>Represents one of the possible tank classes: light/medium/heavy, tank destroyer, and artillery.</summary>
    enum Class
    {
        Light,
        Medium,
        Heavy,
        Destroyer,
        Artillery,
    }

    /// <summary>Represents one of the possible tank availability categories based on how (and whether) they can be bought.</summary>
    enum Category
    {
        /// <summary>This tank can be bought for silver.</summary>
        Normal,
        /// <summary>This tank can be bought for gold only.</summary>
        Premium,
        /// <summary>This tank cannot be bought at all.</summary>
        Special,
    }

    /// <summary>Represents one of the built-in tank image styles.</summary>
    [TypeConverter(typeof(ImageBuiltInStyleTranslation.Conv))]
    enum ImageBuiltInStyle
    {
        Contour,
        ThreeD,
        ThreeDLarge,
        Country,
        Class
    }

    /// <summary>Encapsulates a game version identifier, allowing for a comparable part (like "#332") and a human-readable part (like "0.8.2 Common Test").</summary>
    class VersionId : IComparable<VersionId>, IComparable<int>
    {
        public int BuildId { get; private set; }
        public string Name { get; private set; }
        public override string ToString() { return Name + "  #" + BuildId; }

        /// <summary>Constructor. For XmlClassify.</summary>
        private VersionId() { BuildId = 0; Name = "n/a"; }

        public VersionId(int buildId, string name)
        {
            if (name == null)
                throw new ArgumentNullException("name");
            BuildId = buildId;
            Name = name;
        }

        public int CompareTo(VersionId other)
        {
            if (other == null)
                throw new ArgumentNullException();
            return this.BuildId.CompareTo(other.BuildId);
        }

        public int CompareTo(int otherBuildId)
        {
            return this.BuildId.CompareTo(otherBuildId);
        }

        public override bool Equals(object obj)
        {
            var other = obj as VersionId;
            return obj != null && BuildId == other.BuildId;
        }

        public override int GetHashCode()
        {
            int result = 47;
            result = result * 17 + BuildId.GetHashCode();
            result = result * 17 + Name.GetHashCode();
            return result;
        }

        public static bool operator >(VersionId a, int b) { return a.BuildId.CompareTo(b) > 0; }
        public static bool operator <(VersionId a, int b) { return a.BuildId.CompareTo(b) < 0; }
        public static bool operator >=(VersionId a, int b) { return a.BuildId.CompareTo(b) >= 0; }
        public static bool operator <=(VersionId a, int b) { return a.BuildId.CompareTo(b) <= 0; }
        public static bool operator >=(int a, VersionId b) { return a.CompareTo(b.BuildId) >= 0; }
        public static bool operator <=(int a, VersionId b) { return a.CompareTo(b.BuildId) <= 0; }
        public static bool operator >(VersionId a, VersionId b) { return a.CompareTo(b) > 0; }
        public static bool operator <(VersionId a, VersionId b) { return a.CompareTo(b) < 0; }
        public static bool operator >=(VersionId a, VersionId b) { return a.CompareTo(b) >= 0; }
        public static bool operator <=(VersionId a, VersionId b) { return a.CompareTo(b) <= 0; }
        public static bool operator ==(VersionId a, VersionId b) { return (a == (object) null && b == (object) null) || (a != (object) null && a.Equals(b)); }
        public static bool operator !=(VersionId a, VersionId b) { return !(a == b); }
    }

    /// <summary>
    /// Represents some settings for a particular game version.
    /// </summary>
    sealed class GameVersion
    {
        /// <summary>The version of the game that this applies to. This property is deduced from file name, rather than its content.</summary>
        [XmlIgnore]
        public VersionId Version { get; internal set; }

        /// <summary>How this version should be displayed in the UI - allowing for oddities like "0.7.1b" or "0.7.1.1.1.1".</summary>
        public string DisplayName { get; private set; }

        /// <summary>Relative path to the root directory containing modding-related files for this specific version.</summary>
        public string PathMods { get; private set; }
        /// <summary>Relative path to the directory containing the tank icons we're creating.</summary>
        public string PathDestination { get; private set; }
        /// <summary>Relative path to the directory containing contour tank images. May refer to a zip file with a colon separating the path within the zip.</summary>
        public string PathSourceContour { get; private set; }
        /// <summary>Relative path to the directory containing 3D tank images. May refer to a zip file with a colon separating the path within the zip.</summary>
        public string PathSource3D { get; private set; }
        /// <summary>Relative path to the directory containing 3D (large) tank images. May refer to a zip file with a colon separating the path within the zip.</summary>
        public string PathSource3DLarge { get; private set; }
        public Dictionary<Country, string> PathSourceCountry { get; private set; }
        public Dictionary<Class, string> PathSourceClass { get; private set; }

        /// <summary>Relative path to a file whose size can be checked to auto-guess the game version.</summary>
        public string CheckFileName { get; private set; }
        /// <summary>A string expected inside the <see cref="CheckFileName"/> file. A match means that this is probably the right game version.</summary>
        public string CheckFileContent { get; private set; }

        /// <summary>Specifies whether the tank images should be loaded and saved as PNG or TGA.</summary>
        public string TankIconExtension { get; private set; }

        /// <summary>Constructor, for use by XmlClassify.</summary>
        private GameVersion()
        {
            PathSourceCountry = new Dictionary<Country, string>();
            PathSourceClass = new Dictionary<Class, string>();
            TankIconExtension = ".tga"; // default, if unspecified, for compatibility with older versions
        }

        /// <summary>Returns the display name of this game version.</summary>
        public override string ToString() { return DisplayName; }
    }
}
