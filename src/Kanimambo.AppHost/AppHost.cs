var builder = DistributedApplication.CreateBuilder(args);

// One PostgreSQL database; every module owns its own schema inside it (see docs/adr/0001).
var postgres = builder.AddPostgres("postgres")
    .WithDataVolume("kanimambo-postgres-data")
    .WithLifetime(ContainerLifetime.Persistent);

var database = postgres.AddDatabase("kanimambo");

var redis = builder.AddRedis("redis");

// Local simulators of external systems: the project runs without any real credentials.
var exchangeRates = builder.AddProject<Projects.Kanimambo_Simulators_ExchangeRates>("exchange-rates");
var wholesaler = builder.AddProject<Projects.Kanimambo_Simulators_Wholesaler>("wholesaler");

builder.AddProject<Projects.Kanimambo_Api>("api")
    .WithReference(database)
    .WaitFor(database)
    .WithReference(redis)
    .WaitFor(redis)
    .WithReference(exchangeRates)
    .WithReference(wholesaler)
    .WithExternalHttpEndpoints();

await builder.Build().RunAsync();
