# Fixed Missing References and Central Package Management

## Summary of Changes

I've successfully resolved the missing references and updated the projects to work with central package management. Here's what was fixed:

## 1. Updated Directory.Packages.props

Added missing package versions to support the new projects:

- **Microsoft.Build.Framework**: Version 16.11.6 (for MSBuild tasks)
- **System.CommandLine.DragonFruit**: Version 0.4.0-alpha.22272.1 (for command line API compatibility)

## 2. CsWin32Generator Project (src/CsWin32Generator/CsWin32Generator.csproj)

**Added Package References:**
- `Microsoft.CodeAnalysis.CSharp` - For Roslyn compilation APIs
- `Microsoft.CodeAnalysis.Common` - For core compilation types
- `System.CommandLine` - For command line argument parsing

**Added Project Reference:**
- Reference to `Microsoft.Windows.CsWin32` project to access Generator classes

**Fixed Code Issues:**
- Added missing using statement: `using Microsoft.CodeAnalysis.CSharp.Syntax;`
- Added missing using statement: `using System.CommandLine.Invocation;`
- Fixed SetHandler API call to use `InvocationContext` parameter
- Commented out banned APIs check (needs public API access in future)

## 3. Microsoft.Windows.CsWin32.BuildTasks Project (src/Microsoft.Windows.CsWin32.BuildTasks/Microsoft.Windows.CsWin32.BuildTasks.csproj)

**Updated Package References:**
- Removed version numbers from `Microsoft.Build.Utilities.Core` and `Microsoft.Build.Framework`
- Added proper `PrivateAssets="all"` attributes for MSBuild dependencies

**Central Package Management Compliance:**
- All package references now use central version management
- No hardcoded versions in project files

## 4. Build Verification

? **Build Status:** All projects now compile successfully
? **Dependencies:** All missing references resolved
? **Package Management:** Fully compliant with central package management
? **Demo Project:** Compiles without errors

## Notes for Future Development

1. **Banned APIs Access**: The `Generator.GetBannedAPIs()` method is internal/private. Consider making it public or adding a public property if the command line tool needs access to banned APIs checking.

2. **System.CommandLine Version**: Using the beta version of System.CommandLine 2.0. The API changed between versions, so we used the `InvocationContext` approach for better compatibility.

3. **Project Structure**: Both new projects are now properly integrated into the solution and follow the repository's conventions for package management.

## Testing

After these changes:
- All projects compile successfully
- No missing reference errors
- Central package management working correctly
- Demo project builds without issues

The MSBuild task infrastructure is now ready for testing and use.