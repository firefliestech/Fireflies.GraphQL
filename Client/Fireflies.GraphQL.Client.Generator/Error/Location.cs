public readonly struct Location {
    public Location(int line, int column) {
        if(line < 1) {
            throw new ArgumentOutOfRangeException(nameof(line), line, "Line location out of range");
        }

        if(column < 1) {
            throw new ArgumentOutOfRangeException(nameof(column), column, "Column location out of range");
        }

        Line = line;
        Column = column;
    }

    public int Line { get; }

    public int Column { get; }
}