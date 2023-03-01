using NBomber.CSharp;

using var httpClient = new HttpClient();

var scenario = Scenario.Create("hello_world_scenario", async context => {
        var response = await httpClient.PostAsync("https://localhost:7273/graphql", new StringContent(@"{""query"":""{
  books {
    bookId
    numbers
    title
    iSBN
  }
}""}"));

        return response.IsSuccessStatusCode
            ? Response.Ok()
            : Response.Fail();
    })
    .WithWarmUpDuration(TimeSpan.FromSeconds(5))
    .WithLoadSimulations(
        Simulation.Inject(rate: 200,
            interval: TimeSpan.FromSeconds(1),
            during: TimeSpan.FromSeconds(10))
    );

NBomberRunner
    .RegisterScenarios(scenario)
    .Run();

Console.ReadLine();