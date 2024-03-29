﻿using System.Text.Json;

namespace Fireflies.GraphQL.Core.Scalar;

public class IntScalarHandler : IScalarHandler {
    public Type BaseType => typeof(int);

    public void Serialize(Utf8JsonWriter writer, object value) {
        writer.WriteNumberValue((long)Convert.ChangeType(value, TypeCode.Int64));
    }

    public void Serialize(Utf8JsonWriter writer, string property, object value) {
        writer.WriteNumber(property, (long)Convert.ChangeType(value, TypeCode.Int64));
    }

    public object? Deserialize(object value, Type type) {
        if(value.GetType().IsAssignableTo(type))
            return value;

        return Convert.ChangeType(value, type);
    }
}