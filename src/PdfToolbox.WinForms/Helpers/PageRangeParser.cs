using PdfToolbox.Exceptions;

namespace PdfToolbox.Helpers;

public static class PageRangeParser
{
    public static IReadOnlyList<int> Parse(string text, int pageCount)
    {
        if (pageCount <= 0) throw new ArgumentOutOfRangeException(nameof(pageCount));
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new UserFacingException("Wybierz co najmniej jedną stronę.");
        }

        var pages = new SortedSet<int>();
        foreach (var rawPart in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var part = rawPart.Replace('–', '-').Replace('—', '-');
            var rangeParts = part.Split('-', StringSplitOptions.TrimEntries);
            if (rangeParts.Length == 1 && TryPage(rangeParts[0], pageCount, out var page))
            {
                pages.Add(page);
                continue;
            }

            if (rangeParts.Length == 2 &&
                TryPage(rangeParts[0], pageCount, out var first) &&
                TryPage(rangeParts[1], pageCount, out var last) &&
                first <= last)
            {
                for (var current = first; current <= last; current++) pages.Add(current);
                continue;
            }

            throw new UserFacingException($"Nie rozpoznano zakresu „{rawPart}”. Użyj numerów stron, np. 1-3, 5, 8-12.");
        }

        if (pages.Count == 0) throw new UserFacingException("Wybierz co najmniej jedną stronę.");
        return pages.ToArray();
    }

    public static string Format(IEnumerable<int> pages)
    {
        var sorted = pages.Distinct().Order().ToArray();
        if (sorted.Length == 0) return string.Empty;
        var parts = new List<string>();
        var start = sorted[0];
        var previous = start;
        for (var index = 1; index <= sorted.Length; index++)
        {
            if (index < sorted.Length && sorted[index] == previous + 1)
            {
                previous = sorted[index];
                continue;
            }

            parts.Add(start == previous ? start.ToString() : $"{start}-{previous}");
            if (index < sorted.Length) start = previous = sorted[index];
        }

        return string.Join(", ", parts);
    }

    private static bool TryPage(string value, int pageCount, out int page) =>
        int.TryParse(value, out page) && page >= 1 && page <= pageCount;
}
