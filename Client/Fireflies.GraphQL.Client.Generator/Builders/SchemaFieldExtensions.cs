using Fireflies.GraphQL.Client.Generator.Schema;
using GraphQLParser.AST;

namespace Fireflies.GraphQL.Client.Generator.Builders;

public static class SchemaFieldExtensions {
    public static async Task<FieldMatch> IsSelected(this SchemaField field, SchemaType schemaType, SchemaType? parentType, ASTNode node, bool exactTypeConditionIsRequired, GraphQLGeneratorContext context) {
        if(node is GraphQLField graphQLFieldNode) {
            var selections = graphQLFieldNode.GetSelections();
            if(selections.Any()) {
                foreach(var selection in selections) {
                    var innerIsSelected = await InnerIsSelected(field, schemaType, selection, null, exactTypeConditionIsRequired, true, parentType, context);
                    if(innerIsSelected.IsSelected)
                        return FilterConditionTypeIfSameAsParent(schemaType, innerIsSelected);
                }

                return new FieldMatch(false, null, null, null, null);
            }
        }

        if(!await NodeMatchingTypeCondition(node, schemaType, exactTypeConditionIsRequired, context)) {
            foreach(var selection in node.GetSelections()) {
                var innerIsSelected = await InnerIsSelected(field, schemaType, selection, null, exactTypeConditionIsRequired, false, parentType, context);
                if(innerIsSelected.IsSelected)
                    return FilterConditionTypeIfSameAsParent(schemaType, innerIsSelected);
            }

            return new FieldMatch(false, null, null, null, null);
        }

        var result = await InnerIsSelected(field, schemaType, node, null, exactTypeConditionIsRequired, true, parentType, context);
        return FilterConditionTypeIfSameAsParent(schemaType, result);
    }

    private static FieldMatch FilterConditionTypeIfSameAsParent(SchemaType schemaType, FieldMatch match) {
        var isSameAsSchemaType = match.FoundOnType != null && match.FoundOnType.Name != schemaType.Name;
        return match with { FoundOnType = isSameAsSchemaType ? match.FoundOnType : null };
    }

    private static async Task<bool> NodeMatchingTypeCondition(ASTNode node, SchemaType schemaType, bool exactTypeConditionIsRequired, GraphQLGeneratorContext context) {
        switch(node) {
            case GraphQLFragmentDefinition fragmentNode: return SchemaTypeMatchesTypeCondition(schemaType, fragmentNode.TypeCondition, exactTypeConditionIsRequired, context);
            case GraphQLFragmentSpread fragmentNode: {
                var fragment = await context.FragmentAccessor.GetFragment(fragmentNode.FragmentName);
                return SchemaTypeMatchesTypeCondition(schemaType, fragment.TypeCondition, exactTypeConditionIsRequired, context);
            }
            case GraphQLInlineFragment fragmentNode: return SchemaTypeMatchesTypeCondition(schemaType, fragmentNode.TypeCondition!, exactTypeConditionIsRequired, context);
            default: return true;
        }
    }

    private static async Task<FieldMatch> InnerIsSelected(SchemaField field, SchemaType schemaType, ASTNode node, GraphQLFragmentDefinition? fragmentDefinition, bool exactTypeConditionIsRequired, bool isInsideValidFragment, SchemaType? matchingConditionType, GraphQLGeneratorContext context) {
        VerifySelectionExistOnType(node, schemaType);

        switch(node) {
            case GraphQLOperationDefinition operationDefinitionNode: {
                foreach(var selection in operationDefinitionNode.GetSelections()) {
                    var innerIsSelected = await InnerIsSelected(field, schemaType, selection, fragmentDefinition, exactTypeConditionIsRequired, true, matchingConditionType, context);
                    if(innerIsSelected.IsSelected)
                        return innerIsSelected;
                }

                break;
            }

            case GraphQLFragmentSpread fragmentSpreadNode: {
                var fragment = await context.FragmentAccessor.GetFragment(fragmentSpreadNode.FragmentName);
                if(SchemaTypeMatchesTypeCondition(schemaType, fragment.TypeCondition, exactTypeConditionIsRequired, context)) {
                    var innerIsSelected = await InnerIsSelected(field, schemaType, fragment, fragmentDefinition, exactTypeConditionIsRequired, true, matchingConditionType, context);
                    if(innerIsSelected.IsSelected)
                        return innerIsSelected;
                }

                break;
            }

            case GraphQLFragmentDefinition fragmentDefinitionNode: {
                if(SchemaTypeMatchesTypeCondition(schemaType, fragmentDefinitionNode.TypeCondition, exactTypeConditionIsRequired, context)) {
                    foreach(var selection in fragmentDefinitionNode.GetSelections()) {
                        var innerIsSelected = await InnerIsSelected(field, schemaType, selection, fragmentDefinitionNode, exactTypeConditionIsRequired, true, matchingConditionType, context);
                        if(innerIsSelected.IsSelected)
                            return innerIsSelected;
                    }
                }

                break;
            }

            case GraphQLInlineFragment inlineFragment: {
                if(SchemaTypeMatchesTypeCondition(schemaType, inlineFragment.TypeCondition!, exactTypeConditionIsRequired, context)) {
                    matchingConditionType = context.GetSchemaType(inlineFragment.TypeCondition!.Type);
                    foreach(var selection in inlineFragment.GetSelections()) {
                        var innerIsSelected = await InnerIsSelected(field, schemaType, selection, fragmentDefinition, exactTypeConditionIsRequired, true, matchingConditionType, context);
                        if(innerIsSelected.IsSelected)
                            return innerIsSelected;
                    }
                }

                break;
            }

            case GraphQLField fieldNode: {
                var isMatch = fieldNode.Name == field.Name;
                if(isMatch && isInsideValidFragment) {
                    SchemaType? conditionType = null;
                    if(fragmentDefinition != null && fragmentDefinition.TypeCondition.Type.Name != schemaType.Name)
                        conditionType = schemaType;
                    return new FieldMatch(true, fieldNode, fragmentDefinition, conditionType, matchingConditionType);
                }

                break;
            }
        }

        return new FieldMatch(false, null, null, null, null);
    }

    private static void VerifySelectionExistOnType(ASTNode selection, SchemaType schemaType) {
        if(selection is GraphQLField field) {
            if(field.Name.StringValue != "__typename" && schemaType.Fields.All(x => x.Name != field.Name.StringValue))
                throw new GraphQLGeneratorException($"{field.Name.StringValue} does not exist on {schemaType.Name}");
        }
    }

    private static bool SchemaTypeMatchesTypeCondition(SchemaType schemaType, GraphQLTypeCondition typeCondition, bool exactTypeConditionIsRequired, GraphQLGeneratorContext context) {
        if(schemaType.Name == typeCondition.Type.Name) {
            return true;
        }

        if(exactTypeConditionIsRequired)
            return false;

        if(schemaType.Kind != SchemaTypeKind.INTERFACE) {
            var typeConditionSchemaType = context.GetSchemaType(typeCondition.Type);
            var matchingType = typeConditionSchemaType.PossibleTypes.FirstOrDefault(x => x.Name == schemaType.Name);
            if(matchingType != null) {
                return true;
            }
        }

        return false;
    }
}