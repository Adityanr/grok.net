// Install addins.
#addin "nuget:https://api.nuget.org/v3/index.json?package=Cake.Coveralls&version=1.0.0"
#addin nuget:?package=Microsoft.NET.Test.Sdk&version=16.11.0

// Install tools
#tool "nuget:https://api.nuget.org/v3/index.json?package=coveralls.io&version=1.4.2"
#tool nuget:?package=Microsoft.NET.Test.Sdk&version=16.11.0
#tool nuget:?package=OpenCover&version=4.7.1221

var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");

var artifactsDir = "./artifacts/";
var projectFile = "./src/Grok.Net/Grok.Net.csproj";
var testResultFile = "./test-results/results.xml";
var coverallsRepoToken = EnvironmentVariable("COVERALLS_REPO_TOKEN");

Task("Clean")
    .Does(() =>
{
    CleanDirectory(artifactsDir);

    if(BuildSystem.IsLocalBuild)
    {
        CleanDirectories(GetDirectories("./**/obj") + GetDirectories("./**/bin"));
    }
});

Task("Restore-NuGet-Packages")
    .IsDependentOn("Clean")
    .Does(() =>
{
    DotNetCoreRestore(projectFile);
});

Task("Build")
    .IsDependentOn("Restore-NuGet-Packages")
    .Does(() =>
{
    DotNetCoreBuild(projectFile, new DotNetCoreBuildSettings()
    { 
        Configuration = configuration,
        NoRestore = true
    });
});

Task("Run-Unit-Tests")
    .IsDependentOn("Build")
    .Does(() =>
{
    var projectFile = Directory("./src/Grok.Net.Tests") + File("Grok.Net.Tests.csproj");

    if(BuildSystem.IsLocalBuild || BuildSystem.IsPullRequest)
    {
        DotNetCoreTest(System.IO.Path.GetFullPath(projectFile), new DotNetCoreTestSettings()
        {
            Configuration = configuration
        });
    }
    else
    {
        OpenCover(tool => {
                tool.DotNetCoreTest(
                System.IO.Path.GetFullPath(projectFile),
                new DotNetCoreTestSettings()
                {
                    Configuration = configuration
                }
                );
            },
            testResultFile, new OpenCoverSettings() {
                Filters = { "-[*xunit*]*", "-[GrokNetTests.UnitTests]*", "+[*]*" },
                LogLevel = OpenCoverLogLevel.Debug
            }
        );
    }
});

Task("Upload-Coverage-Report")
    .IsDependentOn("Run-Unit-Tests")
    .WithCriteria((context) => !BuildSystem.IsLocalBuild)
    .WithCriteria((context) => !BuildSystem.IsPullRequest)
    .WithCriteria((context) => FileExists(testResultFile))
    .Does(() =>
{
    CoverallsIo("./coverage/results.xml", new CoverallsIoSettings()
    {
        RepoToken = coverallsRepoToken,
        Debug = true
    });
});

Task("NuGet-Pack")
    .IsDependentOn("Upload-Coverage-Report")
    .Does(() =>
{
    DotNetCorePack(projectFile, new DotNetCorePackSettings()
    {
        Configuration = configuration,
        NoRestore = true,
        NoBuild = true,
        OutputDirectory = artifactsDir
    });
});

Task("Default")
    .IsDependentOn("NuGet-Pack");

RunTarget(target);
