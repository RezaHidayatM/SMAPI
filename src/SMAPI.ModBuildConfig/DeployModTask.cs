using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Newtonsoft.Json;
using StardewModdingAPI.ModBuildConfig.Framework;
using StardewModdingAPI.Toolkit.Framework;
using StardewModdingAPI.Toolkit.Serialization;
using StardewModdingAPI.Toolkit.Serialization.Models;
using StardewModdingAPI.Toolkit.Utilities;

namespace StardewModdingAPI.ModBuildConfig
{
    /// <summary>A build task which deploys the mod files and prepares a release zip.</summary>
    public class DeployModTask : Task
    {
        /*********
        ** Accessors
        *********/
        /// <summary>The name (without extension or path) of the current mod's DLL.</summary>
        [Required]
        public string ModDllName { get; set; }

        /// <summary>The name of the mod folder.</summary>
        [Required]
        public string ModFolderName { get; set; }

        /// <summary>The absolute or relative path to the folder which should contain the generated zip file.</summary>
        [Required]
        public string ModZipPath { get; set; }

        /// <summary>The folder containing the project files.</summary>
        [Required]
        public string ProjectDir { get; set; }

        /// <summary>The folder containing the build output.</summary>
        [Required]
        public string TargetDir { get; set; }

        /// <summary>The folder containing the game's mod folders.</summary>
        [Required]
        public string GameModsDir { get; set; }

        /// <summary>Whether to enable copying the mod files into the game's Mods folder.</summary>
        [Required]
        public bool EnableModDeploy { get; set; }

        /// <summary>Whether to enable the release zip.</summary>
        [Required]
        public bool EnableModZip { get; set; }

        /// <summary>A comma-separated list of regex patterns matching files to ignore when deploying or zipping the mod.</summary>
        public string IgnoreModFilePatterns { get; set; }

        /// <summary>A comma-separated list of relative file paths to ignore when deploying or zipping the mod.</summary>
        public string IgnoreModFilePaths { get; set; }

        /// <summary>A comma-separated list of <see cref="ExtraAssemblyTypes"/> values which indicate which extra DLLs to bundle.</summary>
        public string BundleExtraAssemblies { get; set; }

        /// <summary>A list of content pack folders to bundle with the mod.</summary>
        public ITaskItem[] ContentPacks { get; set; }


