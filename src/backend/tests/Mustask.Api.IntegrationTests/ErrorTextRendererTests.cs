using System.Globalization;
using Mustask.Api.ErrorHandling.Catalog;
using Mustask.Shared.Errors;

namespace Mustask.Api.IntegrationTests;

/// <summary>Подстановка params в шаблон каталога — без Docker, без хоста.</summary>
[Trait("Category", "Integration")]
public sealed class ErrorTextRendererTests
{
    [Fact]
    public void Render_GivenRussianTemplate_SubstitutesValues()
    {
        var parameters = new ErrorParams
        {
            ["used"] = ErrorParam.Number(50m),
            ["limit"] = ErrorParam.Number(50m),
        };

        var result = ErrorTextRenderer.Render("Квота исчерпана: {used} из {limit}.", parameters);

        Assert.Equal("Квота исчерпана: 50 из 50.", result.Text);
        Assert.False(result.HasMissingParams);
    }

    [Fact]
    public void Render_GivenKazakhTemplate_SubstitutesValues()
    {
        var parameters = new ErrorParams { ["country"] = ErrorParam.Text("CN") };

        var result = ErrorTextRenderer.Render("Квота {country} үшін таусылды.", parameters);

        Assert.Equal("Квота CN үшін таусылды.", result.Text);
    }

    [Fact]
    public void Render_GivenDoubledBraces_UnescapesToLiteralBrace()
    {
        var result = ErrorTextRenderer.Render("Формат: {{used}}", ErrorParams.None);

        Assert.Equal("Формат: {used}", result.Text);
        Assert.False(result.HasMissingParams);
    }

    [Fact]
    public void Render_GivenNoTemplatePlaceholders_ReturnsTextUnchanged()
    {
        var result = ErrorTextRenderer.Render("Ресурс не найден.", ErrorParams.None);

        Assert.Equal("Ресурс не найден.", result.Text);
    }

    /// <summary>
    /// Страховка: расхождение места броска с каталогом
    /// не должно ронять рендер — плейсхолдер остаётся как есть, а
    /// расхождение возвращается вызывающему для логирования.
    /// </summary>
    [Fact]
    public void Render_GivenMissingParam_LeavesPlaceholderAndReportsIt()
    {
        var result = ErrorTextRenderer.Render("Квота исчерпана: {used} из {limit}.", new ErrorParams
        {
            ["used"] = ErrorParam.Number(50m),
        });

        Assert.Equal("Квота исчерпана: 50 из {limit}.", result.Text);
        Assert.True(result.HasMissingParams);
        Assert.Equal(["limit"], result.MissingParamNames);
    }

    [Fact]
    public void Render_GivenTextParam_DoesNotAlterValue()
    {
        var result = ErrorTextRenderer.Render("Страна: {country}.", new ErrorParams
        {
            ["country"] = ErrorParam.Text("CN"),
        });

        Assert.Equal("Страна: CN.", result.Text);
    }

    [Fact]
    public void Render_GivenDateParam_FormatsWithoutLocaleDependentProcessCulture()
    {
        var originalCulture = CultureInfo.CurrentCulture;

        try
        {
            // Культура процесса намеренно другая (не совпадает с явным
            // форматом "dd.MM.yyyy"), чтобы доказать: подстановка не зависит
            // от культуры процесса вообще — ни от именованной ru-RU (это и
            // было проблемой раньше: она требует загруженных данных
            // ICU), ни от какой-либо другой.
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");

            var result = ErrorTextRenderer.Render("Дата: {date}.", new ErrorParams
            {
                ["date"] = ErrorParam.Date(new DateOnly(2026, 1, 12)),
            });

            Assert.Equal("Дата: 12.01.2026.", result.Text);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void Render_GivenCurrencyParam_HasNoGroupingSeparatorOrCurrencySymbol()
    {
        var result = ErrorTextRenderer.Render("Сумма: {amount}.", new ErrorParams
        {
            ["amount"] = ErrorParam.Money(12500m),
        });

        Assert.Equal("Сумма: 12500.", result.Text);
        Assert.DoesNotContain("₸", result.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(" ", result.Text.Replace("Сумма: ", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
    }
}
