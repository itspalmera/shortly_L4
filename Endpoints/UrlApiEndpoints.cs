using System.Security.Claims;
using System.Xml.Serialization;
using Shortly.Application.DTOs;
using Shortly.Application.Interfaces;
using Shortly.Infrastructure.Persistence;

namespace Shortly.Endpoints;

public static class UrlApiEndpoints
{
    public static void MapUrlApiEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api");

        // 1. POST /api/urls — Crear una URL corta
        group.MapPost("/urls", async (UrlRequest request, ILinkService linkService, HttpContext context) =>
        {
            if (string.IsNullOrWhiteSpace(request.Url))
            {
                return Results.BadRequest(new { error = "The URL field is required." });
            }

            long userId = 1;
            var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim is not null && long.TryParse(userIdClaim, out var parsedId))
            {
                userId = parsedId;
            }

            var linkResponse = await linkService.CreateLink(request.Url, userId);
            var locationUri = $"/api/urls/{linkResponse.Id}";
            
            return NegotiateResponse(context, linkResponse, statusCode: 201, locationUri: locationUri);
        });

        // 2. GET /api/urls — Listar todas las URLs
        group.MapGet("/urls", async (ILinkService linkService, HttpContext context) =>
        {
            var links = await linkService.GetAllLinks();
            return NegotiateResponse(context, links);
        });

        // 3. GET /api/urls/{id} — Obtener detalles por ID
        group.MapGet("/urls/{id:long}", async (long id, ILinkRepository linkRepository, HttpContext context) =>
        {
            var link = await linkRepository.GetByIdAsync(id);
            if (link is null)
            {
                return Results.NotFound(new { error = $"Url with ID {id} was not found." });
            }

            var response = LinkResponse.From(link);
            return NegotiateResponse(context, response);
        });

        // 4. DELETE /api/urls/{id} — ¡AQUÍ VA TU MÉTODO!
        group.MapDelete("/urls/{id:long}", async (long id, AppDbContext db) =>
        {
            var link = await db.Links.FindAsync(id);
            if (link is null) return Results.NotFound(new { error = $"Url with ID {id} was not found." });
            
            db.Links.Remove(link);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // 5. GET /api/stats — Obtener estadísticas
        group.MapGet("/stats", async (ILinkService linkService, HttpContext context) =>
        {
            var links = await linkService.GetAllLinks();
            var stats = new StatsResponse
            {
                TotalLinks = links.Count,
                TotalClicks = links.Sum(l => l.Clicks)
            };

            return NegotiateResponse(context, stats);
        });
    }

    // El método NegotiateResponse se queda igual abajo...
    private static IResult NegotiateResponse<T>(HttpContext context, T data, int statusCode = 200, string? locationUri = null)
    {
        var acceptHeader = context.Request.Headers.Accept.ToString().ToLowerInvariant();

        if (string.IsNullOrEmpty(acceptHeader) || acceptHeader.Contains("*/*") || acceptHeader.Contains("application/json"))
        {
            return locationUri is not null 
                ? Results.Created(locationUri, data) 
                : Results.Json(data, statusCode: statusCode);
        }

        if (acceptHeader.Contains("application/xml") || acceptHeader.Contains("text/xml"))
        {
            try
            {
                using var stringWriter = new StringWriter();
                var serializer = new XmlSerializer(typeof(T));
                serializer.Serialize(stringWriter, data);
                
                if (locationUri is not null)
                {
                    context.Response.Headers.Location = locationUri;
                    context.Response.StatusCode = 201;
                }

                return Results.Content(stringWriter.ToString(), "application/xml", System.Text.Encoding.UTF8, statusCode);
            }
            catch (Exception)
            {
                return Results.Problem("An error occurred during XML serialization.", statusCode: 500);
            }
        }

        return Results.StatusCode(406);
    }
}