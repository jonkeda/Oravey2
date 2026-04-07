namespace Oravey2.Core.UI;

/// <summary>
/// Per-region fog-of-war mask tracking explored, visited, and unknown tiles.
/// Stores a 2D bitmap where each cell is one of three states:
/// <list type="bullet">
///   <item><description><see cref="FogState.Unknown"/> — never visited, fully dark.</description></item>
///   <item><description><see cref="FogState.Visited"/> — previously explored, dimmed (50 % overlay).</description></item>
///   <item><description><see cref="FogState.Visible"/> — currently within line-of-sight, fully visible.</description></item>
/// </list>
/// </summary>
public sealed class FogOfWarMask
{
    private readonly FogState[,] _cells;

    public int Width { get; }
    public int Height { get; }

    public FogOfWarMask(int width, int height)
    {
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));

        Width = width;
        Height = height;
        _cells = new FogState[width, height];
        // All cells start as Unknown
    }

    /// <summary>Gets the fog state of a cell.</summary>
    public FogState this[int x, int y]
    {
        get => IsInBounds(x, y) ? _cells[x, y] : FogState.Unknown;
        private set
        {
            if (IsInBounds(x, y))
                _cells[x, y] = value;
        }
    }

    /// <summary>
    /// Reveals a circular area around (<paramref name="cx"/>, <paramref name="cy"/>)
    /// with the given <paramref name="radius"/>. Cells within the radius become
    /// <see cref="FogState.Visible"/>; previously visible cells outside the radius
    /// are demoted to <see cref="FogState.Visited"/>.
    /// </summary>
    public void Reveal(int cx, int cy, int radius)
    {
        // First demote all currently Visible to Visited
        for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
                if (_cells[x, y] == FogState.Visible)
                    _cells[x, y] = FogState.Visited;

        // Now reveal in the circle
        int r2 = radius * radius;
        int xMin = Math.Max(0, cx - radius);
        int xMax = Math.Min(Width - 1, cx + radius);
        int yMin = Math.Max(0, cy - radius);
        int yMax = Math.Min(Height - 1, cy + radius);

        for (int x = xMin; x <= xMax; x++)
            for (int y = yMin; y <= yMax; y++)
            {
                int dx = x - cx;
                int dy = y - cy;
                if (dx * dx + dy * dy <= r2)
                    _cells[x, y] = FogState.Visible;
            }
    }

    /// <summary>
    /// Returns the count of cells matching the given state.
    /// </summary>
    public int CountCells(FogState state)
    {
        int count = 0;
        for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
                if (_cells[x, y] == state)
                    count++;
        return count;
    }

    /// <summary>
    /// Serializes the fog mask to a byte array (one byte per cell).
    /// </summary>
    public byte[] ToBytes()
    {
        var bytes = new byte[4 + 4 + Width * Height];
        BitConverter.TryWriteBytes(bytes.AsSpan(0, 4), Width);
        BitConverter.TryWriteBytes(bytes.AsSpan(4, 4), Height);
        int offset = 8;
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                bytes[offset++] = (byte)_cells[x, y];
        return bytes;
    }

    /// <summary>
    /// Deserializes a fog mask from a byte array produced by <see cref="ToBytes"/>.
    /// </summary>
    public static FogOfWarMask FromBytes(byte[] bytes)
    {
        if (bytes.Length < 8)
            throw new ArgumentException("Invalid fog mask data", nameof(bytes));

        int w = BitConverter.ToInt32(bytes, 0);
        int h = BitConverter.ToInt32(bytes, 4);
        var mask = new FogOfWarMask(w, h);

        int offset = 8;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                mask._cells[x, y] = (FogState)bytes[offset++];
        return mask;
    }

    /// <summary>
    /// Merges another mask into this one. Cells take the maximum (most-revealed) state.
    /// Both masks must have the same dimensions.
    /// </summary>
    public void Merge(FogOfWarMask other)
    {
        if (other.Width != Width || other.Height != Height)
            throw new ArgumentException("Mask dimensions must match for merge.");

        for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
                if (other._cells[x, y] > _cells[x, y])
                    _cells[x, y] = other._cells[x, y];
    }

    private bool IsInBounds(int x, int y)
        => x >= 0 && x < Width && y >= 0 && y < Height;
}

/// <summary>
/// Fog-of-war visibility state for a single cell.
/// Ordered so that higher values mean more revealed (for merge logic).
/// </summary>
public enum FogState : byte
{
    /// <summary>Never visited — fully dark on minimap.</summary>
    Unknown = 0,
    /// <summary>Previously explored but not currently in view — dimmed (50 % overlay).</summary>
    Visited = 1,
    /// <summary>Currently within line-of-sight — fully visible.</summary>
    Visible = 2,
}
