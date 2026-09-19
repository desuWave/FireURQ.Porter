using System.Text;
using FireURQ.Porter.AST;
using FireURQ.Porter.Lexer;
using FireURQ.Porter.Parser;

namespace FireURQ.Porter.Analysis;

public static class BodyOwnerProbe
{
    public static void Analyze(
        string rootPath,
        string bodyName)
    {
        string[] files =
            Directory.GetFiles(
                rootPath,
                "*.pas",
                SearchOption.AllDirectories);

        foreach (string filePath in files)
        {
            try
            {
                string source =
                    File.ReadAllText(
                        filePath,
                        Encoding.GetEncoding(1251));

                var lexer =
                    new DelphiLexer(source);

                var tokens =
                    lexer.Tokenize();

                var parser =
                    new DelphiParser(tokens);

                DelphiUnit unit =
                    parser.ParseUnit();

                ProbeDeclarations(
                    unit.Declarations,
                    filePath,
                    bodyName);
            }
            catch
            {
            }
        }
    }

    private static void ProbeDeclarations(
        IReadOnlyList<DelphiNode> declarations,
        string filePath,
        string bodyName)
    {
        foreach (DelphiNode declaration in declarations)
        {
            ProbeNode(
                declaration,
                filePath,
                bodyName,
                "TopLevel");
        }
    }

    private static void ProbeNode(
        DelphiNode node,
        string filePath,
        string bodyName,
        string owner)
    {
        switch (node)
        {
            case DelphiFunction function:
            {
                if (function.Name.Contains(
                        bodyName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine(
                        "================================");

                    Console.WriteLine(
                        $"FILE: {filePath}");

                    Console.WriteLine(
                        $"MATCH: {function.Name}");

                    Console.WriteLine(
                        $"OWNER: {owner}");

                    Console.WriteLine(
                        $"PROCEDURE: {function.IsProcedure}");

                    Console.WriteLine(
                        $"TOKENS: {function.BodyTokens.Count}");

                    Console.WriteLine(
                        "================================");
                }

                break;
            }

            case DelphiClass delphiClass:
            {
                foreach (
                    DelphiClassMember member
                    in delphiClass.Members)
                {
                    ProbeNode(
                        member.Node,
                        filePath,
                        bodyName,
                        $"Class:{delphiClass.Name}");
                }

                break;
            }

            case DelphiInterface delphiInterface:
            {
                foreach (
                    DelphiClassMember member
                    in delphiInterface.Members)
                {
                    ProbeNode(
                        member.Node,
                        filePath,
                        bodyName,
                        $"Interface:{delphiInterface.Name}");
                }

                break;
            }
        }
    }
}