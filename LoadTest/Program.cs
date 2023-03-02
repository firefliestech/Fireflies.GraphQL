using NBomber.CSharp;
using NBomber.Http.CSharp;

using var httpClient = new HttpClient();

var scenario = Scenario.Create("http_scenario", async context => {
        var request =
            Http.CreateRequest("POST", "https://localhost:7273/graphql")
                .WithHeader("Accept", "text/html")
                .WithBody(new StringContent(@"{""query"":""{ books { bookId numbers title iSBN } }""}"));

        var response = await Http.Send(httpClient, request);

        return response;
    })
    .WithWarmUpDuration(TimeSpan.FromSeconds(2))
    .WithLoadSimulations(Simulation.Inject(rate: 20,
        interval: TimeSpan.FromSeconds(1),
        during: TimeSpan.FromSeconds(5)));

NBomberRunner
    .RegisterScenarios(scenario)
    .Run();

Console.ReadLine();