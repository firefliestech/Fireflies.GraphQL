// See https://aka.ms/new-console-template for more information
//using MyNamespace;

using Fireflies.GraphQL.ClientDemo.GraphQL.GraphQLDemo;

Console.WriteLine("Hello, World!"); 
//GeneratedFile.HelloFrom("Kalle");
var client = new GraphQLDemoClient(new Uri("https://localhost:7273/graphql"));
var reply = await client.MyBasicQuery(1, "kalle");
Console.WriteLine(reply.Books.Count());
Console.WriteLine("DONE!");
Console.ReadLine();