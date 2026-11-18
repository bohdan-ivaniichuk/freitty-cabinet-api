using System.Net.Mime;
using FastEndpoints;
using Freitty.Application.Orders.ExportOrders;
using Microsoft.Net.Http.Headers;

namespace Freitty.Api.Endpoints.Orders.ExportOrders;

internal sealed class ExportOrdersEndpoint : Endpoint<ExportOrdersQuery>
{
    private const string FileName = "orders.csv";

    // Set by hand, so the header is exactly the one the export promises: a plain, quoted file name.
    private static readonly string ContentDisposition =
        new ContentDispositionHeaderValue(DispositionTypeNames.Attachment) { FileName = $"\"{FileName}\"" }
            .ToString();

    public override void Configure()
    {
        Get(ApiRoutes.Orders.Export);
        Description(endpoint => endpoint
            .ClearDefaultProduces()
            .Produces(StatusCodes.Status200OK, typeof(string), OrderCsv.MediaType)
            .ProducesProblemDetails());
        Summary(summary =>
        {
            summary.Summary = "Export the filtered Order List as CSV";
            summary.Description =
                "The whole list under the same tabs and filters as the list, in its order, as a CSV file " +
                $"(RFC 4180, UTF-8 with a byte order mark), at most {ExportOrdersQueryHandler.MaxExportRows} " +
                "orders. Columns: Number, Type, Status, Hub, Scheduled at (UTC), Destination, Carrier, Shipments, " +
                "Declared, Actual, Open alerts, Created by. A value that a spreadsheet would run as a formula " +
                "starts with an apostrophe.";
            summary.RequestParam(query => query.Type, OrderFilterDescriptions.Type);
            summary.RequestParam(query => query.Status, OrderFilterDescriptions.Status);
            summary.RequestParam(query => query.HasOpenAlerts, OrderFilterDescriptions.HasOpenAlerts);
            summary.RequestParam(query => query.HubId, OrderFilterDescriptions.HubId);
            summary.RequestParam(query => query.DatePreset, OrderFilterDescriptions.DatePreset);
            summary.RequestParam(query => query.Search, OrderFilterDescriptions.Search);
            summary.Responses[StatusCodes.Status200OK] = "The CSV file, a header row and one row per order.";
            summary.Responses[StatusCodes.Status400BadRequest] = "A parameter is out of range or not understood.";
        });
    }

    public override async Task HandleAsync(ExportOrdersQuery query, CancellationToken ct)
    {
        IReadOnlyList<OrderExportRow> rows = await query.ExecuteAsync(ct);
        HttpContext.Response.Headers.ContentDisposition = ContentDisposition;
        await Send.BytesAsync(OrderCsv.Write(rows), contentType: OrderCsv.MediaType, cancellation: ct);
    }
}
