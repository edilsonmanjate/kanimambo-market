using Kanimambo.Api;
using Kanimambo.Api.ErrorHandling;
using Kanimambo.Api.Idempotency;
using Kanimambo.Api.Persistence;
using Kanimambo.Api.Security;
using Kanimambo.Catalog.Api.Controllers;
using Kanimambo.Catalog.Application;
using Kanimambo.Catalog.Infrastructure;
using Kanimambo.SharedKernel.Messaging;
using Kanimambo.SharedKernel.Messaging.Behaviors;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddPlatformPersistence();
builder.AddRedisDistributedCache(ConnectionNames.Redis);

builder.Services.AddKanimamboProblemDetails();
builder.Services.AddKanimamboSecurity();
builder.Services.AddOpenApi();
builder.Services.AddIdempotency(builder.Configuration);

// Modules: each contributes its controllers (application part), handlers (Mediator) and infrastructure.
builder.AddCatalogModule();
builder.Services.AddKanimamboControllers()
    .AddApplicationPart(typeof(CatalogController).Assembly);

// Behaviors run in registration order (first = outermost).
builder.Services.AddMediator(mediator => mediator
    .AddModule(CatalogModule.Name, typeof(CatalogModule).Assembly)
    .AddBehavior(typeof(LoggingBehavior<,>))
    .AddBehavior(typeof(ValidationBehavior<,>))
    .AddBehavior(typeof(UnitOfWorkBehavior<,>))
    .AddBehavior(typeof(IdempotencyBehavior<,>)));

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();
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
