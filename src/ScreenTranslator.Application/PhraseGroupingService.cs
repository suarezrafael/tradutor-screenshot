using ScreenTranslator.Domain;

namespace ScreenTranslator.Application;

/// <summary>
/// Groups raw per-word OCR results into phrase-level <see cref="OcrBlock"/>s, so translation
/// happens once per sentence instead of once per word (see PERFORMANCE requirements: a single
/// "Hello, my name is John." call beats five single-word calls).
/// </summary>
public sealed class PhraseGroupingService
{
    private readonly double _lineOverlapRatio;
    private readonly double _maxWordGapRatio;

    /// <param name="lineOverlapRatio">
    /// Minimum fraction of vertical overlap two words must share to be considered on the same line.
    /// </param>
    /// <param name="maxWordGapRatio">
    /// Maximum horizontal gap between consecutive words on a line (as a multiple of the average
    /// word height) before a new phrase is started. Keeps unrelated columns of text from merging.
    /// </param>
    public PhraseGroupingService(double lineOverlapRatio = 0.5, double maxWordGapRatio = 1.8)
    {
        _lineOverlapRatio = lineOverlapRatio;
        _maxWordGapRatio = maxWordGapRatio;
    }

    public IReadOnlyList<OcrBlock> Group(IReadOnlyList<OcrWord> words)
    {
        var meaningfulWords = words
            .Where(w => OcrTextNormalizer.IsMeaningful(w.Text))
            .Select(w => w with { Text = OcrTextNormalizer.Normalize(w.Text) })
            .ToList();

        if (meaningfulWords.Count == 0)
        {
            return [];
        }

        var lines = GroupIntoLines(meaningfulWords);
        var blocks = new List<OcrBlock>();

        foreach (var line in lines)
        {
            blocks.AddRange(GroupLineIntoPhrases(line));
        }

        return blocks;
    }

    private List<List<OcrWord>> GroupIntoLines(IReadOnlyList<OcrWord> words)
    {
        // Individual word bounding boxes can be noisy (a word's reported height is sometimes wildly
        // larger than its neighbors' on the very same line), which can trick geometry-only overlap
        // checks into merging unrelated lines. When every word carries the OCR engine's own line
        // index, trust that instead - it's based on the line's actual pixel row projection, not
        // per-glyph boxes, and isn't fooled by a handful of mis-measured words.
        if (words.All(w => w.LineId >= 0))
        {
            return words
                .GroupBy(w => w.LineId)
                .OrderBy(g => g.Min(w => w.Bounds.Top))
                .Select(g => g.OrderBy(w => w.Bounds.Left).ToList())
                .ToList();
        }

        var sorted = words.OrderBy(w => w.Bounds.Top).ThenBy(w => w.Bounds.Left).ToList();
        var lines = new List<List<OcrWord>>();

        foreach (var word in sorted)
        {
            // Checked against every existing member (not the line's accumulated union box): a single
            // OCR artifact with an inflated bounding box (e.g. an icon misread as a "word") would
            // otherwise stretch the union tall enough to wrongly absorb an unrelated line below it.
            var line = lines.FirstOrDefault(l => l.All(existing => VerticallyOverlaps(existing.Bounds, word.Bounds)));
            if (line is null)
            {
                lines.Add([word]);
            }
            else
            {
                line.Add(word);
            }
        }

        foreach (var line in lines)
        {
            line.Sort((a, b) => a.Bounds.Left.CompareTo(b.Bounds.Left));
        }

        return lines.OrderBy(l => LineBounds(l).Top).ToList();
    }

    private bool VerticallyOverlaps(BoundingBox lineBounds, BoundingBox wordBounds)
    {
        var overlap = Math.Min(lineBounds.Bottom, wordBounds.Bottom) - Math.Max(lineBounds.Top, wordBounds.Top);
        if (overlap <= 0)
        {
            return false;
        }

        var shorterHeight = Math.Min(lineBounds.Height, wordBounds.Height);
        return shorterHeight > 0 && overlap / shorterHeight >= _lineOverlapRatio;
    }

    private static BoundingBox LineBounds(IReadOnlyList<OcrWord> line) =>
        line.Aggregate(line[0].Bounds, (acc, w) => acc.Union(w.Bounds));

    private IEnumerable<OcrBlock> GroupLineIntoPhrases(List<OcrWord> line)
    {
        var averageHeight = line.Average(w => w.Bounds.Height);
        var maxGap = averageHeight * _maxWordGapRatio;

        var currentPhrase = new List<OcrWord> { line[0] };

        for (var i = 1; i < line.Count; i++)
        {
            var previous = currentPhrase[^1];
            var current = line[i];
            var gap = current.Bounds.Left - previous.Bounds.Right;

            if (gap > maxGap)
            {
                yield return BuildBlock(currentPhrase);
                currentPhrase = [current];
            }
            else
            {
                currentPhrase.Add(current);
            }
        }

        yield return BuildBlock(currentPhrase);
    }

    private static OcrBlock BuildBlock(List<OcrWord> words)
    {
        var text = string.Join(' ', words.Select(w => w.Text));
        var bounds = words.Aggregate(words[0].Bounds, (acc, w) => acc.Union(w.Bounds));
        var confidence = words.Average(w => w.Confidence);
        return new OcrBlock(text, bounds, confidence, words);
    }
}
