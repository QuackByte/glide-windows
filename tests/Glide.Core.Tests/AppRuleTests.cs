using System.Text.Json;
using static Glide.Core.Tests.TestSupport;

namespace Glide.Core.Tests;

public class AppRuleTests
{
    [Fact]
    public void SettingsFloatsByDefault()
    {
        Assert.Equal(AppRule.RuleMode.Float, new AppRules().Rule("SystemSettings.exe")?.Mode);
        Assert.Equal(AppRule.RuleMode.Float, new AppRules().Rule("systemsettings.exe")?.Mode); // Executable names ignore case
        Assert.Null(new AppRules().Rule("msedge.exe"));
        Assert.Null(new AppRules().Rule(null));
    }

    [Fact]
    public void SetReplacesInPlaceAndRemove()
    {
        var rules = new AppRules([]);
        rules.Set(new AppRule("a", "A", AppRule.RuleMode.Float));
        rules.Set(new AppRule("b", "B", WidthIndex: 0));
        rules.Set(new AppRule("a", "A", AppRule.RuleMode.Tile, Workspace: 2));
        Assert.Equal(["a", "b"], rules.Rules.Select(r => r.AppId));
        Assert.Equal(AppRule.RuleMode.Tile, rules.Rule("a")?.Mode);
        Assert.Equal(2, rules.Rule("a")?.Workspace);
        rules.Remove("a");
        Assert.Equal(["b"], rules.Rules.Select(r => r.AppId));
    }

    [Fact]
    public void DuplicatesCollapseToTheLastOne()
    {
        var rules = new AppRules([new AppRule("a", "A", AppRule.RuleMode.Float), new AppRule("a", "A", AppRule.RuleMode.Tile)]);
        Assert.Single(rules.Rules);
        Assert.Equal(AppRule.RuleMode.Tile, rules.Rule("a")?.Mode);
    }

    [Fact]
    public void ShouldTile()
    {
        var floating = new AppRule("a", "A", AppRule.RuleMode.Float);
        var tiling = new AppRule("a", "A", AppRule.RuleMode.Tile);
        static bool Decide(AppRule? rule, bool tileable = true, bool canTile = true, bool floating = false, bool tiled = false) =>
            AppRules.ShouldTile(rule, tileable, canTile, floating, tiled);
        Assert.True(Decide(null));
        Assert.False(Decide(null, tileable: false));
        Assert.False(Decide(floating)); // The rule floats a window Glide would tile
        Assert.True(Decide(tiling, tileable: false)); // The rule tiles a resizable window Glide would float
        Assert.False(Decide(tiling, tileable: false, canTile: false)); // Dialogs and fixed-size windows still float
        Assert.True(Decide(floating, tiled: true)); // A hand-made choice beats the rule
        Assert.False(Decide(tiling, floating: true));
    }

    [Fact]
    public void RulesRoundTripThroughJson()
    {
        AppRule[] rules = [new("WindowsTerminal.exe", "Terminal", AppRule.RuleMode.Tile, WidthIndex: 0, Workspace: 1)];
        var decoded = JsonSerializer.Deserialize<AppRule[]>(JsonSerializer.Serialize(rules));
        Assert.Equal(rules, decoded);
    }

    [Fact]
    public void InsertOnAnotherWorkspace()
    {
        var set = new WorkspaceSet(new Rect(0, 0, 1200, 800));
        set.Insert(1, widthIndex: 1, focus: true);
        Assert.Equal(3, set.Insert(2, widthIndex: 0, workspace: 3, focus: false));
        Assert.Equal(0, set.Workspaces[3].Strip.Columns[0].WidthIndex);
        Assert.Equal(0, set.ActiveIndex);
        Assert.Equal(0, set.Insert(3, widthIndex: 1, workspace: 42, focus: true)); // Unknown workspace falls back to the active one
    }
}
