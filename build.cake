var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");

var sourceDirectory = Directory("./src").Path;
var testResultsDirectory = Directory("./test-results").Path;
var artifactsDirectory = Directory("./build-artifacts").Path;

var projectName = "MickaelDerriey.UsefulExtensions";
var testProjectName = "MickaelDerriey.UsefulExtensions.Tests";
var projectsNames = new[] { projectName, testProjectName };

Func<string, string> getProjectDirectoryPath = x =>
{
    return sourceDirectory.Combine(x).FullPath;
};

Setup(() =>
{
    CreateDirectory(testResultsDirectory);
    CreateDirectory(artifactsDirectory);
});

Task("Restore")
    .Does(() =>
{
    DotNetCoreRestore(sourceDirectory.FullPath);
});

Task("Build")
    .IsDependentOn("Restore")
    .Does(() =>
{
    foreach (var projectName in projectsNames)
    {
        Information("Building {0}...", projectName);
        DotNetCoreBuild(getProjectDirectoryPath(projectName), new DotNetCoreBuildSettings
        {
            Configuration = configuration
        });
    }
});

Task("UnitTests")
    .IsDependentOn("Build")
    .Does(() =>
{
    Information("Running tests for {0}...", testProjectName);
    DotNetCoreTest(getProjectDirectoryPath(testProjectName), new DotNetCoreTestSettings
    {
        Configuration = configuration,
        NoBuild = true,
        ArgumentCustomization = x => x
            .Append("-xml")
            .AppendQuoted(testResultsDirectory.CombineWithFilePath("test-results.xml").FullPath)
    });
});

Task("CreatePackages")
    .IsDependentOn("UnitTests")
    .Does(() =>
{
    Information("Creating NuGet package for {0}", projectName);
    DotNetCorePack(getProjectDirectoryPath(projectName), new DotNetCorePackSettings
    {
        Configuration = configuration,
        NoBuild = true,
        OutputDirectory = artifactsDirectory
    });
});

Task("Default")
    .IsDependentOn("CreatePackages")
    .Does(() =>
{
    Information("Hello from the Default task with configuration {0}", configuration);
});

RunTarget(target);