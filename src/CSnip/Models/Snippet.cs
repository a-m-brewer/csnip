namespace CSnip.Models;

public record Snippet(
    string Command,
    string? Description,
    string[] Tags);
