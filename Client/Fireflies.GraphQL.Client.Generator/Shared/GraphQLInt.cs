public struct GraphQLInt {
    private readonly long _value;

    public GraphQLInt(long value) {
        _value = value;
    }

    public static implicit operator GraphQLInt(long value) {
        return new GraphQLInt(value);
    }

    public static implicit operator long(GraphQLInt custom) {
        return custom._value;
    }

    public static implicit operator GraphQLInt(ulong value) {
        return new GraphQLInt((long)value);
    }

    public static implicit operator ulong(GraphQLInt custom) {
        return (ulong)custom._value;
    }

    public static implicit operator GraphQLInt(int value) {
        return new GraphQLInt(value);
    }

    public static implicit operator int(GraphQLInt custom) {
        return (int)custom._value;
    }

    public static implicit operator GraphQLInt(uint value) {
        return new GraphQLInt(value);
    }

    public static implicit operator uint(GraphQLInt custom) {
        return (uint)custom._value;
    }

    public static implicit operator GraphQLInt(short value) {
        return new GraphQLInt(value);
    }

    public static implicit operator short(GraphQLInt custom) {
        return (short)custom._value;
    }

    public static implicit operator GraphQLInt(ushort value) {
        return new GraphQLInt(value);
    }

    public static implicit operator ushort(GraphQLInt custom) {
        return (ushort)custom._value;
    }

    public static implicit operator GraphQLInt(byte value) {
        return new GraphQLInt(value);
    }

    public static implicit operator byte(GraphQLInt custom) {
        return (byte)custom._value;
    }

    public static implicit operator GraphQLInt(sbyte value) {
        return new GraphQLInt(value);
    }

    public static implicit operator sbyte(GraphQLInt custom) {
        return (sbyte)custom._value;
    }

    public override int GetHashCode() {
        return _value.GetHashCode();
    }

    public override string ToString() {
        return _value.ToString();
    }
}
