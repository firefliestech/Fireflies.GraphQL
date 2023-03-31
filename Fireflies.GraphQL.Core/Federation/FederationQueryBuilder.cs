using System.Text.Json;
using System.Text.Json.Nodes;
using GraphQLParser.AST;

namespace Fireflies.GraphQL.Core.Federation;

public static class FederationQueryBuilder {
    public static string BuildQuery(string query, OperationType operationType, string name, Dictionary<string, object?>? variables) =>
        JsonSerializer.Serialize(new JsonObject {
            { "query", JsonValue.Create($"{operationType.ToString().ToLower()} {name} {{ {query} }}") },
            { "variables", variables != null ? JsonValue.Create(variables) : null }
        });

    public static string SchemaQuery =>
        @"__schema {
    queryType {
      name
    }
    mutationType {
      name
    }
    subscriptionType {
      name
    }
    types {
      ...FullType
    }
    directives {
      name
      description
      locations
      args {
        ...InputValue
      }
    }
  }
}

fragment FullType on __Type {
  kind
  name
  description
  fields(includeDeprecated: true) {
    name
    description
    args {
      ...InputValue
    }
    type {
      ...TypeRef
    }
    isDeprecated
    deprecationReason
  }
  inputFields {
    ...InputValue
  }
  interfaces {
    ...TypeRef
  }
  enumValues(includeDeprecated: true) {
    name
    description
    isDeprecated
    deprecationReason
  }
  possibleTypes {
    ...TypeRef
  }
}

fragment InputValue on __InputValue {
  name
  description
  type {
    ...TypeRef
  }
  defaultValue
}

fragment TypeRef on __Type {
  kind
  name
  ofType {
    kind
    name
    ofType {
      kind
      name
      ofType {
        kind
        name
        ofType {
          kind
          name
          ofType {
            kind
            name
            ofType {
              kind
              name
              ofType {
                kind
                name
              }
            }
          }
        }
      }
    }
  }
";
}