using System.ComponentModel.DataAnnotations;

namespace Api.Models;

// ── Product DTOs ──────────────────────────────────────────────────────────────
public record ProductDto(
    Guid Id,
    string Name,
    string? Description,
    decimal Price,
    int Stock,
    bool IsActive,
    Guid CategoryId,
    string? CategoryName,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public record CreateProductRequest(
    [Required, StringLength(200, MinimumLength = 2)] string Name,
    string? Description,
    [Range(0, double.MaxValue)] decimal Price,
    [Range(0, int.MaxValue)] int Stock,
    [Required] Guid CategoryId
);

public record UpdateProductRequest(
    [StringLength(200, MinimumLength = 2)] string? Name,
    string? Description,
    [Range(0, double.MaxValue)] decimal? Price,
    [Range(0, int.MaxValue)] int? Stock,
    bool? IsActive,
    Guid? CategoryId
);

// ── Category DTOs ─────────────────────────────────────────────────────────────
public record CategoryDto(Guid Id, string Name, string? Description, int ProductCount, DateTime CreatedAt);

public record CreateCategoryRequest(
    [Required, StringLength(100, MinimumLength = 2)] string Name,
    string? Description
);

// ── Pagination ────────────────────────────────────────────────────────────────
public record PagedResult<T>(IEnumerable<T> Items, int Total, int Page, int PageSize)
{
    public int TotalPages => (int)Math.Ceiling((double)Total / PageSize);
}

// ── Error response ────────────────────────────────────────────────────────────
public record ErrorResponse(string Message, string? Detail = null, IDictionary<string, string[]>? Errors = null);
