using Kanimambo.Api;
using Kanimambo.Api.ErrorHandling;
using Kanimambo.Api.Idempotency;
using Kanimambo.Api.Persistence;
using Kanimambo.SharedKernel.Messaging;
using Kanimambo.SharedKernel.Messaging.Behaviors;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddPlatformPersistence();
builder.AddRedisDistributedCache(ConnectionNames.Redis);

builder.Services.AddKanimamboProblemDetails();
builder.Services.AddKanimamboControllers();
builder.Services.AddOpenApi();
builder.Services.AddIdempotency(builder.Configuration);

// Behaviors run in registration order (first = outermost). Each module is declared with
// AddModule("<Module>", typeof(<Module>ApplicationMarker).Assembly) as it is implemented.
builder.Services.AddMediator(mediator => mediator
    .AddBehavior(typeof(LoggingBehavior<,>))
    .AddBehavior(typeof(ValidationBehavior<,>))
    .AddBehavior(typeof(UnitOfWorkBehavior<,>))
    .AddBehavior(typeof(IdempotencyBehavior<,>)));

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseIdempotency();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapControllers();
app.MapDefaultEndpoints();

await app.MigrateDatabaseAsync();
await app.RunAsync();
