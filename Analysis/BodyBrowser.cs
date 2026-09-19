using System.Text;

namespace FireURQ.Porter.Analysis;

public static class BodyBrowser
{
    public static void Show(
        BodyIndex index)
    {
        while (true)
        {
            var query = ReadSearchQuery();

            if (query == null)
                return;

            IReadOnlyList<BodyIndex.BodyEntry> results =
                index.Search(query);

            if (results.Count == 0)
            {
                Console.Clear();

                Console.WriteLine(
                    "FireURQ.Porter — Body Browser");

                Console.WriteLine();
                Console.WriteLine(
                    $"Search: {query}");

                Console.WriteLine();
                Console.WriteLine(
                    "No bodies found.");

                Console.WriteLine();
                Console.WriteLine(
                    "Press any key...");

                Console.ReadKey(true);
                continue;
            }

            BodyIndex.BodyEntry? selected =
                ShowResults(
                    results,
                    query);

            if (selected == null)
                continue;

            ShowBody(selected);
        }
    }

    private static string? ReadSearchQuery()
    {
        Console.Clear();

        Console.WriteLine(
            "================================");

        Console.WriteLine(
            " FireURQ.Porter — Body Browser");

        Console.WriteLine(
            "================================");

        Console.WriteLine();

        Console.Write(
            "Search body (Esc to exit): ");

        var buffer =
            new StringBuilder();

        while (true)
        {
            ConsoleKeyInfo key =
                Console.ReadKey(true);

            if (key.Key == ConsoleKey.Escape)
                return null;

            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                return buffer.ToString();
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (buffer.Length > 0)
                {
                    buffer.Length--;

                    Console.Write(
                        "\b \b");
                }

                continue;
            }

            if (char.IsControl(key.KeyChar))
                continue;

            buffer.Append(
                key.KeyChar);

            Console.Write(
                key.KeyChar);
        }
    }

    private static BodyIndex.BodyEntry? ShowResults(
        IReadOnlyList<BodyIndex.BodyEntry> results,
        string query)
    {
        var lines =
            new List<string>();

        foreach (
            BodyIndex.BodyEntry body
            in results)
        {
            string kind =
                body.IsProcedure
                    ? "procedure"
                    : "function";

            string file =
                Path.GetFileName(
                    body.FilePath);

            lines.Add(
                $"{kind,-9} {body.Name} " +
                $"[{file}] " +
                $"({body.TokenCount} tokens)");
        }

        var listing =
            new PagedListing(
                lines,
                pageSize: Math.Max(
                    1,
                    Console.WindowHeight - 8));

        while (true)
        {
            Console.Clear();

            Console.WriteLine(
                "================================");

            Console.WriteLine(
                " FireURQ.Porter — Body Search");

            Console.WriteLine(
                "================================");

            Console.WriteLine();

            Console.WriteLine(
                $"Search: {query}");

            Console.WriteLine(
                $"Results: {results.Count}");

            Console.WriteLine();

            IReadOnlyList<string> page =
                listing.GetCurrentPage();

            int pageStart =
                listing.CurrentPage *
                listing.PageSize;

            for (int i = 0;
                 i < page.Count;
                 i++)
            {
                int absoluteLine =
                    pageStart + i;

                string marker =
                    absoluteLine ==
                    listing.CurrentLine
                        ? ">"
                        : " ";

                Console.WriteLine(
                    $"{marker} {page[i]}");
            }

            Console.WriteLine();

            Console.WriteLine(
                $"Page {listing.CurrentPage + 1}" +
                $"/{listing.PageCount}");

            Console.WriteLine(
                "[↑/↓] select  [←/→] page  " +
                "[Enter] open  [Esc] back");

            ConsoleKeyInfo key =
                Console.ReadKey(true);

            switch (key.Key)
            {
                case ConsoleKey.UpArrow:
                    MoveUp(listing);
                    break;

                case ConsoleKey.DownArrow:
                    MoveDown(listing);
                    break;

                case ConsoleKey.LeftArrow:
                    listing.PreviousPage();
                    break;

                case ConsoleKey.RightArrow:
                    listing.NextPage();
                    break;

                case ConsoleKey.Home:
                    listing.FirstPage();
                    break;

                case ConsoleKey.End:
                    listing.LastPage();
                    break;

                case ConsoleKey.Enter:
                {
                    int index =
                        listing.CurrentLine;

                    if (index >= 0 &&
                        index < results.Count)
                    {
                        return results[index];
                    }

                    break;
                }

                case ConsoleKey.Escape:
                    return null;
            }
        }
    }

    private static void MoveUp(
        PagedListing listing)
    {
        int line =
            listing.CurrentLine - 1;

        if (line >= 0)
        {
            listing.SetCurrentLine(line);
        }
        else
        {
            listing.LastPage();
        }
    }

    private static void MoveDown(
        PagedListing listing)
    {
        int line =
            listing.CurrentLine + 1;

        if (line < listing.LineCount)
        {
            listing.SetCurrentLine(line);
        }
        else
        {
            listing.FirstPage();
        }
    }

    private static void ShowBody(
        BodyIndex.BodyEntry body)
    {
        var lines =
            BuildBodyListing(body);

        var listing =
            new PagedListing(
                lines,
                pageSize: Math.Max(
                    1,
                    Console.WindowHeight - 8));

        while (true)
        {
            Console.Clear();

            Console.WriteLine(
                "================================");

            Console.WriteLine(
                " FireURQ.Porter — Body");

            Console.WriteLine(
                "================================");

            Console.WriteLine();

            foreach (
                string line
                in listing.GetCurrentPage())
            {
                Console.WriteLine(line);
            }

            Console.WriteLine();

            Console.WriteLine(
                $"Page {listing.CurrentPage + 1}" +
                $"/{listing.PageCount}");

            Console.WriteLine(
                "[←/→] page  [Home/End]  [Esc] back");

            ConsoleKeyInfo key =
                Console.ReadKey(true);

            switch (key.Key)
            {
                case ConsoleKey.LeftArrow:
                    listing.PreviousPage();
                    break;

                case ConsoleKey.RightArrow:
                    listing.NextPage();
                    break;

                case ConsoleKey.Home:
                    listing.FirstPage();
                    break;

                case ConsoleKey.End:
                    listing.LastPage();
                    break;

                case ConsoleKey.Escape:
                    return;
            }
        }
    }

    private static IReadOnlyList<string> BuildBodyListing(
        BodyIndex.BodyEntry body)
    {
        var lines =
            new List<string>();

        string kind =
            body.IsProcedure
                ? "procedure"
                : "function";

        lines.Add(
            $"{body.Name}");

        lines.Add(
            $"{kind}");

        lines.Add(
            $"File: {body.FilePath}");

        lines.Add(
            $"Tokens: {body.TokenCount}");

        lines.Add(
            $"Owner: {body.OwnerKind}");

        lines.Add("");

        lines.Add(
            "FUNCTIONALITY");

        lines.Add(
            $"  Assignments: {body.Assignments}");

        lines.Add(
            $"  Calls:       {body.Calls}");

        lines.Add(
            $"  Conditions:  {body.Conditions}");

        lines.Add(
            $"  Loops:       {body.Loops}");

        lines.Add(
            $"  Expressions: {body.Expressions}");

        lines.Add("");

        lines.Add(
            "BEHAVIOR");

        AddBehaviorStatements(
            lines,
            body.Behavior.Statements,
            1);

        return lines;
    }

    private static void AddBehaviorStatements(
        List<string> lines,
        IReadOnlyList<BehaviorAnalyzer.BehaviorStatement> statements,
        int indent)
    {
        string prefix =
            new string(
                ' ',
                indent * 2);

        foreach (
            BehaviorAnalyzer.BehaviorStatement statement
            in statements)
        {
            string text =
                string.IsNullOrWhiteSpace(
                    statement.Text)
                    ? statement.Kind.ToString()
                    : statement.Text;

            lines.Add(
                $"{prefix}{statement.Kind}: " +
                $"{text}");

            if (statement.Line > 0)
            {
                lines.Add(
                    $"{prefix}  line {statement.Line}");
            }

            if (statement.Children.Count > 0)
            {
                AddBehaviorStatements(
                    lines,
                    statement.Children,
                    indent + 1);
            }

            if (statement.ElseChildren != null &&
                statement.ElseChildren.Count > 0)
            {
                lines.Add(
                    $"{prefix}else:");

                AddBehaviorStatements(
                    lines,
                    statement.ElseChildren,
                    indent + 1);
            }

            if (statement.Clauses != null)
            {
                foreach (
                    BehaviorAnalyzer.BehaviorClause clause
                    in statement.Clauses)
                {
                    lines.Add(
                        $"{prefix}{clause.Kind}: " +
                        $"{clause.Header}");

                    AddBehaviorStatements(
                        lines,
                        clause.Statements,
                        indent + 1);
                }
            }
        }
    }
}