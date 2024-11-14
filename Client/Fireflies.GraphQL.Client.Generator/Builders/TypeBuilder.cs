using System.Text;
using Fireflies.GraphQL.Client.Generator.Schema;
using GraphQLParser.AST;

namespace Fireflies.GraphQL.Client.Generator.Builders;

public class TypeBuilder : ITypeBuilder {
    private readonly string _typeName;
    private readonly ASTNode _node;
    private readonly GraphQLGeneratorContext _context;
    private readonly StringBuilder _stringBuilder = new();
    private readonly List<PropertyDescriptor> _properties = new();
    private readonly Dictionary<PolymorphicPropertyKey, List<PolymorphicProperty>> _polymorphicProperties = new();
    private readonly HashSet<string> _implementedInterfaces = new();

    private bool _isOperation;
    private bool _onlyInterface;

    public readonly record struct PropertyDescriptor(string PropertyName, string TypeName, SchemaField SchemaField, bool IncludeInInterface);
    public readonly record struct PolymorphicPropertyKey(string PropertyName, string TypeName, SchemaField SchemaField, bool IncludeInInterface);
    public readonly record struct PolymorphicProperty(string ClassName, string InterfaceName, SchemaType SchemaType);

    public TypeBuilder(string typeName, ASTNode node, GraphQLGeneratorContext context) {
        _typeName = typeName;
        _node = node;
        _context = context;
    }

    public string Source() {
        return _stringBuilder.ToString();
    }

    public Task Build() {
        var interfaceName = GenerateInterface();

        if(!_onlyInterface) {
            GenerateClass(interfaceName);
        }

        return Task.CompletedTask;
    }

    public void OnlyInterface() {
        _onlyInterface = true;
    }

    public void AddInterfaceImplementation(string implementsInterface) {
        _implementedInterfaces.Add(implementsInterface);
    }

    public void AddProperty(string typeName, string propertyName, SchemaField schemaField, FieldMatch fieldMatch) {
        var includeInInterface = IncludeInInterface(fieldMatch);
        _properties.Add(new PropertyDescriptor(propertyName, typeName, schemaField, includeInInterface));
    }

    public void AddOperationProperties() {
        _isOperation = true;
    }

    public void AddPolymorphicProperty(string typeName, string propertyName, string className, string interfaceName, SchemaField schemaField, SchemaType schemaType, FieldMatch fieldMatch) {
        var includeInInterface = IncludeInInterface(fieldMatch);

        var polymorphicPropertyKey = new PolymorphicPropertyKey(propertyName, typeName, schemaField, includeInInterface);
        if(!_polymorphicProperties.TryGetValue(polymorphicPropertyKey, out var existing)) {
            existing = new List<PolymorphicProperty>();
            _polymorphicProperties[polymorphicPropertyKey] = existing;
        }

        existing.Add(new PolymorphicProperty(className, interfaceName, schemaType));
    }

    private bool IncludeInInterface(FieldMatch fieldMatch) {
        bool includeInInterface;
        if(fieldMatch.FoundOnType != null) {
            includeInInterface = false;
        } else {
            includeInInterface = fieldMatch.DefinedByFragment == null || fieldMatch.DefinedByFragment == _node;
        }

        if(fieldMatch.DefinedByFragment != null && fieldMatch.DefinedByFragment != _node) {
            var fragmentInterfaceName = $"I{fieldMatch.DefinedByFragment.FragmentName.Name}{(fieldMatch.ConditionType != null ? $"_{fieldMatch.ConditionType}" : null)}";
            _implementedInterfaces.Add(fragmentInterfaceName);
        }

        return includeInInterface;
    }

    private string GenerateInterface() {
        var interfaceName = $"I{_typeName}";
        _stringBuilder.Append($"public interface {interfaceName}");
        if(_implementedInterfaces.Any()) {
            var first = true;
            foreach(var implementedInterface in _implementedInterfaces) {
                _stringBuilder.Append((first ? " : " : ", ") + implementedInterface);
                first = false;
            }
        }

        _stringBuilder.AppendLine(" {");

        foreach(var property in _properties.Where(x => x.IncludeInInterface)) {
            GenerateProperty(property, true);
        }

        foreach(var property in _polymorphicProperties.Keys.Where(x => x.IncludeInInterface))
            GeneratePolymorphicProperty(property, true);

        _stringBuilder.AppendLine("}");
        return interfaceName;
    }

    private void GenerateClass(string interfaceName) {
        _stringBuilder.AppendLine();

        _stringBuilder.AppendLine($"public class {_typeName} : {interfaceName} {{");

        if(_isOperation) {
            _stringBuilder.AppendLine($"\tpublic IEnumerable<IClientError> Errors {{ get; }}");
        }

        foreach(var property in _properties)
            GenerateProperty(property, false);

        foreach(var property in _polymorphicProperties.Keys)
            GeneratePolymorphicProperty(property, false);

        _stringBuilder.AppendLine();

        var dataMayBeNull = false;
        if(_isOperation) {
            dataMayBeNull = true;
            _stringBuilder.AppendLine($"\tpublic {_typeName}(JsonNode? data, JsonSerializerOptions serializerOptions) {{");
            _stringBuilder.AppendLine("\t\tErrors = data?[\"errors\"]?.Deserialize<IEnumerable<ClientError>>(serializerOptions)?.ToArray() ?? new IClientError[0];");
        } else {
            _stringBuilder.AppendLine($"\tpublic {_typeName}(JsonNode data, JsonSerializerOptions serializerOptions) {{");
        }

        GeneratePropertySetters(dataMayBeNull);

        _stringBuilder.AppendLine("\t}");

        foreach(var property in _properties)
            GeneratePropertyFactory(property);

        foreach(var property in _polymorphicProperties)
            GeneratePolymorphicPropertyFactory(property.Key, property.Value);

        _stringBuilder.AppendLine("}");
    }

