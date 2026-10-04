using Mustask.Application.Abstractions.UserDirectory;
using Mustask.Domain.UserDirectory;
using Mustask.Shared.Exceptions;
using static Mustask.Api.IntegrationTests.Features.UserDirectory.UserDirectoryTestData;

namespace Mustask.Api.IntegrationTests.Features.UserDirectory;

/// <summary>
/// mod-014 US-1: the read model (<see cref="IUserDirectory"/>) against a real
/// PostgreSQL — search and lookup, the order computed in SQL, the refusals of
/// api-contract.md A.6. The projection is filled directly here; the push
/// routes' own tests fill it the way the provider does.
/// </summary>
[Collection(MustaskApiCollection.Name)]
public sealed class UserDirectoryReadModelTests(MustaskApiFactory factory) : IAsyncLifetime
{
    public Task InitializeAsync() => ResetAsync(factory);

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<string> SeedOneAsync(string? first, string? last, string? email, bool enabled = true, string? username = null)
    {
        var id = NewId();
        await SeedAsync(factory, [Push(id, first, last, email, enabled, username)]);
        return id;
    }

    // US-1 "A search by part of a name finds an active user".
    [Fact]
    public async Task Search_PartOfAName_FindsTheActiveUserWithFourValues()
    {
        var id = await SeedOneAsync("Анна", "Смирнова", "anna.s@example.test", username: "a.smirnova");

        var result = await SearchAsync(factory, new UserSearch("смир"));

        var user = Assert.Single(result.Users);
        Assert.Equal(new DirectoryUser(id, "Анна Смирнова", "anna.s@example.test", true), user);
        Assert.False(result.MoreMatched);
    }

    // US-1 "A search by part of an e-mail ignores letter case".
    [Fact]
    public async Task Search_PartOfAnEmailInUpperCase_FindsTheUser()
    {
        var id = await SeedOneAsync("Анна", "Смирнова", "anna.s@example.test", username: "a.smirnova");

        var result = await SearchAsync(factory, new UserSearch("ANNA.S@"));

        Assert.Equal(id, Assert.Single(result.Users).Id);
    }

    // US-1 "The username is not searched".
    [Fact]
    public async Task Search_PartOfTheUsername_FindsNobody()
    {
        await SeedOneAsync("Анна", "Смирнова", "anna.s@example.test", username: "a.smirnova");

        var result = await SearchAsync(factory, new UserSearch("a.smirn"));

        Assert.Empty(result.Users);
    }

    // US-1 "ё and е count as the same letter".
    [Fact]
    public async Task Search_YoAndYe_AreTheSameLetter()
    {
        var semyon = await SeedOneAsync("Семён", "Орлов", null);
        var semen = await SeedOneAsync("Семен", "Лебедев", null);

        var byYe = await SearchAsync(factory, new UserSearch("семен"));
        var byYo = await SearchAsync(factory, new UserSearch("Семён"));

        string[] both = [semen, semyon];
        Assert.Equal(both.Order(StringComparer.Ordinal), byYe.Users.Select(user => user.Id).Order(StringComparer.Ordinal));
        Assert.Equal(both.Order(StringComparer.Ordinal), byYo.Users.Select(user => user.Id).Order(StringComparer.Ordinal));
    }

    // US-1 "A text shorter than 2 characters returns nobody".
    [Theory]
    [InlineData(" а ")]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Search_FewerThanTwoCharacters_AnswersNobody(string text)
    {
        await SeedAsync(factory, Enumerable.Range(0, 50).Select(i => Push(NewId(), "Анна", $"Фамилия{i}", null)));

        var result = await SearchAsync(factory, new UserSearch(text));

        Assert.Empty(result.Users);
        Assert.False(result.MoreMatched);
    }

