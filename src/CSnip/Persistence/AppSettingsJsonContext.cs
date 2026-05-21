using System.Text.Json.Serialization;
using CSnip.Models.Settings;

namespace CSnip.Persistence;

[JsonSerializable(typeof(AppSettings))]
public partial class AppSettingsJsonContext : JsonSerializerContext;
