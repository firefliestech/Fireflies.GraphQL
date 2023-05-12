using CommandLine;
using CommandLine.Text;
using Fireflies.GraphQL.Client.Console;
using Fireflies.GraphQL.Client.Console.Generate;
using Fireflies.GraphQL.Client.Console.Schema;

var parser = new Parser(with => with.HelpWriter = null);
var parseResult = parser.ParseArguments<ProjectInitOptions, GenerateOptions, ClientInitOptions, ClientUpdateOptions>(args);

DisplayHelp(parseResult);

parseResult.WithParsed(_ => {
    parseResult.MapResult((ProjectInitOptions o) => RunProjectInit(o),
        (ClientInitOptions o) => RunClientInit(o),
        (ClientUpdateOptions o) => RunClientUpdate(o),
        (GenerateOptions o) => RunGenerate(o),
        _ => 1);
});

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

static void DisplayHelp(ParserResult<object> result) {
    if(!result.Errors.Any()) {
        ConsoleLogger.WriteInfo("Fireflies GraphQL Client Generator");
        ConsoleLogger.WriteInfo("==================================");
    } else {
        var helpText = HelpText.AutoBuild(result, h => {
            h.AdditionalNewLineAfterOption = false;
            h.Heading = "Fireflies GraphQL Client Generator";
            h.Copyright = "==================================";
            return HelpText.DefaultParsingErrorsHandler(result, h);
        }, e => e);

        ConsoleLogger.WriteInfo(helpText);
    }
}