    // US-1 "A text longer than 100 characters is refused".
    [Fact]
    public async Task Search_OverOneHundredCharacters_IsRefused_AndOneHundredIsAccepted()
    {
        await SeedAsync(factory, Enumerable.Range(0, 50).Select(i => Push(NewId(), "Анна", $"Фамилия{i}", null)));

        var tooLong = new string('а', UserDirectoryLimits.SearchTextMaxLength + 1);
        var ex = await Assert.ThrowsAsync<ValidationException>(() => SearchAsync(factory, new UserSearch($"  {tooLong}  ")));

        Assert.Equal(UserDirectoryErrorCodes.ValidationFailed, ex.Code);
        var reason = Assert.Single(ex.Errors["text"]);
        Assert.Equal(UserDirectoryErrorCodes.ValueTooLong, reason.Code);
        Assert.Equal(100m, reason.Params["limit"].Value);

        var exactly = new string('а', UserDirectoryLimits.SearchTextMaxLength);
        var accepted = await SearchAsync(factory, new UserSearch(exactly));
        Assert.Empty(accepted.Users);
    }

    // US-1 "An inactive user is not a candidate unless asked for".
    [Fact]
    public async Task Search_InactiveUser_OnlyWhenAskedFor()
    {
        var petrov = await SeedOneAsync("Иван", "Петров", null);
        var sidorov = await SeedOneAsync("Иван", "Сидоров", null, enabled: false);

        var defaults = await SearchAsync(factory, new UserSearch("иван"));
        var withInactive = await SearchAsync(factory, new UserSearch("иван", IncludeInactive: true));

        Assert.Equal([petrov], defaults.Users.Select(user => user.Id).ToArray());
        Assert.Equal([petrov, sidorov], withInactive.Users.Select(user => user.Id).ToArray());
        Assert.False(withInactive.Users.Single(user => user.Id == sidorov).IsActive);
    }

    // US-1 "A broad search is capped at 20 and says more matched".
    [Fact]
    public async Task Search_ThirtyFiveMatches_AnswersTheFirstTwentyAndMoreMatched()
    {
        var pushes = Enumerable.Range(0, 35)
            .Select(i => Push(NewId(), $"Имя{i:D2}", "Тест", $"user{i:D2}@example.test"))
            .ToList();
        await SeedAsync(factory, pushes);

        var result = await SearchAsync(factory, new UserSearch("example"));

        Assert.Equal(20, result.Users.Count);
        Assert.True(result.MoreMatched);

        var expected = pushes
            .Select(push => new DirectoryUser(push.Id, DisplayNameRule.Compose(push.FirstName, push.LastName, push.Username), push.Email!, true))
            .Order(DirectoryUserOrder.Comparer)
            .Take(20)
            .ToArray();
        Assert.Equal(expected, result.Users.ToArray());
    }

    // US-1 "The order puts Cyrillic before Latin and ignores case and ё" — the same order from SQL.
    [Fact]
    public async Task Search_OrdersCyrillicBeforeLatin_IgnoringCaseAndYo()
    {
        (string First, string Last)[] names =
        [
            ("Zoe", "Adams"), ("Alex", "Brown"), ("Юлия", "Щукина"), ("Ёлкин", "Пётр"), ("Егор", "Лось"), ("анна", "Абрамова"),
        ];
        await SeedAsync(factory, names.Select((name, i) => Push(NewId(), name.First, name.Last, $"u{i}@order.test")));

        var result = await SearchAsync(factory, new UserSearch("order.test"));

        Assert.Equal(
            ["анна Абрамова", "Егор Лось", "Ёлкин Пётр", "Юлия Щукина", "Alex Brown", "Zoe Adams"],
            result.Users.Select(user => user.DisplayName).ToArray());
    }

