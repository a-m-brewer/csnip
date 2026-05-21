using System.Text.Json.Serialization;
using CSnip.Models;

namespace CSnip.Persistence;

[JsonSerializable(typeof(List<Snippet>))]
[JsonSerializable(typeof(Snippet))]
public partial class SnippetJsonContext : JsonSerializerContext;
