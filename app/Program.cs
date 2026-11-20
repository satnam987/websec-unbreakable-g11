using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using Unbreakable.Authorization;
using Unbreakable.Data;
using Unbreakable.Models;

var builder = WebApplication.CreateBuilder(args);

// 'sub' niet laten hernoemen naar de lange .NET claim-URI
JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

builder.Services.AddDbContext<UnbreakableContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Unbreakable")));

builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

// ---------------------------------------------------------------
// Opdracht 4 - AUTHENTICATIE: access token van Keycloak valideren
// ---------------------------------------------------------------
var authority = builder.Configuration["Oidc:Authority"];
var audience = builder.Configuration["Oidc:Audience"];
var metadata = builder.Configuration["Oidc:MetadataAddress"];

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = authority;
        options.MapInboundClaims = false;

        // De container praat intern met Keycloak, maar het token draagt
        // de publieke issuer. Daarom metadata intern, issuer publiek.
        if (!string.IsNullOrEmpty(metadata))
        {
            options.MetadataAddress = metadata;
            options.RequireHttpsMetadata = false;
        }

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            ValidAlgorithms = new[] { SecurityAlgorithms.RsaSha256 }, // sluit alg=none en HS256 uit
            ValidateIssuer = true,
            ValidIssuer = authority,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "sub"
        };

        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = ctx =>
            {
                if (string.IsNullOrWhiteSpace(ctx.Principal?.FindFirst("sub")?.Value))
                {
                    ctx.Fail("sub ontbreekt in het token");
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// De PDP-client (OpenFGA)
builder.Services.AddSingleton<Pdp>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<UnbreakableContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    for (var attempt = 1; attempt <= 30; attempt++)
    {
        try
        {
            context.Database.EnsureCreated();
            SeedData.Initialize(context);
            break;
        }
        catch (Exception ex) when (attempt < 30)
        {
            logger.LogWarning("Databank nog niet bereikbaar ({Error}), poging {Attempt}", ex.Message, attempt);
            Thread.Sleep(2000);
        }
    }
}

app.UseDeveloperExceptionPage();
app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => "ok");

// Alles onder /api vereist een geldig token. De header X-User bestaat niet meer.
var api = app.MapGroup("/api").RequireAuthorization();

// Houdt bij welke sub bij welke gebruikersnaam hoort, zodat je een bericht
// aan "alice" kan sturen terwijl de tuples met de sub werken.
api.AddEndpointFilter(async (ctx, next) =>
{
    var http = ctx.HttpContext;
    var sub = http.User.FindFirst("sub")?.Value;
    if (!string.IsNullOrEmpty(sub))
    {
        var db = http.RequestServices.GetRequiredService<UnbreakableContext>();
        var name = http.User.FindFirst("preferred_username")?.Value ?? sub;
        var known = await db.Users.FindAsync(sub);
        if (known == null)
        {
            db.Users.Add(new AppUser { Sub = sub, Username = name });
            await db.SaveChangesAsync();
        }
        else if (known.Username != name)
        {
            known.Username = name;
            await db.SaveChangesAsync();
        }
    }
    return await next(ctx);
});

api.MapGet("/me", (HttpContext http) => Results.Ok(new
{
    sub = http.User.FindFirst("sub")?.Value,
    username = http.User.FindFirst("preferred_username")?.Value
}));

api.MapGet("/users", async (UnbreakableContext db) =>
    Results.Ok(await db.Users.Select(u => u.Username).OrderBy(n => n).ToListAsync()));

// INBOX: geen "WHERE Recipient = ..." meer. De PDP zegt wat je mag zien.
api.MapGet("/messages", async (HttpContext http, UnbreakableContext db, Pdp pdp) =>
{
    var ids = await pdp.ListReadable(http.User.FindFirst("sub").Value);

    var inbox = db.Messages.Where(m => ids.Contains(m.Id));

    var from = http.Request.Query["from"].ToString();
    if (!string.IsNullOrEmpty(from))
    {
        inbox = inbox.Where(m => m.Sender == from);
    }

    return Results.Ok(await inbox.OrderByDescending(m => m.SentAt).ToListAsync());
});

// ZOEKEN: binnen wat de PDP toelaat. De rauwe SQL is weg, dus ook de SQL-injectie.
api.MapGet("/messages/search", async (HttpContext http, UnbreakableContext db, Pdp pdp) =>
{
    var ids = await pdp.ListReadable(http.User.FindFirst("sub").Value);
    var q = http.Request.Query["q"].ToString();
    var pattern = "%" + q + "%";

    var messages = await db.Messages
        .Where(m => ids.Contains(m.Id) &&
                    (EF.Functions.ILike(m.Subject, pattern) || EF.Functions.ILike(m.Body, pattern)))
        .OrderByDescending(m => m.SentAt)
        .ToListAsync();

    return Results.Ok(messages);
});

// EXPORT: dezelfde beslissing van dezelfde PDP.
api.MapGet("/messages/export", async (HttpContext http, UnbreakableContext db, Pdp pdp) =>
{
    var ids = await pdp.ListReadable(http.User.FindFirst("sub").Value);

    var messages = await db.Messages
        .Where(m => ids.Contains(m.Id))
        .OrderBy(m => m.SentAt)
        .ToListAsync();

    return Results.Text(JsonConvert.SerializeObject(messages, Formatting.Indented), "application/json");
});

// EEN BERICHT: vroeger kon iedereen elk id opvragen (IDOR uit opdracht 2).
api.MapGet("/messages/{id:int}", async (int id, HttpContext http, UnbreakableContext db, Pdp pdp) =>
{
    if (!await pdp.Check(http.User.FindFirst("sub").Value, Pdp.CanRead, id))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var message = await db.Messages.FindAsync(id);
    return message == null ? Results.NotFound() : Results.Ok(message);
});

api.MapPost("/messages", async (Message message, HttpContext http, UnbreakableContext db, Pdp pdp) =>
{
    // De afzender komt uit het token, nooit uit de body.
    var senderSub = http.User.FindFirst("sub").Value;
    var senderName = http.User.FindFirst("preferred_username")?.Value ?? senderSub;

    var recipient = await db.Users.FirstOrDefaultAsync(u => u.Username == message.Recipient);
    if (recipient == null)
    {
        return Results.BadRequest("Onbekende ontvanger. Die moet eerst een keer ingelogd zijn.");
    }

    message.Id = 0;
    message.Sender = senderName;
    message.SenderSub = senderSub;
    message.Recipient = recipient.Username;
    message.RecipientSub = recipient.Sub;
    message.SentAt = DateTime.UtcNow;

    db.Messages.Add(message);
    await db.SaveChangesAsync();

    // Relaties wegschrijven in de PDP.
    await pdp.WriteTuples(message.Id, senderSub, recipient.Sub);

    return Results.Created($"/api/messages/{message.Id}", message);
});

api.MapDelete("/messages/{id:int}", async (int id, HttpContext http, UnbreakableContext db, Pdp pdp) =>
{
    if (!await pdp.Check(http.User.FindFirst("sub").Value, Pdp.CanDelete, id))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var message = await db.Messages.FindAsync(id);
    if (message == null)
    {
        return Results.NotFound();
    }

    db.Messages.Remove(message);
    await db.SaveChangesAsync();

    await pdp.DeleteTuples(id, message.SenderSub, message.RecipientSub);

    return Results.NoContent();
});

app.Run();
