using GafiFish.App.Logging;
using Xunit;

namespace GafiFish.App.Tests;

public sealed class ActivityLogStoreTests
{
    [Fact]
    public void LoadRecent_ReturnsNewestEntriesInChronologicalOrder()
    {
        var directory = CreateTemporaryDirectory();
        var path = Path.Combine(directory, "activity.log");
        try
        {
            var store = new ActivityLogStore(path);
            store.Append("one");
            store.Append("two");
            store.Append("three");
            store.Append("four");

            Assert.Equal(new[] { "three", "four" }, store.LoadRecent(2));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Append_RotatesActiveFileAndKeepsPreviousEntries()
    {
        var directory = CreateTemporaryDirectory();
        var path = Path.Combine(directory, "activity.log");
        try
        {
            var store = new ActivityLogStore(path, maximumFileBytes: 12);
            store.Append("first");
            store.Append("second");

            Assert.Equal(new[] { "first", "second" }, store.LoadRecent(10));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Clear_RemovesActiveAndArchivedLogs()
    {
        var directory = CreateTemporaryDirectory();
        var path = Path.Combine(directory, "activity.log");
        try
        {
            var store = new ActivityLogStore(path, maximumFileBytes: 12);
            store.Append("first");
            store.Append("second");
            store.Clear();

            Assert.Empty(store.LoadRecent());
            Assert.False(File.Exists(path));
            Assert.False(File.Exists(path + ".1"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void LoadRecent_RejectsInvalidLimits()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var store = new ActivityLogStore(Path.Combine(directory, "activity.log"));
            Assert.Throws<ArgumentOutOfRangeException>(() => store.LoadRecent(0));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"gafifish-log-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }
}
