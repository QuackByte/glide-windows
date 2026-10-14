using static Glide.Core.Tests.TestSupport;

namespace Glide.Core.Tests;

public class AppMemoryTests
{
    private const string Main = @"\\.\DISPLAY1";

    private static AppMemory.Strip StripOf(int workspace, params (string App, int Width)[] columns) =>
        new(Main, workspace, 1212, [.. columns.Select(c => (Col([1], widthIndex: c.Width), (IReadOnlyList<string?>)[c.App]))]);

    [Fact]
    public void RemembersEachAppsWorkspaceWidthAndPlace()
    {
        var memory = new AppMemory();
        memory.Learn([StripOf(0, ("terminal.exe", 0), ("code.exe", 2)), StripOf(2, ("discord.exe", 1))]);
        Assert.Equal(new AppPlacement(Main, 0, 2), memory.Lookup("CODE.EXE")); // App IDs ignore case
        Assert.Equal(2, memory.Lookup("discord.exe")?.Workspace);
        Assert.Equal(["terminal.exe", "code.exe"], memory.Order[AppMemory.Key(Main, 0)]);
        Assert.Null(memory.Lookup("notepad.exe"));
    }

    [Fact]
    public void ClosedAppsKeepTheirPlace()
    {
        var memory = new AppMemory();
        memory.Learn([StripOf(0, ("terminal.exe", 0), ("discord.exe", 1), ("code.exe", 2))]);
        // Discord closes; a browser opens at the end.
        memory.Learn([StripOf(0, ("terminal.exe", 0), ("code.exe", 2), ("msedge.exe", 1))]);
        Assert.Equal(["terminal.exe", "discord.exe", "code.exe", "msedge.exe"], memory.Order[AppMemory.Key(Main, 0)]);
        Assert.Equal(1, memory.Lookup("discord.exe")?.WidthIndex);
    }

    [Fact]
    public void AnAppThatMovedLeavesItsOldStrip()
    {
        var memory = new AppMemory();
        memory.Learn([StripOf(0, ("terminal.exe", 0), ("discord.exe", 1))]);
        memory.Learn([StripOf(0, ("terminal.exe", 0)), StripOf(3, ("discord.exe", 1))]);
        Assert.Equal(["terminal.exe"], memory.Order[AppMemory.Key(Main, 0)]);
        Assert.Equal(3, memory.Lookup("discord.exe")?.Workspace);
    }

    [Fact]
    public void NewWindowsGoBackBetweenTheAppsAroundThem()
    {
        var memory = new AppMemory();
        memory.Learn([StripOf(0, ("terminal.exe", 0), ("discord.exe", 1), ("code.exe", 2))]);
        string?[] open = ["terminal.exe", "code.exe"];
        Assert.Equal(1, memory.InsertionIndex(Main, 0, "discord.exe", open));
        Assert.Equal(2, memory.InsertionIndex(Main, 0, "code.exe", ["terminal.exe", "msedge.exe"]));
        Assert.Null(memory.InsertionIndex(Main, 0, "notepad.exe", open)); // No remembered place
        Assert.Null(memory.InsertionIndex(Main, 4, "discord.exe", open));
    }

    [Fact]
    public void ArrangesWindowsIntoTheRememberedOrder()
    {
        var memory = new AppMemory();
        memory.Learn([StripOf(0, ("terminal.exe", 0), ("discord.exe", 1), ("code.exe", 2))]);
        string[] adopted = ["code.exe", "notepad.exe", "terminal.exe", "discord.exe"];
        Assert.Equal(["terminal.exe", "discord.exe", "code.exe", "notepad.exe"], memory.Arrange(Main, 0, adopted, a => a));
    }

    [Fact]
    public void CustomWidthsAreKeptAsAShareOfTheScreen()
    {
        var memory = new AppMemory();
        memory.Learn([new AppMemory.Strip(Main, 0, 1200, [(Col([1], customWidth: 900), ["code.exe"])])]);
        Assert.Equal(0.75, memory.Lookup("code.exe")?.WidthFraction);
    }

    [Fact]
    public void RoundTripsThroughJson()
    {
        var memory = new AppMemory();
        memory.Learn([StripOf(0, ("terminal.exe", 0), ("code.exe", 2))]);
        var decoded = AppMemory.FromJson(memory.ToJson());
        Assert.Equal(memory.Lookup("code.exe"), decoded.Lookup("Code.exe"));
        Assert.Equal(memory.Order[AppMemory.Key(Main, 0)], decoded.Order[AppMemory.Key(Main, 0)]);
    }
}
