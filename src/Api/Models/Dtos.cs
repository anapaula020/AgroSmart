namespace Api.Models;

// ── Pagination ────────────────────────────────────────────────────────────────
public record PagedResult<T>(IEnumerable<T> Items, int Total, int Page, int PageSize)
{
    public int TotalPages => (int)Math.Ceiling((double)Total / PageSize);
}

// ── Error response ────────────────────────────────────────────────────────────
public record ErrorResponse(string Message, string? Detail = null, IDictionary<string, string[]>? Errors = null);
