namespace RobotRally.Game;

/// <summary>Helpers for editing a program tree (a list of blocks with nested bodies).</summary>
public static class ProgramOps
{
    /// <summary>Total blocks, including nested ones.</summary>
    public static int Count(IEnumerable<Block> program) => program.Sum(b => 1 + Count(b.Body));

    public static IEnumerable<Block> All(IEnumerable<Block> program) =>
        program.SelectMany(b => All(b.Body).Prepend(b));

    /// <summary>Finds the list holding a block and its index there.</summary>
    public static (List<Block> List, int Index)? Find(List<Block> program, Guid id)
    {
        var i = program.FindIndex(b => b.Id == id);
        if (i >= 0) return (program, i);
        foreach (var b in program)
            if (Find(b.Body, id) is { } hit) return hit;
        return null;
    }

    public static Block? Get(List<Block> program, Guid id) =>
        Find(program, id) is { } hit ? hit.List[hit.Index] : null;

    public static Block? Remove(List<Block> program, Guid id)
    {
        if (Find(program, id) is not { } hit) return null;
        var block = hit.List[hit.Index];
        hit.List.RemoveAt(hit.Index);
        return block;
    }

    /// <summary>True if <paramref name="list"/> is <paramref name="ancestor"/>'s body or is nested anywhere inside it.</summary>
    public static bool IsInside(Block ancestor, List<Block> list) =>
        ReferenceEquals(ancestor.Body, list) || ancestor.Body.Any(b => IsInside(b, list));

    /// <summary>Moves a block to (list, index). Refuses to drop a block inside itself.</summary>
    public static bool Move(List<Block> program, Guid id, List<Block> target, int index)
    {
        if (Find(program, id) is not { } hit) return false;
        var block = hit.List[hit.Index];
        if (IsInside(block, target)) return false;
        hit.List.RemoveAt(hit.Index);
        if (ReferenceEquals(hit.List, target) && hit.Index < index) index--;
        target.Insert(Math.Clamp(index, 0, target.Count), block);
        return true;
    }

    public static List<Block> CloneAll(IEnumerable<Block> program) => program.Select(b => b.Clone()).ToList();

    public static IEnumerable<BlockKind> KindsUsed(IEnumerable<Block> program) => All(program).Select(b => b.Kind).Distinct();
}