        /*********
        ** Public methods
        *********/
        /// <summary>When overridden in a derived class, executes the task.</summary>
        /// <returns>true if the task successfully executed; otherwise, false.</returns>
        public override bool Execute()
        {
            // log build settings
            {
                var properties = this
                    .GetPropertiesToLog()
                    .Select(p => $"{p.Key}: {p.Value}");
                this.Log.LogMessage(MessageImportance.High, $"[mod build package] Handling build with options {string.Join(", ", properties)}");
            }

            // skip if nothing to do
            // (This must be checked before the manifest validation, to allow cases like unit test projects.)
            if (!this.EnableModDeploy && !this.EnableModZip)
                return true;

            // validate the manifest file
            IManifest manifest;
            {
                try
                {
                    string manifestPath = Path.Combine(this.ProjectDir, "manifest.json");
                    if (!new JsonHelper().ReadJsonFileIfExists(manifestPath, out Manifest rawManifest))
                    {
                        this.Log.LogError("[mod build package] The mod's manifest.json file doesn't exist.");
                        return false;
                    }
                    manifest = rawManifest;
                }
                catch (JsonReaderException ex)
                {
                    // log the inner exception, otherwise the message will be generic
                    Exception exToShow = ex.InnerException ?? ex;
                    this.Log.LogError($"[mod build package] The mod's manifest.json file isn't valid JSON: {exToShow.Message}");
                    return false;
                }

                // validate manifest fields
                if (!ManifestValidator.TryValidateFields(manifest, out string error))
                {
                    this.Log.LogError($"[mod build package] The mod's manifest.json file is invalid: {error}");
                    return false;
                }
            }

            // deploy files
            try
            {
                // parse extra DLLs to bundle
                ExtraAssemblyTypes bundleAssemblyTypes = this.GetExtraAssembliesToBundleOption();

                // parse ignore patterns
                string[] ignoreFilePaths = this.GetCustomIgnoreFilePaths(this.IgnoreModFilePatterns).ToArray();
                Regex[] ignoreFilePatterns = this.GetCustomIgnorePatterns(this.IgnoreModFilePaths).ToArray();

                var modPackages = new Dictionary<string, IModFileManager>
                {
                    [this.ModFolderName] = new MainModFileManager(this.ProjectDir, this.TargetDir, ignoreFilePaths, ignoreFilePatterns, bundleAssemblyTypes, this.ModDllName, validateRequiredModFiles: this.EnableModDeploy || this.EnableModZip)
                };

                if (this.ContentPacks != null)
                {
                    foreach (ITaskItem item in this.ContentPacks)
                    {
                        // get content folder path
                        string contentPath = item.ItemSpec;
                        if (string.IsNullOrWhiteSpace(contentPath))
                            throw new UserErrorException("Content pack doesn't have the required 'Include' attribute.");

                        // get folder name
                        string folderName = item.GetMetadata("FolderName");
                        if (string.IsNullOrWhiteSpace(folderName))
                            folderName = Path.GetFileName(contentPath);

                        // get version
                        string version = item.GetMetadata("Version");

                        // get options
                        ignoreFilePaths = this.GetCustomIgnoreFilePaths(item.GetMetadata("IgnoreModFilePatterns")).ToArray();
                        ignoreFilePatterns = this.GetCustomIgnorePatterns(this.IgnoreModFilePaths).ToArray();
                        string rawValidateManifest = item.GetMetadata("ValidateManifest");
                        bool validateManifest = string.IsNullOrEmpty(rawValidateManifest) || bool.Parse(rawValidateManifest);

                        // apply
                        this.Log.LogMessage(MessageImportance.High, $"[mod build package] Bundling content pack: {folderName} v{version} at {contentPath}.");
                        modPackages.Add(
                            folderName,
                            new ContentPackFileManager(this.ProjectDir, contentPath, version, ignoreFilePaths, ignoreFilePatterns, validateManifest)
                        );
                    }
                }
                // deploy mod files
                if (this.EnableModDeploy)
                {
                    string outputPath = Path.Combine(this.GameModsDir, this.EscapeInvalidFilenameCharacters(this.ModFolderName));
                    this.Log.LogMessage(MessageImportance.High, $"[mod build package] Copying the mod files to {outputPath}...");
                    this.CreateModFolder(modPackages, outputPath);
                }

                // create release zip
                if (this.EnableModZip)
                {
                    string zipName = this.EscapeInvalidFilenameCharacters($"{this.ModFolderName} {manifest.Version}.zip");
                    string zipPath = Path.Combine(this.ModZipPath, zipName);

                    this.Log.LogMessage(MessageImportance.High, $"[mod build package] Generating the release zip at {zipPath}...");
                    this.CreateReleaseZip(modPackages, zipPath);
                }

                return true;
            }
            catch (UserErrorException ex)
            {
                this.Log.LogErrorFromException(ex);
                return false;
            }
            catch (Exception ex)
            {
                this.Log.LogError($"[mod build package] Failed trying to deploy the mod.\n{ex}");
                return false;
            }
        }


        /*********
        ** Private methods
        *********/
        /// <summary>Get the properties to write to the log.</summary>
        private IEnumerable<KeyValuePair<string, string>> GetPropertiesToLog()
        {
            var properties = this.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            foreach (PropertyInfo property in properties.OrderBy(p => p.Name))
            {
                if (property.Name == nameof(this.IgnoreModFilePatterns) && string.IsNullOrWhiteSpace(this.IgnoreModFilePatterns))
                    continue;

                if (property.Name == nameof(this.ContentPacks))
                    continue;

                string name = property.Name;

                string value = property.GetValue(this)?.ToString();
                if (value == null)
                    value = "null";
                else if (property.PropertyType == typeof(bool))
                    value = value.ToLower();
                else
                    value = $"'{value}'";

                yield return new KeyValuePair<string, string>(name, value);
            }
        }

