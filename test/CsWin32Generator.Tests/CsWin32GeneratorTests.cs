// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine.Completions;
using System.Text;
using Microsoft.Testing.Platform.Logging;
using Xunit;

namespace Microsoft.Windows.CsWin32.Tests;

public class CsWin32GeneratorTests
{
    public ITestOutputHelper Logger => TestContext.Current.TestOutputHelper!;

    [Fact]
    public async Task BasicNativeMethods()
    {
        Console.SetOut(new TestOutputWriter(Logger));

        // Arrange
        string nativeMethodsTxtPath = Path.Combine(Path.GetDirectoryName(typeof(CsWin32GeneratorTests).Assembly.Location)!, "TestContent", "BasicNativeMethods.txt");
        string win32winmd = Path.Combine(Path.GetDirectoryName(typeof(CsWin32GeneratorTests).Assembly.Location)!, "Windows.Win32.winmd");
        string outputPath = Path.Combine(this.GetOutputDirectory(), nameof(this.BasicNativeMethods));
        if (Directory.Exists(outputPath))
        {
            Directory.Delete(outputPath, true);
        }

        Directory.CreateDirectory(outputPath);

        Logger.WriteLine($"OutputPath: {outputPath}");

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

    //[Fact]
    //public async Task CommandLineTool_GeneratesCode_WithNativeMethodsTxtAndJson()
    //{
    //    // Arrange
    //    string nativeMethodsTxtPath = Path.Combine("test", "GenerationSandbox.Tests", "NativeMethods.txt");
    //    string nativeMethodsJsonPath = Path.Combine("test", "GenerationSandbox.Tests", "NativeMethods.json");
    //    string outputPath = Path.Combine(Path.GetTempPath(), "CsWin32GeneratorTests_Output2");
    //    Directory.CreateDirectory(outputPath);

    //    // Act
    //    int exitCode = await CsWin32Generator.Program.Main(new[]
    //    {
    //        "--native-methods-txt", nativeMethodsTxtPath,
    //        "--native-methods-json", nativeMethodsJsonPath,
    //        "--output-path", outputPath,
    //    });

    //    // Assert
    //    Assert.Equal(0, exitCode);
    //    Assert.True(Directory.GetFiles(outputPath, "*.g.cs").Any(), "No generated files found.");
    //}

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

    private string GetOutputDirectory()
    {
        return Path.Combine(Path.GetDirectoryName(typeof(CsWin32GeneratorTests).Assembly.Location)!, "TestOutput");
    }

    private class TestOutputWriter : TextWriter
    {
        public TestOutputWriter(ITestOutputHelper outputHelper)
        {
            this.outputHelper = outputHelper;
        }

        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(char[] buffer, int index, int count)
        {
            this.outputHelper.Write(new string(buffer, index, count));
        }

        private ITestOutputHelper outputHelper;
    }
}
