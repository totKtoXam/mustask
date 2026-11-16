using Mustask.Domain.Imports;
using Xunit;

namespace Mustask.Domain.Tests.Imports;

public sealed class ImportFileNameTests
{
    [Fact]
    public void FitName_ShortName_Unchanged() => Assert.Equal("реестр.csv", ImportFile.FitName("реестр.csv"));

    [Fact]
    public void FitName_LongName_CutTo260_KeepingTheExtension()
    {
        var name = ImportFile.FitName(new string('я', 400) + ".xlsx");

        Assert.Equal(260, name.Length);
        Assert.EndsWith(".xlsx", name);
    }

    [Fact]
    public void FitName_CutWouldSplitASurrogatePair_CutsOneEarlier()
    {
        var name = ImportFile.FitName("a" + string.Concat(Enumerable.Repeat("\U0001F600", 200)) + ".csv");

        Assert.EndsWith(".csv", name);
        Assert.True(name.Length <= 260);
        for (var i = 0; i < name.Length; i++)
        {
            if (char.IsHighSurrogate(name[i]))
            {
                Assert.True(char.IsLowSurrogate(name[i + 1]));
                i++;
            }
            else
            {
                Assert.False(char.IsLowSurrogate(name[i]));
            }
        }
    }

    [Fact]
    public void FitName_LongNameWithoutExtension_CutTo260() =>
        Assert.Equal(260, ImportFile.FitName(new string('a', 500)).Length);
}
