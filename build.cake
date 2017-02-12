#addin "Cake.FileHelpers"
#addin "Cake.Json"

var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");

var isAppVeyor = AppVeyor.IsRunningOnAppVeyor;
var isPullRequest = isAppVeyor && AppVeyor.Environment.PullRequest.IsPullRequest;
var isMasterBranch = isAppVeyor && AppVeyor.Environment.Repository.Branch.Equals("master", StringComparison.OrdinalIgnoreCase);
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

Func<string, string> getEnvironmentVariable = name =>
{
    var value = EnvironmentVariable(name);
    if (string.IsNullOrEmpty(value))
    {
        throw new Exception($"Environment variable '{name}' is not set");
    }

    return value;
};

Func<string, Tuple<string, string>> getNugetFeedSettings = x =>
{
    var urlVariableName = $"{x}_URL";
    var url = getEnvironmentVariable(urlVariableName);

    var apiKeyVariableName = $"{x}_APIKEY";
    var apiKey = getEnvironmentVariable(apiKeyVariableName);

    return Tuple.Create(url, apiKey);
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
        OutputType = GitVersionOutput.Json,
        UpdateAssemblyInfo = true
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

Task("PublishPackages")
    .IsDependentOn("CreatePackages")
    .WithCriteria(isAppVeyor)
    .WithCriteria(!isPullRequest)
    .WithCriteria(isMasterBranch)
    .Does(() =>
{
    var feedSettings = getNugetFeedSettings("CI_NUGET_FEED");
    var nugetPackages = GetFiles($"./{artifactsDirectoryName}/*.nupkg");

    NuGetPush(nugetPackages, new NuGetPushSettings
    {
        Source = feedSettings.Item1,
        ApiKey = feedSettings.Item2
    });
});

Task("CreateRelease")
    .IsDependentOn("UnitTests")
    .WithCriteria(isAppVeyor)
    .WithCriteria(isPullRequest)
    .Does(() =>
{
    var prFile = "./temp/pr.json";
    DownloadFile(
        $"https://api.github.com/repos/mderriey/verion-release-brown-bag/pulls/{AppVeyor.Environment.PullRequest.Number}",
        prFile);

    var prData = ParseJsonFromFile(prFile);
    var sourceBranch = prData?["head"]?.Value<string>("ref");

    Information("Found that the PR comes from branch {0}", sourceBranch ?? "(couldn't determine branch)");
    if (string.IsNullOrEmpty(sourceBranch) || !sourceBranch.StartsWith("release", StringComparison.OrdinalIgnoreCase))
    {
        Information("Pull request is not from a release branch, skipping creation of the release");
        return;
    }

    var githubUsername = getEnvironmentVariable("GITHUB_USERNAME");
    var githubToken = getEnvironmentVariable("GITHUB_TOKEN");

    GitReleaseManagerCreate(githubUsername, githubToken, "mderriey", "version-release-brown-bag", new GitReleaseManagerCreateSettings
    {
        Milestone = majorMinorPatch,
        Name = majorMinorPatch,
        Prerelease = true,
        TargetCommitish = "master"
    });
});

Task("Default")
    .IsDependentOn("PublishPackages")
    .IsDependentOn("CreateRelease");

RunTarget(target);