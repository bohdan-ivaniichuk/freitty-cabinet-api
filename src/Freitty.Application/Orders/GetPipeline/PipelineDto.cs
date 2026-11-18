namespace Freitty.Application.Orders.GetPipeline;

// Always one column per status, in the order an order goes through them.
public sealed record PipelineDto(IReadOnlyList<PipelineColumnDto> Columns);
