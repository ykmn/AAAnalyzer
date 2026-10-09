using SystemAudioAnalyzer.App.Settings;
using Xunit;

namespace SystemAudioAnalyzer.App.Tests;

public sealed class UrlLibraryTests
{
    [Fact]
    public async Task StoreRoundTripsEntries()
    {
        var directory = Path.Combine(Path.GetTempPath(), "AAAnalyzerTests", Guid.NewGuid().ToString("N"));
        var store = new UrlLibraryStore(directory);
        Assert.Empty(await store.LoadAsync());

        var entries = new[]
        {
            new UrlLibraryEntry("Radio One", "http://radio.example/one"),
            new UrlLibraryEntry("Radio Two", "http://radio.example/two"),
        };
        await store.SaveAsync(entries);

        var loaded = await store.LoadAsync();

        Assert.Equal(entries, loaded);
    }

    [Fact]
    public async Task StoreIgnoresACorruptFile()
    {
        var directory = Path.Combine(Path.GetTempPath(), "AAAnalyzerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var store = new UrlLibraryStore(directory);
        await File.WriteAllTextAsync(store.Path, "not json");

        Assert.Empty(await store.LoadAsync());
    }

    [Fact]
    public void ParserReadsExtinfNames()
    {
        var lines = new[]
        {
            "#EXTM3U",
            "#EXTINF:-1,Radio One",
            "http://radio.example/one",
            "#EXTINF:-1,Radio Two",
            "http://radio.example/two",
        };

        var entries = M3uPlaylistParser.Parse(lines);

        Assert.Equal(
            [new UrlLibraryEntry("Radio One", "http://radio.example/one"), new UrlLibraryEntry("Radio Two", "http://radio.example/two")],
            entries);
    }

    [Fact]
    public void ParserNamesBareUrlsAfterThemselves()
    {
        var entries = M3uPlaylistParser.Parse(["http://radio.example/bare"]);

        Assert.Equal([new UrlLibraryEntry("http://radio.example/bare", "http://radio.example/bare")], entries);
    }

    [Fact]
    public void ParserSkipsBlankAndCommentLines()
    {
        var entries = M3uPlaylistParser.Parse(["#EXTM3U", "", "   ", "#EXTINF:-1,Named", "http://radio.example/named"]);

        Assert.Equal([new UrlLibraryEntry("Named", "http://radio.example/named")], entries);
    }

    [Fact]
    public void WriterRoundTripsThroughTheParser()
    {
        var entries = new[]
        {
            new UrlLibraryEntry("Radio One", "http://radio.example/one"),
            new UrlLibraryEntry("Radio Two", "http://radio.example/two"),
        };

        var lines = M3uPlaylistWriter.Write(entries).ToArray();
        var parsed = M3uPlaylistParser.Parse(lines);

        Assert.Equal("#EXTM3U", lines[0]);
        Assert.Equal(entries, parsed);
    }

    [Fact]
    public void ImportSkipsUrlsAlreadyInTheLibrary()
    {
        var viewModel = new SystemAudioAnalyzer.App.ViewModels.UrlLibraryViewModel();
        viewModel.Load([new UrlLibraryEntry("Existing", "http://radio.example/existing")]);

        var added = viewModel.ImportPlaylist(
        [
            new UrlLibraryEntry("Existing", "http://radio.example/existing"),
            new UrlLibraryEntry("New", "http://radio.example/new"),
        ]);

        Assert.Equal(1, added);
        Assert.Equal(2, viewModel.Entries.Count);
    }
}
