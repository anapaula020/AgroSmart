using System.ComponentModel.DataAnnotations;

namespace Api.Models;

public record LookupRequest(
    [Required, StringLength(100, MinimumLength = 2)] string Name,
    string? Description
);
