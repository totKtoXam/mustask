using Mustask.Shared.Errors;

namespace Mustask.Domain.Imports;

/// <summary>
/// Error codes of the import module (doc-017 BR-IE-*, mod-024 api-contract § 6; ADR-0005 registry). Each constant has an entry with
/// Russian and Kazakh texts in <c>contracts/errors/imports.json</c>. The <c>imports.row.*</c> and <c>imports.dry_run.*</c> codes are
/// reasons and notes inside a dry-run report, not HTTP errors.
/// </summary>
[ErrorCodes]
public static class ImportErrorCodes
{
    // ----- HTTP errors -----

    public const string AccessForbidden = "imports.access_forbidden";

    public const string ValidationFailed = "imports.validation_failed";

    public const string TemplateNotFound = "imports.template.not_found";

    public const string TemplateNameTaken = "imports.template.name_taken";

    public const string TemplateLimitReached = "imports.template.limit_reached";

    public const string FileNotFound = "imports.file.not_found";

    public const string DryRunNotFound = "imports.dry_run.not_found";

    // ----- Field reasons of a template -----

    public const string TemplateNameRequired = "imports.template.name_required";

    public const string TemplateNameTooLong = "imports.template.name_too_long";

    public const string HeaderRowInvalid = "imports.source.header_row_invalid";

    public const string FirstDataRowInvalid = "imports.source.first_data_row_invalid";

    public const string HeaderEmpty = "imports.source.header_empty";

    public const string MappingSourceRequired = "imports.mapping.source_required";

    public const string MappingFieldUnknown = "imports.mapping.field_unknown";

    public const string MappingFieldTaken = "imports.mapping.field_taken";

    public const string MappingColumnUnknown = "imports.mapping.column_unknown";

    public const string MappingListIntoSingle = "imports.mapping.list_into_single";

    public const string MappingChainTooLong = "imports.mapping.chain_too_long";

    public const string MappingConverterUnknown = "imports.mapping.converter_unknown";

    public const string MappingConverterParamInvalid = "imports.mapping.converter_param_invalid";

    public const string MappingPatternTooLong = "imports.mapping.pattern_too_long";

    public const string MappingPatternInvalid = "imports.mapping.pattern_invalid";

    public const string MappingClearNotAllowed = "imports.mapping.clear_not_allowed";

    public const string RecodingDefaultRequired = "imports.recoding.default_required";

    public const string RecodingEmptyNotAllowed = "imports.recoding.empty_not_allowed";

    public const string RecodingTargetUnknown = "imports.recoding.target_unknown";

    public const string RecodingTooManyEntries = "imports.recoding.too_many_entries";

    // ----- The refusal of a file: one top-level code, the reason in errors.file -----

    public const string FileRefused = "imports.file.refused";

    public const string FileUnsupportedFormat = "imports.file.unsupported_format";

    public const string FileTooLarge = "imports.file.too_large";

    public const string FileTooManyRows = "imports.file.too_many_rows";

    public const string FileTooManyColumns = "imports.file.too_many_columns";

    public const string FileTooManySheets = "imports.file.too_many_sheets";

    public const string FileExpandsTooMuch = "imports.file.expands_too_much";

    public const string FileExternalReference = "imports.file.external_reference";

    public const string FileHasMacros = "imports.file.has_macros";

    public const string FileUnreadable = "imports.file.unreadable";

    public const string FileColumnsMissing = "imports.file.columns_missing";

    public const string FileSheetMissing = "imports.file.sheet_missing";

    // ----- Reasons and notes of a dry-run report -----

    public const string RowEmpty = "imports.row.empty";

    public const string RowConvertFailed = "imports.row.convert_failed";

    public const string RowValueNotRecoded = "imports.row.value_not_recoded";

    public const string RowRecodingOutdated = "imports.row.recoding_outdated";

    public const string RowProjectForbidden = "imports.row.project_forbidden";

    public const string RowProjectNotFound = "imports.row.project_not_found";

    public const string RowTypeNotFound = "imports.row.type_not_found";

    public const string RowStateNotFound = "imports.row.state_not_found";

    public const string RowAssigneeNotFound = "imports.row.assignee_not_found";

    public const string RowTeamNotLinked = "imports.row.team_not_linked";

    public const string RowFieldNotOnType = "imports.row.field_not_on_type";

    public const string RowDateOutOfRange = "imports.row.date_out_of_range";

    public const string RowRegexTimeout = "imports.row.regex_timeout";

    public const string RowIdentifierNotFound = "imports.row.identifier_not_found";

    public const string RowIdentifierDeleted = "imports.row.identifier_deleted";

    public const string RowIdentifierSkipped = "imports.row.identifier_skipped";

    public const string RowIdentifierDuplicate = "imports.row.identifier_duplicate";

    public const string RowProjectChanged = "imports.row.project_changed";

    public const string RowTypeChanged = "imports.row.type_changed";

    public const string RowValueInvalid = "imports.row.value_invalid";

    public const string RowValueTruncated = "imports.row.value_truncated";

    public const string RowRequiredMissing = "imports.row.required_missing";

    public const string NoteNoIdentifierColumn = "imports.dry_run.no_identifier_column";

    public const string NoteEmptyFile = "imports.dry_run.empty_file";
}
