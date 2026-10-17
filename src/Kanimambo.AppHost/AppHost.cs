var builder = DistributedApplication.CreateBuilder(args);

// One PostgreSQL database; every module owns its own schema inside it (see docs/adr/0001).
var postgres = builder.AddPostgres("postgres")
    .WithDataVolume("kanimambo-postgres-data")
    .WithLifetime(ContainerLifetime.Persistent);

var database = postgres.AddDatabase("kanimambo");

var redis = builder.AddRedis("redis");

builder.AddProject<Projects.Kanimambo_Api>("api")
    .WithReference(database)
    .WaitFor(database)
    .WithReference(redis)
    .WaitFor(redis)
    .WithExternalHttpEndpoints();

await builder.Build().RunAsync();
