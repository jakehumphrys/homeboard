using HomeBoard.Models;
using Microsoft.Extensions.Options;

namespace HomeBoard.Services;
using System.ServiceModel.Syndication;
using System.Xml;
using Microsoft.Extensions.Caching.Memory;

public class RssService(HttpClient http, IMemoryCache cache, IOptions<List<FeedSource>> feeds)
{
    public async Task<List<FeedItem>> GetNewsAsync(int max = 30)
    {
        var enabled = feeds.Value.Where(f => f.IsEnabled);
        var results = await Task.WhenAll(enabled.Select(f => GetFeedAsync(f, max)));

        return results
            .SelectMany(r => r)
            .DistinctBy(i => i.Title.Trim(), StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(i => i.Date)
            .Take(max)
            .ToList();
    }

    public async Task<List<FeedItem>> GetFeedAsync(FeedSource source, int max = 20)
    {
        if (cache.TryGetValue(source.Url, out List<FeedItem>? cached) && cached is not null)
            return cached;

        try
        {
            await using var stream = await http.GetStreamAsync(source.Url);
            using var reader = XmlReader.Create(stream);
            var feed = SyndicationFeed.Load(reader);

            var items = feed.Items
                .Select(i => new FeedItem(
                    i.Title?.Text ?? "(no title)",
                    i.Links.FirstOrDefault()?.Uri.ToString() ?? "",
                    source.Name,
                    (i.PublishDate != default ? i.PublishDate : i.LastUpdatedTime).LocalDateTime))
                .DistinctBy(i => i.Title.Trim(), StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(i => i.Date)
                .Take(max)
                .ToList();

            cache.Set(source.Url, items, TimeSpan.FromMinutes(15));
            return items;
        }
        catch (Exception)
        {
            return []; // add logging here later
        }
    }
}