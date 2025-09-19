// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.Windows.CsWin32.BuildTasks;

/// <summary>
/// Interface for executing command line tools.
/// </summary>
public interface IToolExecutor
{
    /// <summary>
    /// Executes a tool with the given command line arguments.
    /// </summary>
    /// <param name="toolPath">The path to the tool executable.</param>
    /// <param name="commandLineCommands">The command line arguments.</param>
    /// <param name="workingDirectory">The working directory for the tool execution.</param>
    /// <returns><see langword="true"/> if the tool executed successfully; otherwise, <see langword="false"/>.</returns>
    bool ExecuteTool(string toolPath, string commandLineCommands, string workingDirectory);
}

/// <summary>
/// Default implementation of IToolExecutor that delegates to ToolTask base class.
/// </summary>
internal class DefaultToolExecutor : IToolExecutor
{
    private readonly ToolTask toolTask;

    public DefaultToolExecutor(ToolTask toolTask)
    {
        this.toolTask = toolTask;
    }

    public bool ExecuteTool(string toolPath, string commandLineCommands, string workingDirectory)
    {
        // This will be called by the base ToolTask implementation
        return true; // The actual execution is handled by ToolTask.Execute()
    }
}

/// <summary>
/// MSBuild task to invoke CsWin32 code generation via the command line tool.
/// </summary>
public class CsWin32CodeGeneratorTask : ToolTask
{
    private IToolExecutor? toolExecutor;

    /// <summary>
    /// Gets or sets the tool executor for testing purposes.
    /// </summary>
    public IToolExecutor? ToolExecutor
    {
        get => this.toolExecutor ?? new DefaultToolExecutor(this);
        set => this.toolExecutor = value;
    }

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
    /// Gets or sets the generated source files.
    /// </summary>
    [Output]
    public ITaskItem[]? GeneratedFiles { get; set; }

    /// <summary>
    /// Gets the name of the tool executable.
    /// </summary>
    protected override string ToolName => "CsWin32Generator.exe";

    /// <summary>
    /// Executes the task after the tool has run successfully.
    /// </summary>
    /// <returns><see langword="true"/> if the task executed successfully; otherwise, <see langword="false"/>.</returns>
    public override bool Execute()
    {
        // If we have a custom tool executor (for testing), use it instead of base.Execute()
        if (this.toolExecutor != null)
        {
            if (!this.ValidateParameters())
            {
                return false;
            }

            string toolPath = this.GenerateFullPathToTool();
            string commandLine = this.GenerateCommandLineCommands();
            bool executionSuccess = this.toolExecutor.ExecuteTool(toolPath, commandLine, this.GetWorkingDirectory());

            if (executionSuccess && !string.IsNullOrEmpty(this.OutputPath))
            {
                this.PopulateGeneratedFiles();
            }

            return executionSuccess;
        }

        bool success = base.Execute();

        if (success && !string.IsNullOrEmpty(this.OutputPath))
        {
            this.PopulateGeneratedFiles();
        }

        return success;
    }

    /// <summary>
    /// Gets the full path to the tool executable.
    /// </summary>
    /// <returns>The full path to the tool executable.</returns>
    protected override string GenerateFullPathToTool()
    {
        if (!string.IsNullOrEmpty(this.ToolPath))
        {
            return this.ToolPath;
        }

        // The tool should be in the same directory as this assembly
        string assemblyLocation = typeof(CsWin32CodeGeneratorTask).Assembly.Location;
        string assemblyDirectory = Path.GetDirectoryName(assemblyLocation)!;
        return Path.Combine(assemblyDirectory, this.ToolName);
    }

    /// <summary>
    /// Generates the command line arguments for the tool.
    /// </summary>
    /// <returns>The command line arguments.</returns>
    protected override string GenerateCommandLineCommands()
    {
        var commandLine = new CommandLineBuilder();

        // Required parameters
        commandLine.AppendSwitchIfNotNull("--native-methods-txt ", this.NativeMethodsTxt);
        commandLine.AppendSwitchIfNotNull("--output-path ", this.OutputPath);

        if (!string.IsNullOrEmpty(this.MetadataPaths))
        {
            string[] paths = SplitPaths(this.MetadataPaths!);
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
        commandLine.AppendSwitchIfNotNull("--native-methods-json ", this.NativeMethodsJson);

        if (!string.IsNullOrEmpty(this.DocPaths))
        {
            string[] paths = SplitPaths(this.DocPaths!);
            if (paths.Length > 0)
            {
                commandLine.AppendSwitch("--doc-paths");
                foreach (string path in paths)
                {
                    commandLine.AppendFileNameIfNotNull(path.Trim());
                }
            }
        }

        if (!string.IsNullOrEmpty(this.AppLocalAllowedLibraries))
        {
            string[] paths = SplitPaths(this.AppLocalAllowedLibraries!);
            if (paths.Length > 0)
            {
                commandLine.AppendSwitch("--app-local-allowed-libraries");
                foreach (string path in paths)
                {
                    commandLine.AppendFileNameIfNotNull(path.Trim());
                }
            }
        }

        commandLine.AppendSwitchIfNotNull("--allow-unsafe-blocks ", this.AllowUnsafeBlocks.ToString().ToLowerInvariant());
        commandLine.AppendSwitchIfNotNull("--target-framework ", this.TargetFramework);
        commandLine.AppendSwitchIfNotNull("--platform ", this.Platform);

        if (this.References != null && this.References.Length > 0)
        {
            commandLine.AppendSwitch("--references");
            foreach (ITaskItem reference in this.References)
            {
                commandLine.AppendFileNameIfNotNull(reference.ItemSpec);
            }
        }

        return commandLine.ToString();
    }

    /// <summary>
    /// Validates the task parameters.
    /// </summary>
    /// <returns><see langword="true"/> if the parameters are valid; otherwise, <see langword="false"/>.</returns>
    protected override bool ValidateParameters()
    {
        if (string.IsNullOrEmpty(this.NativeMethodsTxt))
        {
            this.Log.LogError("NativeMethodsTxt property must be specified.");
            return false;
        }

        if (!File.Exists(this.NativeMethodsTxt))
        {
            this.Log.LogError($"NativeMethods.txt file not found: {this.NativeMethodsTxt}");
            return false;
        }

        if (string.IsNullOrEmpty(this.MetadataPaths))
        {
            this.Log.LogError("MetadataPaths property must be specified.");
            return false;
        }

        if (string.IsNullOrEmpty(this.OutputPath))
        {
            this.Log.LogError("OutputPath property must be specified.");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Gets the command line arguments that would be passed to the tool (for testing purposes).
    /// </summary>
    /// <returns>The command line arguments.</returns>
    public string GetCommandLineArguments()
    {
        return this.GenerateCommandLineCommands();
    }

    private static string[] SplitPaths(string paths)
    {
        return paths.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
    }

    private void PopulateGeneratedFiles()
    {
        // Populate the GeneratedFiles output with the files that were created
        var generatedFiles = new List<ITaskItem>();

        if (Directory.Exists(this.OutputPath))
        {
            foreach (string filePath in Directory.GetFiles(this.OutputPath, "*.g.cs", SearchOption.TopDirectoryOnly))
            {
                var taskItem = new TaskItem(filePath);
                taskItem.SetMetadata("Generator", "CsWin32");
                generatedFiles.Add(taskItem);
            }
        }

        this.GeneratedFiles = generatedFiles.ToArray();
        this.Log.LogMessage(MessageImportance.Normal, $"Successfully generated {this.GeneratedFiles.Length} source files.");
    }
}
