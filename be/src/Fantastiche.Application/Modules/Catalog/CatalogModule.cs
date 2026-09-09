using Fantastiche.Application.Infrastructure.Http;
using Fantastiche.Application.Infrastructure.Modules;
using Fantastiche.Infrastructure.Catalog;
using Fantastiche.Infrastructure.Common;
using FluentValidation;

namespace Fantastiche.Application.Modules.Catalog;

public sealed class CatalogModule : IRegistrableModule
{
    public void RegisterEndpoints(IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/Catalog").WithTags("Catalog").RequireAuthorization();
        group.MapPost("/Imports", Import).RequireAuthorization("SuperAdmin").WithName("ImportCatalog")
            .WithSummary("Importa listone in bozza")
            .WithDescription("Importa il CSV Fantacalcio a 19 colonne con stagione esplicita. La bozza resta visibile al SuperAdmin fino alla pubblicazione. Quotazioni e statistiche non ancora mappate.")
            .Produces<ApiResponse<ListVersionView>>(StatusCodes.Status201Created);
        group.MapPost("/Versions/{listVersionId:guid}/Publish", Publish).RequireAuthorization("SuperAdmin").WithName("PublishCatalog")
            .WithSummary("Pubblica listone")
            .WithDescription("Rende consultabile la versione verificata. Le leghe conservano la versione già selezionata.")
            .Produces<ApiResponse<ListVersionView>>();
        group.MapGet("/Versions", Versions).WithName("GetCatalogVersions")
            .WithSummary("Versioni del listone")
            .WithDescription("Elenco paginato delle versioni pubblicate; il SuperAdmin vede anche le bozze. Filtro facoltativo per stagione.")
            .Produces<ApiResponse<CatalogPage<ListVersionView>>>();
        group.MapGet("/Versions/{listVersionId:guid}/Entries", Entries).WithName("GetCatalogEntries")
            .WithSummary("Calciatori del listone")
            .WithDescription("Snapshot paginato con filtri nome, ruolo Classic e club. Le bozze sono riservate al SuperAdmin.")
            .Produces<ApiResponse<CatalogPage<CatalogEntryView>>>();
    }

    private static async Task<IResult> Import(ImportCatalogRequest request, HttpContext context,
        IValidator<ImportCatalogRequest> validator, IRequestPublisher publisher, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var result = await publisher.SendAsync<ImportCatalogCommand, ListVersionView>(
            new(context.CreateRequestContext(), request.SeasonName, request.Csv), ct);
        return ApiResults.Created($"/api/Catalog/Versions/{result.Id}/Entries", result);
    }

    private static async Task<IResult> Publish(Guid listVersionId, HttpContext context, IRequestPublisher publisher, CancellationToken ct)
        => ApiResults.Ok(await publisher.SendAsync<PublishCatalogCommand, ListVersionView>(new(context.CreateRequestContext(), listVersionId), ct));

    private static async Task<IResult> Versions(string? seasonName, int? page, int? pageSize, HttpContext context,
        IValidator<CatalogVersionsRequest> validator, IRequestPublisher publisher, CancellationToken ct)
    {
        var request = new CatalogVersionsRequest(seasonName, page ?? 1, pageSize ?? 50);
        await validator.ValidateAndThrowAsync(request, ct);
        return ApiResults.Ok(await publisher.QueryAsync<GetCatalogVersionsQuery, CatalogPage<ListVersionView>>(
            new(context.CreateRequestContext(), request.SeasonName, request.Page, request.PageSize), ct));
    }

    private static async Task<IResult> Entries(Guid listVersionId, string? search, string? role, string? club, int? page, int? pageSize,
        HttpContext context, IValidator<CatalogEntriesRequest> validator, IRequestPublisher publisher, CancellationToken ct)
    {
        var request = new CatalogEntriesRequest(search, role, club, page ?? 1, pageSize ?? 50);
        await validator.ValidateAndThrowAsync(request, ct);
        return ApiResults.Ok(await publisher.QueryAsync<GetCatalogEntriesQuery, CatalogPage<CatalogEntryView>>(
            new(context.CreateRequestContext(), listVersionId, request.Search, request.Role, request.Club, request.Page, request.PageSize), ct));
    }
}
