using Fireflies.GraphQL.Client.Generator.Schema;
using GraphQLParser.AST;

namespace Fireflies.GraphQL.Client.Generator.Builders;

public record struct FieldMatch(bool IsSelected, ASTNode? SelectedByNode, GraphQLFragmentDefinition? DefinedByFragment, SchemaType? ConditionType, SchemaType? FoundOnType);