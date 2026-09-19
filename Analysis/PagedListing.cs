namespace FireURQ.Porter.Analysis;

public sealed class PagedListing
{
    private readonly IReadOnlyList<string> _lines;
    private readonly int _pageSize;

    public int PageSize => _pageSize;
    public int LineCount => _lines.Count;

    public int PageCount =>
        Math.Max(
            1,
            (int)Math.Ceiling(
                (double)_lines.Count / _pageSize));

    public int CurrentPage { get; private set; }

    public int CurrentLine { get; private set; }

    public PagedListing(
        IReadOnlyList<string> lines,
        int pageSize)
    {
        if (pageSize <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(pageSize));

        _lines = lines;
        _pageSize = pageSize;

        CurrentPage = 0;
        CurrentLine = 0;
    }

    public IReadOnlyList<string> GetCurrentPage()
    {
        int start =
            CurrentPage * _pageSize;

        if (start >= _lines.Count)
            return Array.Empty<string>();

        return _lines
            .Skip(start)
            .Take(_pageSize)
            .ToList();
    }

    public string? GetCurrentLine()
    {
        if (CurrentLine < 0 ||
            CurrentLine >= _lines.Count)
        {
            return null;
        }

        return _lines[CurrentLine];
    }

    public void NextPage()
    {
        if (CurrentPage < PageCount - 1)
        {
            CurrentPage++;

            CurrentLine =
                CurrentPage * _pageSize;
        }
    }

    public void PreviousPage()
    {
        if (CurrentPage > 0)
        {
            CurrentPage--;

            CurrentLine =
                CurrentPage * _pageSize;
        }
    }

    public void FirstPage()
    {
        CurrentPage = 0;
        CurrentLine = 0;
    }

    public void LastPage()
    {
        CurrentPage = PageCount - 1;

        CurrentLine =
            Math.Max(
                0,
                _lines.Count - 1);
    }

    public bool SetCurrentLine(int line)
    {
        if (line < 0 ||
            line >= _lines.Count)
        {
            return false;
        }

        CurrentLine = line;

        CurrentPage =
            line / _pageSize;

        return true;
    }

    public int? Find(
        string query,
        int startLine = 0,
        bool ignoreCase = true)
    {
        if (string.IsNullOrWhiteSpace(query))
            return null;

        if (_lines.Count == 0)
            return null;

        startLine =
            Math.Clamp(
                startLine,
                0,
                _lines.Count - 1);

        StringComparison comparison =
            ignoreCase
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;

        for (int i = startLine;
             i < _lines.Count;
             i++)
        {
            if (_lines[i].Contains(
                    query,
                    comparison))
            {
                SetCurrentLine(i);
                return i;
            }
        }

        return null;
    }

    public int? FindNext(
        string query,
        bool ignoreCase = true)
    {
        if (_lines.Count == 0)
            return null;

        int start =
            CurrentLine + 1;

        if (start >= _lines.Count)
            start = 0;

        int? result =
            Find(
                query,
                start,
                ignoreCase);

        if (result.HasValue)
            return result;

        if (start > 0)
        {
            return Find(
                query,
                0,
                ignoreCase);
        }

        return null;
    }

    public IReadOnlyList<int> FindAll(
        string query,
        bool ignoreCase = true)
    {
        var result =
            new List<int>();

        if (string.IsNullOrWhiteSpace(query))
            return result;

        StringComparison comparison =
            ignoreCase
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;

        for (int i = 0;
             i < _lines.Count;
             i++)
        {
            if (_lines[i].Contains(
                    query,
                    comparison))
            {
                result.Add(i);
            }
        }

        return result;
    }
}