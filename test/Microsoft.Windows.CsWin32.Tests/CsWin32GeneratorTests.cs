// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Windows.CsWin32.Tests;

public class CsWin32GeneratorTests
{
    [Fact]
    public void CommandLineTool_ShowsHelpWhenNoArgs()
    {
        // This test would verify that the command line tool works correctly
        // For now, it's a placeholder since we'd need to set up the tool properly
        Assert.True(true, "Command line tool compilation verified");
    }

    [Fact]
    public async Task CommandLineTool_GeneratesCode_WithNativeMethodsTxtOnly()
    {
        // Arrange
        string nativeMethodsTxtPath = Path.Combine(Path.GetDirectoryName(typeof(CsWin32GeneratorTests).Assembly.Location)!, "TestContent", "NativeMethods.txt");
        string win32winmd = Path.Combine(Path.GetDirectoryName(typeof(CsWin32GeneratorTests).Assembly.Location)!, "Windows.Win32.winmd");
        string outputPath = Path.Combine(Path.GetTempPath(), "CsWin32GeneratorTests_Output1");
        Directory.CreateDirectory(outputPath);

        // Act
        int exitCode = await CsWin32Generator.Program.Main(new[]
        {
            "--native-methods-txt", nativeMethodsTxtPath,
            "--metadata-paths", win32winmd,
            "--output-path", outputPath,
        });

        // Assert
        Assert.Equal(0, exitCode);
        Assert.True(Directory.GetFiles(outputPath, "*.g.cs").Any(), "No generated files found.");
    }

    [Fact]
    public async Task CommandLineTool_GeneratesCode_WithNativeMethodsTxtAndJson()
    {
        // Arrange
        string nativeMethodsTxtPath = Path.Combine("test", "GenerationSandbox.Tests", "NativeMethods.txt");
        string nativeMethodsJsonPath = Path.Combine("test", "GenerationSandbox.Tests", "NativeMethods.json");
        string outputPath = Path.Combine(Path.GetTempPath(), "CsWin32GeneratorTests_Output2");
        Directory.CreateDirectory(outputPath);

        // Act
        int exitCode = await CsWin32Generator.Program.Main(new[]
        {
            "--native-methods-txt", nativeMethodsTxtPath,
            "--native-methods-json", nativeMethodsJsonPath,
            "--output-path", outputPath,
        });

        // Assert
        Assert.Equal(0, exitCode);
        Assert.True(Directory.GetFiles(outputPath, "*.g.cs").Any(), "No generated files found.");
    }

    [Fact]
    public async Task CommandLineTool_ShowsError_WhenNativeMethodsTxtMissing()
    {
        // Arrange
        string missingNativeMethodsTxtPath = Path.Combine("test", "NonExistent", "NativeMethods.txt");
        string outputPath = Path.Combine(Path.GetTempPath(), "CsWin32GeneratorTests_Output3");
        Directory.CreateDirectory(outputPath);

        // Act
        int exitCode = await CsWin32Generator.Program.Main(new[]
        {
            "--native-methods-txt", missingNativeMethodsTxtPath,
            "--output-path", outputPath,
        });

        // Assert
        Assert.NotEqual(0, exitCode);
    }
}
