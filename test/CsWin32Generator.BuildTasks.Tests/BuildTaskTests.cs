// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Windows.CsWin32.BuildTasks;
using Moq;
using Xunit;

namespace Microsoft.Windows.CsWin32.Tests;

public class BuildTaskTests
{
    public ITestOutputHelper Logger => TestContext.Current.TestOutputHelper!;

    [Fact]
    public void TestBuildTasks()
    {
        Mock<IBuildEngine> buildEngine = new();
        buildEngine.Setup(x => x.LogErrorEvent(It.IsAny<BuildErrorEventArgs>())).Callback<BuildErrorEventArgs>(e => this.Logger.WriteLine(e.Message));

        CsWin32CodeGeneratorTask task = new();
        task.BuildEngine = buildEngine.Object;

        bool result = task.Execute();

        Assert.True(result);
    }
}
