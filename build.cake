#addin "Cake.FileHelpers"
#addin "Cake.Json"

var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");

var isAppVeyor = AppVeyor.IsRunningOnAppVeyor;
var artifactsDirectoryName = "build-artifacts";

var sourceDirectory = Directory("./src").Path;
var testResultsDirectory = Directory("./test-results").Path;
var artifactsDirectory = Directory($"./{artifactsDirectoryName}").Path;

var projectName = "MickaelDerriey.UsefulExtensions";
var testProjectName = "MickaelDerriey.UsefulExtensions.Tests";
var projectsNames = new[] { projectName, testProjectName };

var majorMinorPatch = string.Empty;
string versionSuffix = null;

Func<string, string> getProjectDirectoryPath = x =>
{
    return sourceDirectory.Combine(x).FullPath;
};

Setup(context =>
{
    CreateDirectory(testResultsDirectory);
    CreateDirectory(artifactsDirectory);

    if (isAppVeyor)
    {
        GitVersion(new GitVersionSettings
        {
            OutputType = GitVersionOutput.BuildServer
        });
    }

    var version = GitVersion(new GitVersionSettings
    {
        OutputType = GitVersionOutput.Json
    });

    majorMinorPatch = version.MajorMinorPatch;
    if (version.LegacySemVerPadded.IndexOf($"{majorMinorPatch}-") > -1)
    {
        versionSuffix = version.LegacySemVerPadded.Substring(majorMinorPatch.Length + 1);
    }

    Information("Calculated semantic version is {0}", version.SemVer);
    Information("Calculated NuGet base version is {0}", majorMinorPatch);
    Information("Calculated NuGet prerelease tag is {0}", versionSuffix ?? "empty");
});

Teardown(context =>
{
    if (isAppVeyor)
    {
        var artifacts = GetFiles($"./{artifactsDirectoryName}/*.*");
        foreach (var artifact in artifacts)
        {
            AppVeyor.UploadArtifact(artifact);
        }
    }
});

Task("UpdateVersion")
    .Does(() =>
{
    var projectJsonFile = sourceDirectory
        .Combine(projectName)
        .CombineWithFilePath("project.json");

    var json = ParseJsonFromFile(projectJsonFile);
    json["version"] = string.Format("{0}-*", majorMinorPatch);

    FileWriteText(projectJsonFile, json.ToString());
});

Task("Restore")
    .IsDependentOn("UpdateVersion")
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
        OutputDirectory = artifactsDirectory,
        VersionSuffix = versionSuffix
    });
});

Task("Default")
    .IsDependentOn("CreatePackages");

RunTarget(target);