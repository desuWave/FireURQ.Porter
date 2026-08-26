namespace FireURQ.Porter.Analysis;

public static class DependencyGraphPrinter
{
    public static void Print(
        DependencyGraph graph)
    {
        Console.WriteLine();
        Console.WriteLine("================================");
        Console.WriteLine(" Dependency Graph");
        Console.WriteLine("================================");

        Console.WriteLine();
        Console.WriteLine(
            $"Nodes: {graph.Nodes.Count}");

        Console.WriteLine(
            $"Edges: {graph.Edges.Count}");

        Console.WriteLine();

        Console.WriteLine("NODES");

        foreach (var node in graph.Nodes)
        {
            Console.WriteLine(
                $"  {node.Kind}: {node.Name}");
        }

        Console.WriteLine();

        Console.WriteLine("EDGES");

        foreach (var edge in graph.Edges)
        {
            Console.WriteLine(
                $"  {edge.From} " +
                $"--[{edge.Kind}]--> " +
                $"{edge.To}");
        }

        Console.WriteLine();
    }
}