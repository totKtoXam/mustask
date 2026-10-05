namespace Mustask.Infrastructure.Organizations;

/// <summary>
/// Display order (design § 4.6, api-contract § 0.6): by name in Russian
/// collation, letters before letter case, «Ё» with «Е», then by id. ICU's
/// Russian collation does exactly that, and PostgreSQL imports it at
/// <c>initdb</c> when built with ICU (<c>postgres:18-alpine</c> is). A server
/// without it fails the list reads with SQLSTATE 42704 (plan-backend R-3).
/// </summary>
internal static class OrganizationsCollation
{
    public const string Russian = "ru-x-icu";
}
