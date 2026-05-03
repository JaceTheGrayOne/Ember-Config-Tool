using System.IO;
using System.Windows;
using Ember_Config_Tool.Services;
using Ember_Config_Tool.ViewModels;

namespace Ember_Config_Tool;

public partial class App : System.Windows.Application
{
    public App()
    {
        DispatcherUnhandledException += (_, args) =>
        {
            ReportStartupException(args.Exception);
            args.Handled = true;
            Shutdown(1);
        };
    }

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        try
        {
            base.OnStartup(e);
            var options = AppOptions.Parse(e.Args);
            if (options.SmokeTest)
            {
                Shutdown(RunSmokeTest(options));
                return;
            }

            if (options.WorkflowSmokeTest)
            {
                Shutdown(RunWorkflowSmokeTest(options));
                return;
            }

            new MainWindow(options).Show();
        }
        catch (Exception ex)
        {
            ReportStartupException(ex);
            Shutdown(1);
        }
    }

    private static int RunSmokeTest(AppOptions options)
    {
        try
        {
            var metadata = ConfigMetadata.LoadDefault();
            ConfigDataset dataset;
            if (!string.IsNullOrWhiteSpace(options.EmberRepoRoot))
            {
                var resolved = ConfigLocator.ResolveSelectedFolder(options.EmberRepoRoot);
                dataset = resolved.IsValid ? ConfigSourceLoader.LoadFromModFolder(resolved.ModFolder) : ConfigSourceLoader.LoadSnapshot();
            }
            else if (!string.IsNullOrWhiteSpace(options.ModFolder))
            {
                var resolved = ConfigLocator.ResolveSelectedFolder(options.ModFolder);
                dataset = resolved.IsValid ? ConfigSourceLoader.LoadFromModFolder(resolved.ModFolder) : ConfigSourceLoader.LoadSnapshot();
            }
            else
            {
                dataset = ConfigSourceLoader.LoadSnapshot();
            }

            return MetadataValidator.Validate(dataset, metadata).Count == 0 ? 0 : 1;
        }
        catch
        {
            return 1;
        }
    }

    private static int RunWorkflowSmokeTest(AppOptions options)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(options.ModFolder))
            {
                Console.Error.WriteLine("--workflow-smoke-test requires --mod-folder.");
                return 1;
            }

            var resolved = ConfigLocator.ResolveSelectedFolder(options.ModFolder);
            if (!resolved.IsValid || resolved.IsSourceTree)
            {
                Console.Error.WriteLine("Workflow smoke target must be a valid disposable Ember mod folder.");
                return 1;
            }

            var realRepoOverride = FindRealRepoOverridePath();
            var realRepoExisted = realRepoOverride is not null && File.Exists(realRepoOverride);
            var realRepoHash = realRepoExisted ? File.ReadAllBytes(realRepoOverride!).Length + ":" + File.GetLastWriteTimeUtc(realRepoOverride!) : "";

            var overridePath = ConfigLocator.OverridesPath(resolved.ModFolder);
            File.WriteAllText(overridePath, SeedWorkflowOverride(), new System.Text.UTF8Encoding(false));

            var vm = new MainViewModel(new AppOptions { ModFolder = resolved.ModFolder });
            if (vm.BlockingIssues.Count != 0)
            {
                Console.Error.WriteLine(string.Join(Environment.NewLine, vm.BlockingIssues));
                return 1;
            }

            var logLevel = FindSetting(vm, "LOG_LEVEL");
            logLevel.ApplyOverride(ConfigValueState.Value, new LuaNumberValue(1, "1"));

            var glider = FindTable(vm, "GliderConfig");
            var yaw = glider.Rows.First(row => row.Identity == "T1").Cells.First(cell => cell.Column.Key == "yawAngleSpeed");
            yaw.ApplyOverride(ConfigValueState.Value, new LuaNumberValue(93, "93"));

            vm.SaveCommand.Execute(null);
            if (vm.BlockingIssues.Count != 0)
            {
                Console.Error.WriteLine(string.Join(Environment.NewLine, vm.BlockingIssues));
                return 1;
            }

            var saved = File.ReadAllText(overridePath);
            if (!saved.Contains("-- manual before", StringComparison.Ordinal) ||
                !saved.Contains("-- manual after", StringComparison.Ordinal))
            {
                Console.Error.WriteLine("Manual Lua was not preserved.");
                return 1;
            }

            if (!saved.Contains("BlockMaterialReplacements = {", StringComparison.Ordinal) ||
                !saved.Contains("materialIndex = 184", StringComparison.Ordinal))
            {
                Console.Error.WriteLine("Hidden BlockMaterialReplacements override was not preserved.");
                return 1;
            }

            var reloaded = new MainViewModel(new AppOptions { ModFolder = resolved.ModFolder });
            if (FindSetting(reloaded, "LOG_LEVEL").ValueText != "1")
            {
                Console.Error.WriteLine("Reloaded LOG_LEVEL state did not match saved value.");
                return 1;
            }

            var reloadedYaw = FindTable(reloaded, "GliderConfig").Rows.First(row => row.Identity == "T1").Cells.First(cell => cell.Column.Key == "yawAngleSpeed");
            if (reloadedYaw.ValueText != "93" || reloadedYaw.State != ConfigValueState.Value)
            {
                Console.Error.WriteLine("Reloaded GliderConfig.T1.yawAngleSpeed state did not match saved value.");
                return 1;
            }

            if (realRepoOverride is not null)
            {
                var realRepoStillExisted = File.Exists(realRepoOverride);
                var realRepoHashAfter = realRepoStillExisted ? File.ReadAllBytes(realRepoOverride).Length + ":" + File.GetLastWriteTimeUtc(realRepoOverride) : "";
                if (realRepoStillExisted != realRepoExisted || realRepoHashAfter != realRepoHash)
                {
                    Console.Error.WriteLine("Real source-tree User_Config_Overrides.lua changed during workflow smoke.");
                    return 1;
                }
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static string SeedWorkflowOverride()
    {
        return string.Join(Environment.NewLine, new[]
        {
            "-- manual before",
            "Manual_WorkflowSmoke = true",
            "",
            LuaOverrideWriter.BeginMarker,
            "-- schema_version = 1",
            "LOG_LEVEL = 2",
            "GliderConfig.T1.yawAngleSpeed = 91",
            "for _, row in ipairs(ExpandedGameSettings_Config or {}) do",
            "    if row.Name == \"playerMana\" then",
            "        row.Max = 1200",
            "    end",
            "end",
            "BlockMaterialReplacements = {",
            "    { enabled = true, targetGuid = \"11111111-1111-1111-1111-111111111111\", materialIndex = 184 },",
            "}",
            LuaOverrideWriter.EndMarker,
            "",
            "-- manual after",
            "Manual_WorkflowSmoke_After = true",
            ""
        });
    }

    private static SettingViewModel FindSetting(MainViewModel vm, string key)
    {
        return vm.Groups.SelectMany(group => group.Items).OfType<SettingViewModel>().Single(item => item.Key == key);
    }

    private static TableSettingViewModel FindTable(MainViewModel vm, string key)
    {
        return vm.Groups.SelectMany(group => group.Items).OfType<TableSettingViewModel>().Single(item => item.Key == key);
    }

    private static string? FindRealRepoOverridePath()
    {
        var directory = new DirectoryInfo(Environment.CurrentDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "mods", "Ember", "src", "User_Config_Overrides.lua");
            if (ConfigLocator.IsEmberRepoRoot(directory.FullName))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static void ReportStartupException(Exception ex)
    {
        var logPath = Path.Combine(AppContext.BaseDirectory, "Ember_Config_Tool_Error.log");
        try
        {
            File.WriteAllText(logPath, ex.ToString());
        }
        catch
        {
            logPath = "";
        }

        try
        {
            var message = string.IsNullOrWhiteSpace(logPath)
                ? ex.Message
                : $"{ex.Message}{Environment.NewLine}{Environment.NewLine}Details were written to:{Environment.NewLine}{logPath}";
            System.Windows.MessageBox.Show(message, "Ember Config Tool startup error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch
        {
        }
    }
}