    // US-1 "Equal display names are ordered by id, the same way every time".
    [Fact]
    public async Task Search_TwentyFiveNamesakes_AnswersTheFirstTwentyByIdTwice()
    {
        var ids = Enumerable.Range(0, 25).Select(_ => NewId()).ToList();
        await SeedAsync(factory, ids.Select(id => Push(id, "Анна", "Смирнова", null)));

        var first = await SearchAsync(factory, new UserSearch("смирнова"));
        var second = await SearchAsync(factory, new UserSearch("смирнова"));

        var expected = ids.Order(StringComparer.Ordinal).Take(20).ToArray();
        Assert.Equal(expected, first.Users.Select(user => user.Id).ToArray());
        Assert.Equal(expected, second.Users.Select(user => user.Id).ToArray());
        Assert.True(first.MoreMatched);
        Assert.True(second.MoreMatched);
    }

    // US-1 "A search can be restricted to a given set of ids".
    [Fact]
    public async Task Search_RestrictedToIds_AnswersOnlyThose_RepeatsAndNeverHeldIdsChangeNothing()
    {
        var smirnova = await SeedOneAsync("Анна", "Смирнова", null);
        var kuznetsova = await SeedOneAsync("Анна", "Кузнецова", null);
        await SeedOneAsync("Анна", "Волкова", null);

        var restricted = await SearchAsync(factory, new UserSearch("анна", AmongIds: [smirnova, kuznetsova]));
        var withRepeatAndUnknown = await SearchAsync(factory, new UserSearch("анна", AmongIds: [smirnova, kuznetsova, smirnova, NewId()]));

        Assert.Equal(["Анна Кузнецова", "Анна Смирнова"], restricted.Users.Select(user => user.DisplayName).ToArray());
        Assert.Equal(restricted.Users, withRepeatAndUnknown.Users);
        Assert.Equal(restricted.MoreMatched, withRepeatAndUnknown.MoreMatched);
    }

    // US-1 "An empty id restriction answers nobody".
    [Fact]
    public async Task Search_EmptyIdRestriction_AnswersNobody()
    {
        await SeedOneAsync("Анна", "Смирнова", null);

        var result = await SearchAsync(factory, new UserSearch("анна", AmongIds: []));

        Assert.Empty(result.Users);
        Assert.False(result.MoreMatched);
    }

    // US-1 "A search restricted to more than 500 ids is refused".
    [Fact]
    public async Task Search_RestrictedToFiveHundredAndOneIds_IsRefused_AndFiveHundredIsAnswered()
    {
        var anna = await SeedOneAsync("Анна", "Смирнова", null);
        var ids = Enumerable.Range(0, 500).Select(_ => NewId()).Prepend(anna).ToList();

        var ex = await Assert.ThrowsAsync<ValidationException>(() => SearchAsync(factory, new UserSearch("анна", AmongIds: ids)));

        var reason = Assert.Single(ex.Errors["amongIds"]);
        Assert.Equal(UserDirectoryErrorCodes.TooManyIds, reason.Code);
        Assert.Equal(500m, reason.Params["limit"].Value);

        var answered = await SearchAsync(factory, new UserSearch("анна", AmongIds: ids.Take(500).ToList()));
        Assert.Equal(anna, Assert.Single(answered.Users).Id);
    }

    // US-1 "A user with no first or last name is shown by username".
    [Fact]
    public async Task Search_UserWithNoNames_IsShownByUsernameWithAnEmptyEmail()
    {
        var id = await SeedOneAsync(null, null, null, username: "qa.fixture");

        var result = await SearchAsync(factory, new UserSearch("qa.fix"));

        Assert.Equal(new DirectoryUser(id, "qa.fixture", string.Empty, true), Assert.Single(result.Users));
    }

    // US-1 "A lookup resolves known ids and reports unknown ones".
    [Fact]
    public async Task Lookup_ActiveDisabledDeletedAndUnknown()
    {
        var a = await SeedOneAsync("Анна", "Активная", "a@example.test");
        var b = await SeedOneAsync("Борис", "Отключённый", "b@example.test", enabled: false);
        var c = NewId();
        await SeedAsync(factory, [Push(c, "Ольга", "Белова", "o.belova@example.test", deleted: true)]);
        var d = NewId();

        var result = await LookupAsync(factory, a, b, c, d);

        Assert.True(result.Known[a].IsActive);
        Assert.Equal(new DirectoryUser(b, "Борис Отключённый", "b@example.test", false), result.Known[b]);
        Assert.Equal(new DirectoryUser(c, "Ольга Белова", "o.belova@example.test", false), result.Known[c]);
        Assert.Equal([d], result.Unknown.ToArray());
    }

