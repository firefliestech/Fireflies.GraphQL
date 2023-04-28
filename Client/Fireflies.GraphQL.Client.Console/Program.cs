using CommandLine;
using Fireflies.GraphQL.Client.Console.Generate;
using Fireflies.GraphQL.Client.Console.Schema;

Parser.Default.ParseArguments<GenerateOptions, InitOptions, UpdateOptions>(args)
    .MapResult((GenerateOptions o) => RunGenerate(o),
        (InitOptions o) => RunInit(o),
        (UpdateOptions o) => RunUpdate(o),
        errs => 1);

int RunUpdate(UpdateOptions options) {
    var handler = new SchemaHandler();
    return (int)handler.Update(options).GetAwaiter().GetResult();
}

int RunInit(InitOptions options) {
    var handler = new SchemaHandler();
    return (int)handler.Init(options).GetAwaiter().GetResult();
}

int RunGenerate(GenerateOptions options) {
    var handler = new GenerateHandler();
    return (int)handler.Generate(options).GetAwaiter().GetResult();
}