        /// <summary>Parse the extra assembly types which should be bundled with the mod.</summary>
        private ExtraAssemblyTypes GetExtraAssembliesToBundleOption()
        {
            ExtraAssemblyTypes flags = ExtraAssemblyTypes.None;

            if (!string.IsNullOrWhiteSpace(this.BundleExtraAssemblies))
            {
                foreach (string raw in this.BundleExtraAssemblies.Split(','))
                {
                    if (!Enum.TryParse(raw, out ExtraAssemblyTypes type))
                    {
                        this.Log.LogWarning($"[mod build package] Ignored invalid <{nameof(this.BundleExtraAssemblies)}> value '{raw}', expected one of '{string.Join("', '", Enum.GetNames(typeof(ExtraAssemblyTypes)))}'.");
                        continue;
                    }

                    flags |= type;
                }
            }

            return flags;
        }

        /// <summary>Get the custom ignore patterns provided by the user.</summary>
        /// <param name="patterns">The comma-separated regex patterns.</param>
        private IEnumerable<Regex> GetCustomIgnorePatterns(string patterns)
        {
            if (string.IsNullOrWhiteSpace(patterns))
                yield break;

            foreach (string raw in patterns.Split(','))
            {
                Regex regex;
                try
                {
                    regex = new Regex(raw.Trim(), RegexOptions.IgnoreCase);
                }
                catch (Exception ex)
                {
                    this.Log.LogWarning($"[mod build package] Ignored invalid <{nameof(patterns)}> pattern {raw}:\n{ex}");
                    continue;
                }

                yield return regex;
            }
        }

        /// <summary>Get the custom relative file paths provided by the user to ignore.</summary>
        /// <param name="pattern">The comma-separated file paths.</param>
        private IEnumerable<string> GetCustomIgnoreFilePaths(string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern))
                yield break;

            foreach (string raw in pattern.Split(','))
            {
                string path;
                try
                {
                    path = PathUtilities.NormalizePath(raw);
                }
                catch (Exception ex)
                {
                    this.Log.LogWarning($"[mod build package] Ignored invalid <{nameof(pattern)}> path {raw}:\n{ex}");
                    continue;
                }

                yield return path;
            }
        }

        /// <summary>Copy the mod files into the game's mod folder.</summary>
        /// <param name="modPackages">The mods to include.</param>
        /// <param name="outputPath">The folder path to create with the mod files.</param>
        private void CreateModFolder(IDictionary<string, IModFileManager> modPackages, string outputPath)
        {
            foreach (var mod in modPackages)
            {
                string relativePath = modPackages.Count == 1
                    ? outputPath
                    : Path.Combine(outputPath, this.EscapeInvalidFilenameCharacters(mod.Key));

                foreach (var file in mod.Value.GetFiles())
                {
                    string fromPath = file.Value.FullName;
                    string toPath = Path.Combine(relativePath, file.Key);

                    Directory.CreateDirectory(Path.GetDirectoryName(toPath)!);

                    File.Copy(fromPath, toPath, overwrite: true);
                }
            }
        }

        /// <summary>Create a release zip in the recommended format for uploading to mod sites.</summary>
        /// <param name="modPackages">The mods to include.</param>
        /// <param name="zipPath">The absolute path to the zip file to create.</param>
        private void CreateReleaseZip(IDictionary<string, IModFileManager> modPackages, string zipPath)
        {
            // create zip file
            Directory.CreateDirectory(Path.GetDirectoryName(zipPath)!);
            using Stream zipStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write);
            using ZipArchive archive = new(zipStream, ZipArchiveMode.Create);

            foreach (var mod in modPackages)
            {
                string modFolder = this.EscapeInvalidFilenameCharacters(mod.Key);
                foreach (var file in mod.Value.GetFiles())
                {
                    string relativePath = file.Key;
                    FileInfo fileInfo = file.Value;

                    string archivePath = modPackages.Count == 1
                        ? $"{modFolder}/{relativePath}"
                        : $"{this.ModFolderName}/{modFolder}/{relativePath}";

                    using Stream fileStream = new FileStream(fileInfo.FullName, FileMode.Open, FileAccess.Read);
                    using Stream fileStreamInZip = archive.CreateEntry(archivePath).Open();
                    fileStream.CopyTo(fileStreamInZip);
                }
            }
        }

        /// <summary>Get a copy of a filename with all invalid filename characters substituted.</summary>
        /// <param name="name">The filename.</param>
        private string EscapeInvalidFilenameCharacters(string name)
        {
            foreach (char invalidChar in Path.GetInvalidFileNameChars())
                name = name.Replace(invalidChar, '.');
            return name;
        }
    }
}