    // US-1 "A lookup answers a repeated id once and an empty list with nothing".
    [Fact]
    public async Task Lookup_RepeatedIdOnce_AndEmptyListAnswersNothing()
    {
        var a = await SeedOneAsync("Анна", "Активная", null);
        var b = await SeedOneAsync("Борис", "Отключённый", null, enabled: false);

        var repeated = await LookupAsync(factory, a, a, b);
        var empty = await LookupAsync(factory);

        Assert.Equal(2, repeated.Known.Count);
        Assert.Empty(repeated.Unknown);
        Assert.Empty(empty.Known);
        Assert.Empty(empty.Unknown);
    }

    // US-1 "A lookup of more than 500 ids is refused".
    [Fact]
    public async Task Lookup_FiveHundredAndOneIds_IsRefused_AndFiveHundredIsAnswered()
    {
        var a = await SeedOneAsync("Анна", "Активная", null);
        var ids = Enumerable.Range(0, 500).Select(_ => NewId()).Prepend(a).ToArray();

        var ex = await Assert.ThrowsAsync<ValidationException>(() => LookupAsync(factory, ids));

        Assert.Equal(UserDirectoryErrorCodes.ValidationFailed, ex.Code);
        Assert.Equal(UserDirectoryErrorCodes.TooManyIds, Assert.Single(ex.Errors["ids"]).Code);

        var answered = await LookupAsync(factory, ids.Take(500).ToArray());
        Assert.Single(answered.Known);
        Assert.Equal(499, answered.Unknown.Count);
    }

    [Fact]
    public async Task Lookup_IdsAreOpaqueAndCaseSensitive()
    {
        var id = await SeedOneAsync("Анна", "Активная", null);

        var result = await LookupAsync(factory, id.ToUpperInvariant(), string.Empty, new string('x', 300));

        Assert.Empty(result.Known);
        Assert.Equal(3, result.Unknown.Count);
    }

    [Fact]
    public async Task Reads_NullArguments_AreProgrammingErrors()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => SearchAsync(factory, null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => SearchAsync(factory, new UserSearch(null!)));
        await Assert.ThrowsAsync<ArgumentNullException>(() => ReadAsync(factory, directory => directory.LookupAsync(null!, CancellationToken.None)));
    }

    // US-1 "Reads work while Keycloak is unreachable" — the realm's discovery fails, both reads answer.
    [Fact]
    public async Task Reads_KeycloakUnreachable_AnswerFromTheProjection()
    {
        var id = await SeedOneAsync("Анна", "Смирнова", null);
        factory.RealmMetadataFake.CurrentMode = KeycloakTestRealm.FakeRealmMetadata.Mode.Unavailable;

        try
        {
            var search = await SearchAsync(factory, new UserSearch("смир"));
            var lookup = await LookupAsync(factory, id);

            Assert.Equal(id, Assert.Single(search.Users).Id);
            Assert.True(lookup.Known[id].IsActive);
        }
        finally
        {
            factory.RealmMetadataFake.CurrentMode = KeycloakTestRealm.FakeRealmMetadata.Mode.Ok;
        }
    }

    [Fact]
    public async Task Search_LikeWildcardsInTheText_AreLiteral()
    {
        await SeedOneAsync("Анна", "Смирнова", "anna@example.test");
        var underscore = await SeedOneAsync("Анна", "Подчёркнутая", "anna_s@example.test");

        var result = await SearchAsync(factory, new UserSearch("a_s@"));

        Assert.Equal(underscore, Assert.Single(result.Users).Id);
        Assert.Empty((await SearchAsync(factory, new UserSearch("%%"))).Users);
    }
}
