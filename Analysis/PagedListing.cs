namespace FireURQ.Porter.Analysis;

public sealed class PagedListing
{
    private readonly IReadOnlyList<string> _lines;
    private readonly int _pageSize;

    public int PageSize =>
        _pageSize;

    public int LineCount =>
        _lines.Count;

    public int PageCount =>
        Math.Max(
            1,
            (int)Math.Ceiling(
                (double)_lines.Count / _pageSize));

    public int CurrentPage { get; private set; }

    public PagedListing(
        IReadOnlyList<string> lines,
        int pageSize)
    {
        if (pageSize <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pageSize));
        }

        _lines = lines;
        _pageSize = pageSize;
        CurrentPage = 0;
    }

    public IReadOnlyList<string> GetCurrentPage()
    {
        int start =
            CurrentPage * _pageSize;

        if (start >= _lines.Count)
        {
            return Array.Empty<string>();
        }

        return _lines
            .Skip(start)
            .Take(_pageSize)
            .ToList();
    }

    public void NextPage()
    {
        if (CurrentPage < PageCount - 1)
        {
            CurrentPage++;
        }
    }

    public void PreviousPage()
    {
        if (CurrentPage > 0)
        {
            CurrentPage--;
        }
    }

    public void FirstPage()
    {
        CurrentPage = 0;
    }

    public void LastPage()
    {
        CurrentPage = PageCount - 1;
    }
}
