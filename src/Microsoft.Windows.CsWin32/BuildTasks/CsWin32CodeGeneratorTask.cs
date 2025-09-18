using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.Windows.CsWin32.BuildTasks
{
    /// <summary>
    /// MSBuild task to invoke CsWin32 code generation via the command line tool.
    /// </summary>
    public class CsWin32CodeGeneratorTask : ToolTask
    {
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
        /// Gets or sets the full path to the CsWin32Generator tool executable.
        /// </summary>
        public string? ToolPath { get; set; }

        /// <summary>
        /// Gets the generated source files.
        /// </summary>
        [Output]
        public ITaskItem[]? GeneratedFiles { get; set; }

        /// <summary>
        /// Gets the name of the tool executable.
        /// </summary>
        protected override string ToolName => "CsWin32Generator.exe";

        /// <summary>
        /// Gets the full path to the tool executable.
        /// </summary>
        /// <returns>The full path to the tool executable.</returns>
        protected override string GenerateFullPathToTool()
        {
            if (!string.IsNullOrEmpty(ToolPath))
            {
                return ToolPath;
            }

            // The tool should be in the same directory as this assembly
            string assemblyLocation = typeof(CsWin32CodeGeneratorTask).Assembly.Location;
            string assemblyDirectory = Path.GetDirectoryName(assemblyLocation)!;
            return Path.Combine(assemblyDirectory, ToolName);
        }

        /// <summary>
        /// Generates the command line arguments for the tool.
        /// </summary>
        /// <returns>The command line arguments.</returns>
        protected override string GenerateCommandLineCommands()
        {
            var commandLine = new CommandLineBuilder();

            // Required parameters
            commandLine.AppendSwitchIfNotNull("--native-methods-txt ", NativeMethodsTxt);
            commandLine.AppendSwitchIfNotNull("--output-path ", OutputPath);

            if (!string.IsNullOrEmpty(MetadataPaths))
            {
                string[] paths = SplitPaths(MetadataPaths);
                if (paths.Length > 0)
                {
                    commandLine.AppendSwitch("--metadata-paths");
                    foreach (string path in paths)
                    {
                        commandLine.AppendFileNameIfNotNull(path.Trim());
                    }
                }
            }

            // Optional parameters
            commandLine.AppendSwitchIfNotNull("--native-methods-json ", NativeMethodsJson);

            if (!string.IsNullOrEmpty(DocPaths))
            {
                string[] paths = SplitPaths(DocPaths);
                if (paths.Length > 0)
                {
                    commandLine.AppendSwitch("--doc-paths");
                    foreach (string path in paths)
                    {
                        commandLine.AppendFileNameIfNotNull(path.Trim());
                    }
                }
            }

            if (!string.IsNullOrEmpty(AppLocalAllowedLibraries))
            {
                string[] paths = SplitPaths(AppLocalAllowedLibraries);
                if (paths.Length > 0)
                {
                    commandLine.AppendSwitch("--app-local-allowed-libraries");
                    foreach (string path in paths)
                    {
                        commandLine.AppendFileNameIfNotNull(path.Trim());
                    }
                }
            }

            commandLine.AppendSwitchIfNotNull("--allow-unsafe-blocks ", AllowUnsafeBlocks.ToString().ToLowerInvariant());
            commandLine.AppendSwitchIfNotNull("--target-framework ", TargetFramework);
            commandLine.AppendSwitchIfNotNull("--platform ", Platform);

            if (References != null && References.Length > 0)
            {
                commandLine.AppendSwitch("--references");
                foreach (ITaskItem reference in References)
                {
                    commandLine.AppendFileNameIfNotNull(reference.ItemSpec);
                }
            }

            return commandLine.ToString();
        }

        /// <summary>
        /// Executes the task after the tool has run successfully.
        /// </summary>
        /// <returns><see langword="true"/> if the task executed successfully; otherwise, <see langword="false"/>.</returns>
        public override bool Execute()
        {
            bool success = base.Execute();

            if (success && !string.IsNullOrEmpty(OutputPath))
            {
                // Populate the GeneratedFiles output with the files that were created
                var generatedFiles = new List<ITaskItem>();

                if (Directory.Exists(OutputPath))
                {
                    foreach (string filePath in Directory.GetFiles(OutputPath, "*.g.cs", SearchOption.TopDirectoryOnly))
                    {
                        var taskItem = new TaskItem(filePath);
                        taskItem.SetMetadata("Generator", "CsWin32");
                        generatedFiles.Add(taskItem);
                    }
                }

                GeneratedFiles = generatedFiles.ToArray();
                Log.LogMessage(MessageImportance.Normal, $"Successfully generated {GeneratedFiles.Length} source files.");
            }

            return success;
        }

        /// <summary>
        /// Validates the task parameters.
        /// </summary>
        /// <returns><see langword="true"/> if the parameters are valid; otherwise, <see langword="false"/>.</returns>
        protected override bool ValidateParameters()
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

        private static string[] SplitPaths(string paths)
        {
            return paths.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
        }
    }
}
