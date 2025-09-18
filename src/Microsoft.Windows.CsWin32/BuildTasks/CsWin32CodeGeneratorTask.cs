using System.Text;
using System.Text.Json;
using Microsoft.Build.Framework;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.Windows.CsWin32.BuildTasks
{
    /// <summary>
    /// MSBuild task to invoke CsWin32 code generation.
    /// </summary>
    public class CsWin32CodeGeneratorTask : Microsoft.Build.Utilities.Task
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        private static readonly char[] ZeroWhiteSpace = new char[]
        {
            '\uFEFF', // ZERO WIDTH NO-BREAK SPACE (U+FEFF)
            '\u200B', // ZERO WIDTH SPACE (U+200B)
        };

        /// <summary>
        /// Gets or sets the path to the NativeMethods.txt file containing API names to generate.
        /// </summary>
        [Required]
        public string? NativeMethodsTxt { get; set; }

        /// <summary>
        /// Gets or sets the path to the NativeMethods.json file containing generation options.
        /// </summary>
        public string? NativeMethodsJson { get; set; }

        /// <summary>
        /// Gets or sets the semicolon-separated paths to Windows metadata files (.winmd).
        /// </summary>
        [Required]
        public string? MetadataPaths { get; set; }

        /// <summary>
        /// Gets or sets the semicolon-separated paths to documentation files.
        /// </summary>
        public string? DocPaths { get; set; }

        /// <summary>
        /// Gets or sets the semicolon-separated paths to app-local allowed libraries.
        /// </summary>
        public string? AppLocalAllowedLibraries { get; set; }

        /// <summary>
        /// Gets or sets the output directory where generated files will be written.
        /// </summary>
        [Required]
        public string? OutputPath { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether unsafe code is allowed.
        /// </summary>
        public bool AllowUnsafeBlocks { get; set; } = true;

        /// <summary>
        /// Gets or sets the target framework version (affects available features).
        /// </summary>
        public string? TargetFramework { get; set; }

        /// <summary>
        /// Gets or sets the target platform (e.g., x86, x64, AnyCPU).
        /// </summary>
        public string? Platform { get; set; } = "AnyCPU";

        /// <summary>
        /// Gets or sets additional references to be included in the compilation context.
        /// </summary>
        public ITaskItem[]? References { get; set; }

        /// <summary>
        /// Gets the generated source files.
        /// </summary>
        [Output]
        public ITaskItem[]? GeneratedFiles { get; set; }

        public override bool Execute()
        {
            try
            {
                if (!ValidateInputs())
                {
                    return false;
                }

                // Load generator options from NativeMethods.json if provided
                GeneratorOptions options = LoadGeneratorOptions();

                // Parse metadata paths
                string[] metadataPaths = SplitPaths(MetadataPaths!);
                if (metadataPaths.Length == 0)
                {
                    Log.LogError("At least one metadata path must be provided.");
                    return false;
                }

                // Parse other optional paths
                string[] docPaths = string.IsNullOrEmpty(DocPaths) 
                    ? Array.Empty<string>() 
                    : SplitPaths(DocPaths);

                IEnumerable<string> appLocalLibraries = string.IsNullOrEmpty(AppLocalAllowedLibraries)
                    ? Array.Empty<string>()
                    : SplitPaths(AppLocalAllowedLibraries).Select(Path.GetFileName);

                // Create compilation context
                CSharpCompilation? compilation = CreateCompilation();
                CSharpParseOptions? parseOptions = CreateParseOptions();

                // Load docs if available
                Docs? docs = LoadDocs(docPaths);

                // Create generators for all metadata paths
                var generators = new List<Generator>();
                foreach (string metadataPath in metadataPaths)
                {
                    if (!File.Exists(metadataPath))
                    {
                        Log.LogError($"Metadata file not found: {metadataPath}");
                        return false;
                    }

                    generators.Add(new Generator(metadataPath, docs, appLocalLibraries, options, compilation, parseOptions));
                }

                // Create super generator if multiple generators
                using SuperGenerator superGenerator = generators.Count == 1 
                    ? SuperGenerator.Combine(generators[0])
                    : SuperGenerator.Combine(generators.ToArray());

                // Process NativeMethods.txt file
                if (!ProcessNativeMethodsFile(superGenerator))
                {
                    return false;
                }

                // Generate compilation units and write to files
                if (!GenerateAndWriteFiles(superGenerator))
                {
                    return false;
                }

                Log.LogMessage(MessageImportance.Normal, $"Successfully generated {GeneratedFiles?.Length ?? 0} source files.");
                return true;
            }
            catch (Exception ex)
            {
                Log.LogErrorFromException(ex, showStackTrace: true);
                return false;
            }
        }

        private static string[] SplitPaths(string paths)
        {
            return paths.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private bool ValidateInputs()
        {
            if (string.IsNullOrEmpty(NativeMethodsTxt))
            {
                Log.LogError("NativeMethodsTxt property must be specified.");
                return false;
            }

            if (!File.Exists(NativeMethodsTxt))
            {
                Log.LogError($"NativeMethods.txt file not found: {NativeMethodsTxt}");
                return false;
            }

            if (string.IsNullOrEmpty(MetadataPaths))
            {
                Log.LogError("MetadataPaths property must be specified.");
                return false;
            }

            if (string.IsNullOrEmpty(OutputPath))
            {
                Log.LogError("OutputPath property must be specified.");
                return false;
            }

            return true;
        }

        private GeneratorOptions LoadGeneratorOptions()
        {
            if (string.IsNullOrEmpty(NativeMethodsJson) || !File.Exists(NativeMethodsJson))
            {
                return new GeneratorOptions();
            }

            try
            {
                string optionsJson = File.ReadAllText(NativeMethodsJson);
                return JsonSerializer.Deserialize<GeneratorOptions>(optionsJson, JsonOptions);
            }
            catch (JsonException ex)
            {
                Log.LogError($"Failed to parse NativeMethods.json: {ex.Message}");
                return new GeneratorOptions();
            }
        }

        private CSharpCompilation? CreateCompilation()
        {
            var references = new List<MetadataReference>();

            // Add basic framework references
            string? runtimePath = Path.GetDirectoryName(typeof(object).Assembly.Location);
            if (runtimePath != null)
            {
                string systemRuntimePath = Path.Combine(runtimePath, "System.Runtime.dll");
                if (File.Exists(systemRuntimePath))
                {
                    references.Add(MetadataReference.CreateFromFile(systemRuntimePath));
                }

                string netstandardPath = Path.Combine(runtimePath, "netstandard.dll");
                if (File.Exists(netstandardPath))
                {
                    references.Add(MetadataReference.CreateFromFile(netstandardPath));
                }

                string systemMemoryPath = Path.Combine(runtimePath, "System.Memory.dll");
                if (File.Exists(systemMemoryPath))
                {
                    references.Add(MetadataReference.CreateFromFile(systemMemoryPath));
                }
            }

            // Add additional references if provided
            if (References != null)
            {
                foreach (ITaskItem reference in References)
                {
                    string refPath = reference.ItemSpec;
                    if (File.Exists(refPath))
                    {
                        references.Add(MetadataReference.CreateFromFile(refPath));
                    }
                }
            }

            Microsoft.CodeAnalysis.Platform platform = Platform switch
            {
                "x86" => Microsoft.CodeAnalysis.Platform.X86,
                "x64" => Microsoft.CodeAnalysis.Platform.X64,
                "arm64" => Microsoft.CodeAnalysis.Platform.Arm64,
                _ => Microsoft.CodeAnalysis.Platform.AnyCpu,
            };

            var compilationOptions = new CSharpCompilationOptions(
                outputKind: OutputKind.DynamicallyLinkedLibrary,
                allowUnsafe: AllowUnsafeBlocks,
                platform: platform);

            return CSharpCompilation.Create(
                assemblyName: "GeneratedCode",
                syntaxTrees: null,
                references: references,
                options: compilationOptions);
        }

        private CSharpParseOptions? CreateParseOptions()
        {
            // Determine language version based on target framework - using safe values for .NET Standard 2.0
            LanguageVersion languageVersion = TargetFramework switch
            {
                var tf when tf?.StartsWith("net9.0") == true => LanguageVersion.Latest,
                var tf when tf?.StartsWith("net8.0") == true => LanguageVersion.Latest,
                var tf when tf?.StartsWith("net7.0") == true => LanguageVersion.Latest,
                var tf when tf?.StartsWith("net6.0") == true => LanguageVersion.CSharp9,
                _ => LanguageVersion.CSharp9,
            };

            return new CSharpParseOptions(languageVersion: languageVersion);
        }

        private Docs? LoadDocs(string[] docPaths)
        {
            if (docPaths.Length == 0)
            {
                return null;
            }

            var docsList = new List<Docs>();
            foreach (string docPath in docPaths)
            {
                try
                {
                    if (File.Exists(docPath))
                    {
                        docsList.Add(Docs.Get(docPath));
                    }
                }
                catch (Exception ex)
                {
                    Log.LogWarning($"Failed to load documentation from {docPath}: {ex.Message}");
                }
            }

            return docsList.Count > 0 ? Docs.Merge(docsList) : null;
        }

        private bool ProcessNativeMethodsFile(SuperGenerator superGenerator)
        {
            try
            {
                var lines = File.ReadAllLines(NativeMethodsTxt!);
                
                foreach (string line in lines)
                {
                    string name = line.Trim().Trim(ZeroWhiteSpace);
                    if (string.IsNullOrWhiteSpace(name) || name.StartsWith("//", StringComparison.InvariantCulture))
                    {
                        continue;
                    }

                    try
                    {
                        if (Generator.GetBannedAPIs(superGenerator.Generators.First().Value.Options).TryGetValue(name, out string? reason))
                        {
                            Log.LogWarning($"API '{name}' is banned: {reason}");
                            continue;
                        }

                        if (name.EndsWith(".*", StringComparison.Ordinal))
                        {
                            string? moduleName = name.Substring(0, name.Length - 2);
                            int matches = superGenerator.TryGenerateAllExternMethods(moduleName, CancellationToken.None);
                            if (matches == 0)
                            {
                                Log.LogWarning($"No methods found under module '{moduleName}'");
                            }
                            continue;
                        }

                        superGenerator.TryGenerate(name, out IReadOnlyCollection<string> matchingApis, out IReadOnlyCollection<string> redirectedEnums, CancellationToken.None);
                        
                        foreach (string declaringEnum in redirectedEnums)
                        {
                            Log.LogWarning($"Use the name of the enum that declares this constant: {declaringEnum}");
                        }

                        switch (matchingApis.Count)
                        {
                            case 0:
                                Log.LogWarning($"Method, type or constant '{name}' not found");
                                break;
                            case > 1:
                                Log.LogError($"The API '{name}' is ambiguous. Please specify one of: {string.Join(", ", matchingApis.Select(api => $"\"{api}\""))}");
                                break;
                        }
                    }
                    catch (PlatformIncompatibleException)
                    {
                        Log.LogWarning($"API '{name}' is not available for the target platform");
                    }
                    catch (Exception ex)
                    {
                        Log.LogError($"Internal error processing '{name}': {ex.Message}");
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Log.LogError($"Failed to process NativeMethods.txt file: {ex.Message}");
                return false;
            }
        }

        private bool GenerateAndWriteFiles(SuperGenerator superGenerator)
        {
            try
            {
                // Ensure output directory exists
                Directory.CreateDirectory(OutputPath!);

                var generatedFiles = new List<ITaskItem>();

                foreach (KeyValuePair<string, CompilationUnitSyntax> unit in superGenerator.GetCompilationUnits(CancellationToken.None))
                {
                    string fileName = unit.Key;
                    string filePath = Path.Combine(OutputPath!, fileName);

                    // Write the file
                    string sourceText = unit.Value.GetText(Encoding.UTF8).ToString();
                    File.WriteAllText(filePath, sourceText, Encoding.UTF8);

                    // Add to output items
                    var taskItem = new Microsoft.Build.Utilities.TaskItem(filePath);
                    taskItem.SetMetadata("Generator", "CsWin32");
                    generatedFiles.Add(taskItem);

                    Log.LogMessage(MessageImportance.Low, $"Generated: {fileName}");
                }

                GeneratedFiles = generatedFiles.ToArray();
                return true;
            }
            catch (Exception ex)
            {
                Log.LogError($"Failed to generate and write files: {ex.Message}");
                return false;
            }
        }
    }
}
