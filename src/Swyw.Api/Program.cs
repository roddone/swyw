using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using NSwag.Annotations;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

Console.WriteLine("Application is Starting");

var builder = WebApplication.CreateSlimBuilder(args);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerDocument();
builder.Services.AddCors();

var app = builder.Build();
if (!app.Environment.IsDevelopment()) app.UseHttpsRedirection();

app.UseCors(c =>
{
    c.AllowAnyHeader();
    c.AllowAnyMethod();
    c.AllowAnyOrigin();
});
app.Use(async (ctx, next) =>
{
    if (!ctx.Request.Path.StartsWithSegments("/api"))
    {
        await next();
        return;
    }

    Console.WriteLine($"Start processing request {ctx.Request.Path} ...");
    try
    {
        Stopwatch sw = Stopwatch.StartNew();
        await next();
        Console.WriteLine($"Request {ctx.Request.Path} ended, elapsed : {sw.Elapsed}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Request {ctx.Request.Path} failed, reason was : {ex.ToString()}");
        Console.WriteLine(ex);
    }

});
app.UseOpenApi();
app.UseSwaggerUi();

var genericDictionary = new ConcurrentDictionary<string, Dictionary<string, JsonDocument>>();
genericDictionary.AddOrUpdate("user1", new Dictionary<string, JsonDocument>() { { "entity1", JsonDocument.Parse(@"{ ""test"": true }") } }, (k, v) => v);
genericDictionary.AddOrUpdate("user2", new Dictionary<string, JsonDocument>() { { "entity1", JsonDocument.Parse(@"{ ""test"": true }") }, { "entity2", JsonDocument.Parse(@"{ ""une-autre-entite"": 42 }") } }, (k, v) => v);

var api = app.MapGroup("api").WithTags("Api");
//récupère tout le dictionnaire d'un utilisateur
api.MapGet("/{user}", (string user) => genericDictionary.TryGetValue(user, out var output) ? Results.Ok(output) : Results.NotFound())
    .WithDescription("Get user entities")
    .WithSummary("Get user entities")
    .Produces((int)HttpStatusCode.NotFound)
    .WithOpenApi(c =>
    {
        c.Responses.Add("200", new OpenApiResponse() { Description = "Entities with Json format" });
        return c;
    });

//récupère une entité particulière du dictionnaire
api.MapGet("/{user}/{entityName}", (string user, string entityName) =>
{
    if (genericDictionary.TryGetValue(user, out var output))
    {
        return output.TryGetValue(entityName, out var result) ? Results.Ok(result) : Results.NotFound();
    }
    return Results.NotFound();
})
    .WithDescription("Get user entity by its name")
    .WithSummary("Get user entity by its name")
    .Produces((int)HttpStatusCode.NotFound)
    .WithOpenApi(c =>
    {
        c.Responses.Add("200", new OpenApiResponse() { Description = "Entity with Json format" });
        return c;
    });

//vide le dictionnaire d'un utilisateur
api.MapGet("/{user}/clear", (string user) =>
{
    genericDictionary[user].Clear();
    return Results.NoContent();
})
    .WithDescription("Clear user entities")
    .WithSummary("Clear user entities")
    .Produces((int)HttpStatusCode.NoContent)
    .WithOpenApi();

//rajoute une entité pour un utilisateur
api.MapPost("/{user}/{entityName}", (string user, string entityName, JsonDocument entity) =>
{
    if (!genericDictionary.ContainsKey(user)) genericDictionary.TryAdd(user, []);

    if (genericDictionary[user].ContainsKey(entityName)) return Results.Conflict(entityName);

    genericDictionary[user].Add(entityName, entity);

    return Results.NoContent();
})
    .WithDescription("Create an entity for the specified user")
    .WithSummary("Create an entity for the specified user")
    .Produces((int)HttpStatusCode.NoContent)
    .Produces((int)HttpStatusCode.Conflict)
    .WithOpenApi();



//met à jour une entité pour un utilisateur
api.MapPut("/{user}/{entityName}", ([FromRoute] string user, [FromRoute] string entityName, JsonDocument entity) =>
{
    if (!genericDictionary.TryGetValue(user, out Dictionary<string, JsonDocument>? value)) return Results.NotFound(user);

    if (!value.ContainsKey(entityName)) return Results.NotFound(entityName);
    value[entityName] = entity;

    return Results.NoContent();
})
    .WithDescription("Update an entity for the specified user")
    .WithSummary("Update an entity for the specified user")
    .Produces((int)HttpStatusCode.NoContent)
    .Produces((int)HttpStatusCode.NotFound)
    .WithOpenApi();

//supprime une entité pour un utilisateur
api.MapDelete("/{user}/{entityName}", (string user, string entityName) =>
{
    if (!genericDictionary.TryGetValue(user, out Dictionary<string, JsonDocument>? value)) return Results.NotFound(user);

    if (!value.ContainsKey(entityName)) return Results.NotFound(entityName);
    value.Remove(entityName);

    return Results.NoContent();
})
    .WithDescription("Delete an entity for the specified user")
    .WithSummary("Delete an entity for the specified user")
    .Produces((int)HttpStatusCode.NoContent)
    .Produces((int)HttpStatusCode.NotFound)
    .WithOpenApi();

//supprime un utilisateur
api.MapDelete("/{user}", (string user) => genericDictionary.Remove(user, out _) ? Results.NoContent() : Results.NotFound())
    .WithDescription("Delete a user")
    .WithSummary("Delete a user")
    .Produces((int)HttpStatusCode.NoContent)
    .Produces((int)HttpStatusCode.NotFound)
    .WithOpenApi();

//route me permettant de voir tout ce qu'ils ont fait
api.MapGet("/all", () => genericDictionary).ExcludeFromDescription();


Console.WriteLine("Application is initialized and will run");
await app.RunAsync();

Console.WriteLine("App has started");