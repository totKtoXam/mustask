using Gridify;

namespace Mustask.Application.Features.Dictionaries.GetLanguages;

/// <summary>`GET /api/dictionaries/languages` . No `Accept-Language` here — a language has no translations.</summary>
public sealed record GetLanguagesQuery(IGridifyQuery Paging);
