using Kanimambo.Api;
using Kanimambo.Api.ErrorHandling;
using Kanimambo.Api.Idempotency;
using Kanimambo.Api.Persistence;
using Kanimambo.Api.Security;
using Kanimambo.Cart.Api.Controllers;
using Kanimambo.Cart.Application;
using Kanimambo.Cart.Infrastructure;
using Kanimambo.Catalog.Api.Controllers;
using Kanimambo.Catalog.Application;
using Kanimambo.Catalog.Infrastructure;
using Kanimambo.Invoicing.Api.Controllers;
using Kanimambo.Invoicing.Application;
using Kanimambo.Invoicing.Infrastructure;
using Kanimambo.Notifications.Api.Controllers;
using Kanimambo.Notifications.Application;
using Kanimambo.Notifications.Infrastructure;
using Kanimambo.Ordering.Api.Controllers;
using Kanimambo.Ordering.Application;
using Kanimambo.Ordering.Infrastructure;
using Kanimambo.Payments.Api.Controllers;
using Kanimambo.Payments.Application;
using Kanimambo.Payments.Infrastructure;
using Kanimambo.Promotions.Api.Controllers;
using Kanimambo.Promotions.Application;
using Kanimambo.Promotions.Infrastructure;
using Kanimambo.SharedKernel.Infrastructure.Features;
using Kanimambo.SharedKernel.Messaging;
using Kanimambo.SharedKernel.Messaging.Behaviors;
using Kanimambo.Shipping.Api.Controllers;
using Kanimambo.Shipping.Application;
using Kanimambo.Shipping.Infrastructure;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddPlatformPersistence();
builder.AddRedisDistributedCache(ConnectionNames.Redis);

builder.Services.AddKanimamboProblemDetails();
builder.Services.AddKanimamboSecurity();
builder.Services.AddOpenApi();
builder.Services.AddIdempotency(builder.Configuration);
builder.Services.AddFeatureFlags(builder.Configuration);

// Modules: each contributes its controllers (application part), handlers (Mediator) and infrastructure.
builder.AddCatalogModule();
builder.AddPromotionsModule();
builder.AddCartModule();
builder.AddOrderingModule();
builder.AddPaymentsModule();
builder.AddShippingModule();
builder.AddInvoicingModule();
builder.AddNotificationsModule();
builder.Services.AddKanimamboControllers()
    .AddApplicationPart(typeof(CatalogController).Assembly)
    .AddApplicationPart(typeof(PromotionsController).Assembly)
    .AddApplicationPart(typeof(CartsController).Assembly)
    .AddApplicationPart(typeof(CheckoutController).Assembly)
    .AddApplicationPart(typeof(PaymentsController).Assembly)
    .AddApplicationPart(typeof(ShippingController).Assembly)
    .AddApplicationPart(typeof(InvoicingController).Assembly)
    .AddApplicationPart(typeof(NotificationsController).Assembly);

// Behaviors run in registration order (first = outermost).
builder.Services.AddMediator(mediator => mediator
    .AddModule(CatalogModule.Name, typeof(CatalogModule).Assembly)
    .AddModule(PromotionsModule.Name, typeof(PromotionsModule).Assembly)
    .AddModule(CartModule.Name, typeof(CartModule).Assembly)
    .AddModule(OrderingModule.Name, typeof(OrderingModule).Assembly)
    .AddModule(PaymentsModule.Name, typeof(PaymentsModule).Assembly)
    .AddModule(ShippingModule.Name, typeof(ShippingModule).Assembly)
    .AddModule(InvoicingModule.Name, typeof(InvoicingModule).Assembly)
    .AddModule(NotificationsModule.Name, typeof(NotificationsModule).Assembly)
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
