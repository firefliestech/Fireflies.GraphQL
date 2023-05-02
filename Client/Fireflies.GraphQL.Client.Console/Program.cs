using CommandLine;
using Fireflies.GraphQL.Client.Console.Generate;
using Fireflies.GraphQL.Client.Console.Schema;

Parser.Default.ParseArguments<ProjectInitOptions, GenerateOptions, ClientInitOptions, ClientUpdateOptions>(args)
    .MapResult(
        (ProjectInitOptions o) => RunProjectInit(o),
        (ClientInitOptions o) => RunClientInit(o),
        (ClientUpdateOptions o) => RunClientUpdate(o),
        (GenerateOptions o) => RunGenerate(o),
        errs => 1);


int RunProjectInit(ProjectInitOptions options) {
    var handler = new ProjectHandler();
    return (int)handler.Init(options).GetAwaiter().GetResult();
}

int RunClientUpdate(ClientUpdateOptions options) {
    var handler = new SchemaHandler();
    return (int)handler.Update(options).GetAwaiter().GetResult();
}

int RunClientInit(ClientInitOptions options) {
    var handler = new SchemaHandler();
    return (int)handler.Init(options).GetAwaiter().GetResult();
}

int RunGenerate(GenerateOptions options) {
    var handler = new GenerateHandler();
    return (int)handler.Generate(options).GetAwaiter().GetResult();
}