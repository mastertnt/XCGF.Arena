using XCGF.Arena.Blueprints;

namespace XCGF.Arena.Map.Blueprints;

/// <summary>
/// Places the nodes of a graph that has no editor layout: each event starts a band, its exec chain runs from left to
/// right, a second exec output (failure, false) opens a row below, and the nodes reached by no exec link go to the bottom.
/// </summary>
public static class BlueprintLayout
{
    /// <summary>
    /// Horizontal distance between two columns.
    /// </summary>
    public const double ColumnWidth = 250;

    /// <summary>
    /// Vertical margin between two rows.
    /// </summary>
    public const double RowMargin = 50;

    /// <summary>
    /// Gives the position of each node: the editor layout of the graph when it has one, else an automatic one.
    /// </summary>
    /// <param name="pGraph">The graph.</param>
    /// <param name="pHeight">The height of a node, by id.</param>
    /// <returns>The position of each node, by id.</returns>
    public static Dictionary<int, (double X, double Y)> Arrange(BlueprintGraph pGraph, Func<int, double> pHeight)
    {
        ArgumentNullException.ThrowIfNull(pGraph);
        ArgumentNullException.ThrowIfNull(pHeight);
        if (pGraph.Layout.Count > 0)
        {
            return pGraph.Nodes.ToDictionary(pNode => pNode.Id, pNode => pGraph.Layout.GetValueOrDefault(pNode.Id));
        }

        Dictionary<int, (int Column, int Row)> lCells = new Dictionary<int, (int Column, int Row)>();
        Dictionary<int, List<int>> lOutputs = pGraph.ExecLinks
            .GroupBy(pLink => pLink.From)
            .ToDictionary(pGroup => pGroup.Key, pGroup => pGroup.OrderBy(pLink => pLink.FromPin).Select(pLink => pLink.To).ToList());
        int lNextRow = 0;

        void Place(int pNode, int pColumn, int pRow)
        {
            if (!lCells.TryAdd(pNode, (pColumn, pRow)))
            {
                return;
            }

            List<int> lTargets = lOutputs.GetValueOrDefault(pNode) ?? new List<int>();
            for (int lIndex = 0; lIndex < lTargets.Count; lIndex++)
            {
                int lRow = pRow;
                if (lIndex > 0 && !lCells.ContainsKey(lTargets[lIndex]))
                {
                    lRow = lNextRow++;
                }

                Place(lTargets[lIndex], pColumn + 1, lRow);
            }
        }

        foreach (NodeInstance lNode in pGraph.Nodes.Where(pNode => pNode.Definition is IEventNode))
        {
            Place(lNode.Id, 0, lNextRow++);
        }

        // Pure nodes and unreachable actions: a last row, left of their first reader when they have one.
        int lBottom = lNextRow;
        HashSet<int> lTaken = new HashSet<int>();
        foreach (NodeInstance lNode in pGraph.Nodes.Where(pNode => !lCells.ContainsKey(pNode.Id)))
        {
            int lColumn = pGraph.DataLinks
                .Where(pLink => pLink.From == lNode.Id && lCells.ContainsKey(pLink.To))
                .Select(pLink => lCells[pLink.To].Column - 1)
                .DefaultIfEmpty(0)
                .Min();
            lColumn = Math.Max(lColumn, 0);
            while (!lTaken.Add(lColumn))
            {
                lColumn++;
            }

            lCells[lNode.Id] = (lColumn, lBottom);
        }

        // Each row is as high as its highest node.
        Dictionary<int, double> lRowHeight = lCells
            .GroupBy(pCell => pCell.Value.Row)
            .ToDictionary(pGroup => pGroup.Key, pGroup => pGroup.Max(pCell => pHeight(pCell.Key)));
        Dictionary<int, double> lRowTop = new Dictionary<int, double>();
        double lTop = 0;
        foreach (int lRow in lRowHeight.Keys.Order())
        {
            lRowTop[lRow] = lTop;
            lTop += lRowHeight[lRow] + RowMargin;
        }

        return lCells.ToDictionary(pCell => pCell.Key, pCell => (pCell.Value.Column * ColumnWidth, lRowTop[pCell.Value.Row]));
    }
}
