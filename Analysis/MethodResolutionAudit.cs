using System.Text;
using FireURQ.Porter.AST;
using FireURQ.Porter.Lexer;
using FireURQ.Porter.Parser;

namespace FireURQ.Porter.Analysis;

public static class MethodResolutionAudit
{
    public static void Analyze(string rootPath)
    {
        Console.WriteLine();
        Console.WriteLine("================================");
        Console.WriteLine(" Method Resolution Audit");
        Console.WriteLine("================================");
        Console.WriteLine();

        if (!Directory.Exists(rootPath))
        {
            Console.WriteLine("ERROR: Directory not found:");
            Console.WriteLine(rootPath);
            Console.WriteLine();
            return;
        }

        Encoding.RegisterProvider(
            CodePagesEncodingProvider.Instance);

        var files =
            Directory
                .GetFiles(
                    rootPath,
                    "*.pas",
                    SearchOption.AllDirectories)
                .OrderBy(path => path)
                .ToList();

        var declarations =
            new List<MethodDeclaration>();

        var implementations =
            new List<MethodImplementation>();

        var resolutions =
            new List<MethodResolutionInfo>();

        int parsed = 0;
        int failed = 0;

        foreach (var file in files)
        {
            try
            {
                string source =
                    File.ReadAllText(
                        file,
                        Encoding.GetEncoding(1251));

                var lexer =
                    new DelphiLexer(source);

                var tokens =
                    lexer.Tokenize();

                var parser =
                    new DelphiParser(tokens);

                DelphiUnit unit =
                    parser.ParseUnit();

                Collect(
                    unit,
                    Path.GetRelativePath(
                        rootPath,
                        file),
                    declarations,
                    implementations,
                    resolutions);

                parsed++;
            }
            catch (Exception ex)
            {
                failed++;

                Console.WriteLine(
                    $"FAIL: {Path.GetRelativePath(rootPath, file)}");

                Console.WriteLine(
                    $"      {ex.Message}");
            }
        }

        // ------------------------------------------------------------
        // Signature-aware grouping
        // ------------------------------------------------------------

        var declarationGroups =
            declarations
                .GroupBy(
                    x => x.Signature,
                    StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    x => x.Key,
                    x => x.ToList(),
                    StringComparer.OrdinalIgnoreCase);

        var implementationGroups =
            implementations
                .GroupBy(
                    x => x.Signature,
                    StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    x => x.Key,
                    x => x.ToList(),
                    StringComparer.OrdinalIgnoreCase);

        var declarationSignatures =
            declarationGroups.Keys.ToHashSet(
                StringComparer.OrdinalIgnoreCase);

        var implementationSignatures =
            implementationGroups.Keys.ToHashSet(
                StringComparer.OrdinalIgnoreCase);

        var matched =
            declarationSignatures
                .Intersect(
                    implementationSignatures,
                    StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .ToList();

        var unmatchedDeclarations =
            declarationSignatures
                .Except(
                    implementationSignatures,
                    StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .ToList();

        var orphanImplementations =
            implementationSignatures
                .Except(
                    declarationSignatures,
                    StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .ToList();

        // A real overload group contains multiple DISTINCT signatures
        // belonging to the same method name.
        var overloaded =
            declarations
                .GroupBy(
                    x => x.FullName,
                    StringComparer.OrdinalIgnoreCase)
                .Where(
                    x =>
                        x.Select(
                            item => item.Signature)
                         .Distinct(
                             StringComparer.OrdinalIgnoreCase)
                         .Count() > 1)
                .OrderBy(x => x.Key)
                .ToList();

        // Duplicate implementation means the SAME signature occurs
        // more than once.
        var duplicateImplementations =
            implementationGroups
                .Where(x => x.Value.Count > 1)
                .OrderBy(x => x.Key)
                .ToList();

        Console.WriteLine("--------------------------------");
        Console.WriteLine("Summary");
        Console.WriteLine("--------------------------------");

        Console.WriteLine(
            $"Files:                    {files.Count}");

        Console.WriteLine(
            $"Parsed:                   {parsed}");

        Console.WriteLine(
            $"Failed:                   {failed}");

        Console.WriteLine();

        Console.WriteLine(
            $"Class method declarations:{declarations.Count}");

        Console.WriteLine(
            $"Implementations:          {implementations.Count}");

        Console.WriteLine(
            $"Matched:                  {matched.Count}");

        Console.WriteLine(
            $"Unmatched declarations:   {unmatchedDeclarations.Count}");

        Console.WriteLine(
            $"Orphan implementations:   {orphanImplementations.Count}");

        Console.WriteLine(
            $"Potential overloads:      {overloaded.Count}");

        Console.WriteLine(
            $"Duplicate implementations:{duplicateImplementations.Count}");

        Console.WriteLine(
            $"Method resolutions:       {resolutions.Count}");

        if (unmatchedDeclarations.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("--------------------------------");
            Console.WriteLine("Unmatched declarations");
            Console.WriteLine("--------------------------------");

            foreach (var signature in unmatchedDeclarations)
            {
                Console.WriteLine(
                    $"  {signature}");

                foreach (var item in declarationGroups[signature])
                {
                    Console.WriteLine(
                        $"      {item.FilePath}");
                }
            }
        }

        if (orphanImplementations.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("--------------------------------");
            Console.WriteLine("Orphan implementations");
            Console.WriteLine("--------------------------------");

            foreach (var signature in orphanImplementations)
            {
                Console.WriteLine(
                    $"  {signature}");

                foreach (var item in implementationGroups[signature])
                {
                    Console.WriteLine(
                        $"      {item.filePath}");
                }
            }
        }

        if (overloaded.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("--------------------------------");
            Console.WriteLine("Overloaded declarations");
            Console.WriteLine("--------------------------------");

            foreach (var group in overloaded)
            {
                Console.WriteLine(
                    $"  {group.Key}");

                foreach (var signature in
                         group
                             .Select(x => x.Signature)
                             .Distinct(
                                 StringComparer.OrdinalIgnoreCase)
                             .OrderBy(x => x))
                {
                    Console.WriteLine(
                        $"      {signature}");
                }
            }
        }

        if (duplicateImplementations.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("--------------------------------");
            Console.WriteLine("Duplicate implementations");
            Console.WriteLine("--------------------------------");

            foreach (var group in duplicateImplementations)
            {
                Console.WriteLine(
                    $"  {group.Key} x{group.Value.Count}");

                foreach (var item in group.Value)
                {
                    Console.WriteLine(
                        $"      {item.filePath}");
                }
            }
        }

        if (resolutions.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("--------------------------------");
            Console.WriteLine("Method resolutions");
            Console.WriteLine("--------------------------------");

            foreach (var resolution in resolutions)
            {
                Console.WriteLine(
                    $"  {resolution.InterfaceMethod} = " +
                    $"{resolution.ImplementationMethod}");
            }
        }

        Console.WriteLine();
        Console.WriteLine("================================");
        Console.WriteLine(" Method Resolution Audit Complete");
        Console.WriteLine("================================");
        Console.WriteLine();
    }

    private static void Collect(
        DelphiUnit unit,
        string filePath,
        List<MethodDeclaration> declarations,
        List<MethodImplementation> implementations,
        List<MethodResolutionInfo> resolutions)
    {
        foreach (var node in unit.Declarations)
        {
            switch (node)
            {
                case DelphiClass cls:
                    CollectClass(
                        cls,
                        filePath,
                        declarations,
                        resolutions);
                    break;

                case DelphiInterface iface:
                    CollectInterface(
                        iface,
                        filePath,
                        resolutions);
                    break;

                case DelphiFunction function:
                    if (IsQualified(function.Name))
                    {
                        implementations.Add(
                            new MethodImplementation(
                                GetMethodName(function.Name),
                                filePath,
                                function));
                    }

                    break;

                case DelphiMethodResolution resolution:
                    resolutions.Add(
                        new MethodResolutionInfo(
                            resolution.InterfaceMethod,
                            resolution.ImplementationMethod,
                            filePath));

                    break;
            }
        }
    }

    private static void CollectClass(
        DelphiClass cls,
        string filePath,
        List<MethodDeclaration> declarations,
        List<MethodResolutionInfo> resolutions)
    {
        foreach (var member in cls.Members)
        {
            if (member.Kind == DelphiClassMemberKind.Method &&
                member.Node is DelphiFunction function)
            {
                string methodName =
                    GetMethodName(function.Name);

                string fullName =
                    $"{cls.Name}.{methodName}";

                declarations.Add(
                    new MethodDeclaration(
                        fullName,
                        filePath,
                        function));
            }

            if (member.Kind ==
                DelphiClassMemberKind.MethodResolution &&
                member.Node is DelphiMethodResolution resolution)
            {
                resolutions.Add(
                    new MethodResolutionInfo(
                        resolution.InterfaceMethod,
                        resolution.ImplementationMethod,
                        filePath));
            }
        }
    }

    private static void CollectInterface(
        DelphiInterface iface,
        string filePath,
        List<MethodResolutionInfo> resolutions)
    {
        foreach (var member in iface.Members)
        {
            if (member.Kind ==
                DelphiClassMemberKind.MethodResolution &&
                member.Node is DelphiMethodResolution resolution)
            {
                resolutions.Add(
                    new MethodResolutionInfo(
                        resolution.InterfaceMethod,
                        resolution.ImplementationMethod,
                        filePath));
            }
        }
    }

    private static bool IsQualified(
        string name)
    {
        return name.Contains('.');
    }

    private static string GetMethodName(
        string name)
    {
        int index =
            name.LastIndexOf('.');

        return index >= 0
            ? name[(index + 1)..]
            : name;
    }

    private static string BuildSignature(
        string fullName,
        DelphiFunction function)
    {
        string parameters =
            string.Join(
                ",",
                function.Parameters.Select(
                    parameter =>
                        $"{parameter.Kind}:{parameter.Type}"));

        return $"{fullName}({parameters})";
    }

    private sealed record MethodDeclaration(
        string FullName,
        string FilePath,
        DelphiFunction function)
    {
        public string Signature =>
            BuildSignature(
                FullName,
                function);
    }

    private sealed record MethodImplementation(
        string functionName,
        string filePath,
        DelphiFunction function)
    {
        public string Signature =>
            BuildSignature(
                function.Name,
                function);
    }

    private sealed record MethodResolutionInfo(
        string InterfaceMethod,
        string ImplementationMethod,
        string filePath);
}