namespace FireURQ.Porter.Analysis;

public static class PagedListingViewer
{
    public static void Show(
        PagedListing listing,
        string title = "FireURQ.Porter")
    {
        if (listing is null)
            throw new ArgumentNullException(nameof(listing));

        Console.Clear();

        while (true)
        {
            Render(listing, title);

            var key =
                Console.ReadKey(intercept: true);

            switch (key.Key)
            {
                case ConsoleKey.PageDown:
                case ConsoleKey.RightArrow:
                case ConsoleKey.DownArrow:
                    listing.NextPage();
                    break;

                case ConsoleKey.PageUp:
                case ConsoleKey.LeftArrow:
                case ConsoleKey.UpArrow:
                    listing.PreviousPage();
                    break;

                case ConsoleKey.Home:
                    listing.FirstPage();
                    break;

                case ConsoleKey.End:
                    listing.LastPage();
                    break;

                case ConsoleKey.Escape:
                    Console.Clear();
                    return;
            }
        }
    }

    private static void Render(
        PagedListing listing,
        string title)
    {
        Console.Clear();

        int width =
            Math.Max(
                40,
                Console.WindowWidth);

        Console.WriteLine(title);
        Console.WriteLine(
            new string(
                '─',
                width));

        foreach (var line in listing.GetCurrentPage())
        {
            Console.WriteLine(line);
        }

        Console.WriteLine(
            new string(
                '─',
                width));

        Console.WriteLine(
            $"‹ Prev    Page " +
            $"{listing.CurrentPage + 1} / {listing.PageCount}" +
            $"    Next ›");

        Console.WriteLine(
            "[PgUp/PgDn] [←/→]  " +
            "[Home/End]  [Esc] Exit");
    }
}
