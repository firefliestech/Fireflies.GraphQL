using Fireflies.GraphQL.Client.Generator.Schema;
using GraphQLParser.AST;

namespace Fireflies.GraphQL.Client.Generator.Builders;

public class FragmentTypeBuilder {
    private readonly GraphQLFragmentDefinition _fragmentDefinition;
    private readonly GraphQLGeneratorContext _context;

    public FragmentTypeBuilder(GraphQLFragmentDefinition fragmentDefinition, GraphQLGeneratorContext context) {
        _fragmentDefinition = fragmentDefinition;
        _context = context;
    }

    public async Task<string> Build() {
        var fragmentTypeName = _fragmentDefinition.FragmentName.Name.StringValue;

        if(!_context.RootContext.ShouldGenerateType(fragmentTypeName))
            return fragmentTypeName;

        var fragmentSchemaType = _context.GetSchemaType(_fragmentDefinition.TypeCondition.Type);
        var subResultTypeBuilder = new SubResultTypeBuilder(fragmentTypeName, _fragmentDefinition, null, fragmentSchemaType, _context);
        subResultTypeBuilder.OnlyInterface();
        await subResultTypeBuilder.Build();

        foreach(var subSchemaType in fragmentSchemaType.PossibleTypes.Select(x => x.GetOfType(_context))) {
            var subTypeName = $"{fragmentTypeName}_{subSchemaType.Name.Capitalize()}";
            var subFragmentBuilder = new SubResultTypeBuilder(subTypeName, _fragmentDefinition, fragmentSchemaType, subSchemaType, _context);
            subFragmentBuilder.AddInterfaceImplementation($"I{_fragmentDefinition.FragmentName.Name.StringValue}");
            subFragmentBuilder.OnlyInterface();
            subFragmentBuilder.ExactTypeConditionRequired();
            await subFragmentBuilder.Build();
        }

        return fragmentTypeName;
    }
}