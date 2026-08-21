namespace PubgMortarRanger.Tests.Packaging;

public sealed class InstallerDefinitionTests
{
    [Fact]
    public void Installer_RemovesPreviouslyWrappedExecutableBeforeUpgrade()
    {
        var repositoryRoot = FindRepositoryRoot();
        var installerDefinition = File.ReadAllText(
            Path.Combine(repositoryRoot, "installer", "PubgMortarRanger.iss"));

        Assert.Contains("[InstallDelete]", installerDefinition);
        Assert.Contains("HD_MortarRangefinder.exe", installerDefinition);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PubgMortarRanger.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
