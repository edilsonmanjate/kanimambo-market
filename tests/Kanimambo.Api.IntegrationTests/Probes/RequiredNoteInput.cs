using System.ComponentModel.DataAnnotations;

namespace Kanimambo.Api.IntegrationTests.Probes;

public sealed record RequiredNoteInput([Required] string? Note);
