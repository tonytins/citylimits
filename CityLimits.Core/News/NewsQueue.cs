namespace CityLimits.Core.News;

/// <summary>
/// The lines waiting for the news ticker. A disaster goes before everything, an advisory or an
/// ordinance's news before any headline, and lines of the same rank keep their order. A line already waiting isn't queued
/// twice, so a warning the game keeps raising doesn't fill the ticker, and the queue holds at most
/// <see cref="Capacity"/> lines, dropping the oldest headline, or else the oldest line, to make room.
/// </summary>
public sealed class NewsQueue
{
    /// <summary>The most lines the queue holds.</summary>
    public const int Capacity = 16;

    private readonly List<NewsItem> _waiting = new(Capacity);

    /// <summary>How many lines are waiting.</summary>
    public int Count => _waiting.Count;

    /// <summary>Queues a line behind the others of its kind, unless the same line is already waiting.</summary>
    /// <exception cref="ArgumentException">The line has no text.</exception>
    public void Post(NewsItem item)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(item.Text, nameof(item));
        if (_waiting.Contains(item))
        {
            return;
        }

        if (_waiting.Count == Capacity)
        {
            var headline = _waiting.FindIndex(waiting => waiting.Kind == NewsKind.Headline);
            _waiting.RemoveAt(headline >= 0 ? headline : 0);
        }

        // After every line that goes before it or with it, and before the first that goes after it.
        var at = _waiting.FindIndex(waiting => Rank(waiting.Kind) < Rank(item.Kind));
        _waiting.Insert(at >= 0 ? at : _waiting.Count, item);
    }

    /// <summary>Takes the next line to show, when there is one.</summary>
    public bool TryNext(out NewsItem item)
    {
        if (_waiting.Count == 0)
        {
            item = default;
            return false;
        }

        item = _waiting[0];
        _waiting.RemoveAt(0);
        return true;
    }

    private static int Rank(NewsKind kind) => kind switch
    {
        NewsKind.Disaster => 2,
        NewsKind.Advisory or NewsKind.Ordinance => 1,
        _ => 0,
    };
}
