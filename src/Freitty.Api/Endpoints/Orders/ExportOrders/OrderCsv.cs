using System.Globalization;
using System.Text;
using Freitty.Application.Orders.ExportOrders;
using Freitty.Domain.Orders;

namespace Freitty.Api.Endpoints.Orders.ExportOrders;

// CSV is a presentation of the rows, so it is written here: RFC 4180 — CRLF line ends, a field quoted when it holds
// a comma, a quote or a line break, quotes doubled — in UTF-8 with a byte order mark, without which Excel reads the
// file in the local code page. Numbers and instants are written in the invariant culture.
internal static class OrderCsv
{
    public const string MediaType = "text/csv; charset=utf-8";

    private const string LineEnd = "\r\n";
    private const string Separator = ",";
    private const string Quote = "\"";
    private const string EscapedQuote = "\"\"";
    private const string FormulaGuard = "'";
    private const string InstantFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";
    private const string DriverPrefix = "Driver: ";
    private const string SelfPickup = "Self pickup";

    private static readonly string[] Header =
    [
        "Number", "Type", "Status", "Hub", "Scheduled at (UTC)", "Destination", "Carrier", "Shipments", "Declared",
        "Actual", "Open alerts", "Created by",
    ];

    private static readonly char[] CharactersToQuote = [',', '"', '\r', '\n'];

    // A spreadsheet runs a cell that starts with one of these as a formula, which a value typed by a user — a city,
    // a driver's name — must never become; the apostrophe makes the cell text.
    private static readonly char[] FormulaStarts = ['=', '+', '-', '@', '\t', '\r'];

    private static readonly UTF8Encoding Utf8WithByteOrderMark = new(encoderShouldEmitUTF8Identifier: true);

    public static byte[] Write(IReadOnlyList<OrderExportRow> rows)
    {
        var csv = new StringBuilder();
        AppendLine(csv, Header);
        foreach (OrderExportRow row in rows)
        {
            AppendLine(csv, FieldsOf(row));
        }

        return [.. Utf8WithByteOrderMark.GetPreamble(), .. Utf8WithByteOrderMark.GetBytes(csv.ToString())];
    }

    // Enums are written by the names the API's JSON and query parameters use.
    private static string[] FieldsOf(OrderExportRow row)
    {
        return
        [
            row.Number,
            row.Type.ToString(),
            row.Status.ToString(),
            row.HubName,
            row.ScheduledAt?.UtcDateTime.ToString(InstantFormat, CultureInfo.InvariantCulture) ?? string.Empty,
            row.DestinationCity is null ? string.Empty : $"{row.DestinationCity}, {row.DestinationRegion}",
            TransportOf(row),
            Number(row.ShipmentCount),
            Number(row.DeclaredQuantity),
            Number(row.ActualQuantity),
            Number(row.OpenAlertCount),
            row.CreatedByName,
        ];
    }

    // The words of the order cards.
    private static string TransportOf(OrderExportRow row)
    {
        return row.TransportKind switch
        {
            TransportKind.Carrier => row.CarrierName ?? string.Empty,
            TransportKind.OwnDriver => $"{DriverPrefix}{row.DriverName}",
            TransportKind.SelfPickup => SelfPickup,
            _ => string.Empty,
        };
    }

    private static string Number(int value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }

    private static void AppendLine(StringBuilder csv, IEnumerable<string> fields)
    {
        csv.AppendJoin(Separator, fields.Select(Escaped)).Append(LineEnd);
    }

    private static string Escaped(string value)
    {
        string guarded = value.Length > 0 && FormulaStarts.Contains(value[0]) ? FormulaGuard + value : value;
        return guarded.IndexOfAny(CharactersToQuote) >= 0
            ? Quote + guarded.Replace(Quote, EscapedQuote, StringComparison.Ordinal) + Quote
            : guarded;
    }
}