    private void GeneratePropertySetters(bool dataMayBeNull) {
        foreach(var property in _properties)
            GeneratePropertySetter(property.SchemaField.Name, property.PropertyName, property.SchemaField.IsEnumerable(), dataMayBeNull);

        foreach(var property in _polymorphicProperties.Keys)
            GeneratePropertySetter(property.SchemaField.Name, property.PropertyName, property.SchemaField.IsEnumerable(), dataMayBeNull);
    }

    private void GeneratePropertySetter(string fieldName, string propertyName, bool isEnumerable, bool dataMayBeNull) {
        var nullableData = $"data{(dataMayBeNull ? "?" : null)}[\"{fieldName}\"]";
        var data = $"data[\"{fieldName}\"]";
        if(isEnumerable) {
            _stringBuilder.AppendLine($"\t\t{propertyName} = ({nullableData} != null ? {data}!.AsArray().Select(x => Create{propertyName}(x, serializerOptions)).ToArray() : null)!;");
        } else {
            _stringBuilder.AppendLine($"\t\t{propertyName} = Create{propertyName}({nullableData}, serializerOptions);");
        }
    }

    private void GeneratePolymorphicPropertyFactory(PolymorphicPropertyKey property, List<PolymorphicProperty> implementations) {
        _stringBuilder.AppendLine();

        var typeName = GetActualType(property.SchemaField, property.TypeName);
        _stringBuilder.AppendLine($"\tprivate {typeName} Create{property.PropertyName}(JsonNode? data, JsonSerializerOptions serializerOptions) {{");

        _stringBuilder.AppendLine("\t\tif(data == null)");
        _stringBuilder.AppendLine("\t\t\treturn null!;");

        _stringBuilder.AppendLine();

        _stringBuilder.AppendLine("\t\tvar typeName = data[\"__typename\"]!.GetValue<string>()!;");
        _stringBuilder.AppendLine("\t\treturn typeName switch {");

        foreach(var possibleType in implementations) {
            _stringBuilder.AppendLine($"\t\t\t\"{possibleType.SchemaType.Name}\" => ({possibleType.InterfaceName})new {possibleType.ClassName}(data, serializerOptions),");
        }

        _stringBuilder.AppendLine("\t\t\t_ => throw new ArgumentException($\"Cant find implementation for {typeName}\")");
        _stringBuilder.AppendLine("\t\t};");

        _stringBuilder.AppendLine("\t}");
    }

    private void GeneratePropertyFactory(PropertyDescriptor property) {
        var schemaType = property.SchemaField.GetOfType(_context);
        var isScalarOrEnum = schemaType.Kind is SchemaTypeKind.SCALAR or SchemaTypeKind.ENUM;

        _stringBuilder.AppendLine();
        var typeName = GetActualType(property.SchemaField, property.TypeName);
        _stringBuilder.AppendLine($"\tprivate {(isScalarOrEnum ? null : "I")}{typeName} Create{property.PropertyName}(JsonNode? data, JsonSerializerOptions serializerOptions) {{");
        if(isScalarOrEnum) {
            if(property.SchemaField.IsNullable()) {
                _stringBuilder.AppendLine($"\t\treturn data?.Deserialize<{property.TypeName}>(serializerOptions) ?? null;");
            } else {
                _stringBuilder.AppendLine($"\t\treturn data?.Deserialize<{property.TypeName}>(serializerOptions) ?? default!;");
            }
        } else {
            _stringBuilder.AppendLine("\t\tif(data == null)");
            _stringBuilder.AppendLine("\t\t\treturn null!;");
            _stringBuilder.AppendLine($"\t\treturn new {property.TypeName}(data!, serializerOptions);");
        }

        _stringBuilder.AppendLine("\t}");
    }

    private void GeneratePolymorphicProperty(PolymorphicPropertyKey property, bool isInterface) {
        GenerateActualProperty(isInterface, property.SchemaField, property.TypeName, property.PropertyName);
    }

    private void GenerateProperty(PropertyDescriptor property, bool isInterface) {
        var schemaField = property.SchemaField;
        var returnTypeIsInterface = schemaField.GetOfType(_context).Kind is not SchemaTypeKind.SCALAR and not SchemaTypeKind.ENUM;
        var typeName = $"{(returnTypeIsInterface ? "I" : null)}{property.TypeName}";

        GenerateActualProperty(isInterface, schemaField, typeName, property.PropertyName);
    }

    private void GenerateActualProperty(bool isInterface, SchemaField schemaField, string typeName, string propertyName) {
        var actualTypeName = GetActualType(schemaField, typeName);
        if(schemaField.IsEnumerable()) {
            _stringBuilder.AppendLine($"\t{(isInterface ? null : "public ")}{actualTypeName}[] {propertyName} {{ get; }}");
        } else {
            _stringBuilder.AppendLine($"\t{(isInterface ? null : "public ")}{actualTypeName} {propertyName} {{ get; }}");
        }
    }

    private static string GetActualType(SchemaField schemaField, string typeName) {
        return $"{typeName}{(schemaField.IsNullable() ? "?" : null)}";
    }
}