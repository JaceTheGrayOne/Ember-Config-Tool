using System.IO;

namespace Ember_Config_Tool.Services;

public sealed record ResolvedEmberFolder(string ModFolder, bool IsValid, bool IsSourceTree, string Warning);

public static class ConfigLocator
{
    public static string OverridesPath(string modFolder)
    {
        return Path.Combine(modFolder, "src", "User_Config_Overrides.lua");
    }

    public static bool IsValidEmberModFolder(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            return false;
        }

        return File.Exists(Path.Combine(path, "src", "mod.lua")) &&
               Directory.Exists(Path.Combine(path, "src", "Config"));
    }

    public static bool IsEmberRepoRoot(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            return false;
        }

        return IsValidEmberModFolder(Path.Combine(path, "mods", "Ember"));
    }

    public static ResolvedEmberFolder ResolveSelectedFolder(string selectedFolder)
    {
        var full = Path.GetFullPath(selectedFolder);
        if (IsValidEmberModFolder(full))
        {
            var isSourceTree = LooksLikeSourceTreeModFolder(full);
            return new ResolvedEmberFolder(
                full,
                true,
                isSourceTree,
                isSourceTree ? "Developer warning: saving writes into mods\\Ember\\src\\User_Config_Overrides.lua in the source tree." : "");
        }

        var repoShape = Path.Combine(full, "mods", "Ember");
        if (IsValidEmberModFolder(repoShape))
        {
            return new ResolvedEmberFolder(
                Path.GetFullPath(repoShape),
                true,
                true,
                "Developer warning: repo root selection normalized to mods\\Ember; saving writes into the source tree.");
        }

        var childShape = Path.Combine(full, "Ember");
        if (IsValidEmberModFolder(childShape))
        {
            return new ResolvedEmberFolder(Path.GetFullPath(childShape), true, false, "");
        }

        return new ResolvedEmberFolder(full, false, false, $"Expected an Ember folder containing src\\mod.lua and src\\Config: {full}");
    }

    public static bool TryAutoDetect(out ResolvedEmberFolder resolved)
    {
        foreach (var start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
        {
            var directory = Directory.Exists(start)
                ? new DirectoryInfo(start)
                : Directory.GetParent(start);

            while (directory is not null)
            {
                var candidate = ResolveSelectedFolder(directory.FullName);
                if (candidate.IsValid)
                {
                    resolved = candidate;
                    return true;
                }

                directory = directory.Parent;
            }
        }

        resolved = new ResolvedEmberFolder("", false, false, "");
        return false;
    }

    private static bool LooksLikeSourceTreeModFolder(string modFolder)
    {
        var directory = new DirectoryInfo(Path.GetFullPath(modFolder));
        return directory.Name.Equals("Ember", StringComparison.OrdinalIgnoreCase) &&
               directory.Parent?.Name.Equals("mods", StringComparison.OrdinalIgnoreCase) == true &&
               IsEmberRepoRoot(directory.Parent?.Parent?.FullName);
    }
}

public sealed class AppOptions
{
    public string ModFolder { get; init; } = "";
    public string EmberRepoRoot { get; init; } = "";
    public bool SmokeTest { get; init; }
    public bool WorkflowSmokeTest { get; init; }

    public static AppOptions Parse(IEnumerable<string> args)
    {
        var tokens = args.ToList();
        var options = new AppOptionsBuilder();

        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            if (TryReadOption(token, "--mod-folder", tokens, ref i, out var modFolder))
            {
                options.ModFolder = modFolder;
            }
            else if (TryReadOption(token, "--ember-repo-root", tokens, ref i, out var repoRoot))
            {
                options.EmberRepoRoot = repoRoot;
            }
            else if (token.Equals("--smoke-test", StringComparison.OrdinalIgnoreCase))
            {
                options.SmokeTest = true;
            }
            else if (token.Equals("--workflow-smoke-test", StringComparison.OrdinalIgnoreCase))
            {
                options.WorkflowSmokeTest = true;
            }
        }

        return options.ToOptions();
    }

    private static bool TryReadOption(string token, string name, IReadOnlyList<string> tokens, ref int index, out string value)
    {
        value = "";
        if (token.StartsWith(name + "=", StringComparison.OrdinalIgnoreCase))
        {
            value = token[(name.Length + 1)..];
            return true;
        }

        if (!token.Equals(name, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (index + 1 >= tokens.Count)
        {
            throw new ArgumentException($"{name} requires a value.");
        }

        index++;
        value = tokens[index];
        return true;
    }

    private sealed class AppOptionsBuilder
    {
        public string ModFolder { get; set; } = "";
        public string EmberRepoRoot { get; set; } = "";
        public bool SmokeTest { get; set; }
        public bool WorkflowSmokeTest { get; set; }

        public AppOptions ToOptions()
        {
            return new AppOptions
            {
                ModFolder = ModFolder,
                EmberRepoRoot = EmberRepoRoot,
                SmokeTest = SmokeTest,
                WorkflowSmokeTest = WorkflowSmokeTest
            };
        }
    }
}
