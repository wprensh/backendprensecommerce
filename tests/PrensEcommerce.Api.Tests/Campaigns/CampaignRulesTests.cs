using PrensEcommerce.Api.Domain;
using PrensEcommerce.Api.Features.Campaigns;

namespace PrensEcommerce.Api.Tests.Campaigns;

public sealed class CampaignRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);

    private static Campaign Make(bool active = true, int? startDays = null, int? endDays = null) => new()
    {
        Name = "Prueba",
        Title = "Título",
        IsActive = active,
        StartsAt = startDays is { } s ? Now.AddDays(s) : null,
        EndsAt = endDays is { } e ? Now.AddDays(e) : null,
    };

    [Fact]
    public void Status_SinFechas_EstaVigente() => Assert.Equal(CampaignStatus.Running, Make().StatusAt(Now));

    [Fact]
    public void Status_Desactivada_GanaSobreLasFechas() =>
        Assert.Equal(CampaignStatus.Inactive, Make(active: false, startDays: -1, endDays: 1).StatusAt(Now));

    [Fact]
    public void Status_ConInicioFuturo_EstaProgramada() =>
        Assert.Equal(CampaignStatus.Scheduled, Make(startDays: 2).StatusAt(Now));

    [Fact]
    public void Status_ConFinPasado_EstaTerminada() =>
        Assert.Equal(CampaignStatus.Ended, Make(startDays: -5, endDays: -1).StatusAt(Now));

    [Fact]
    public void IsLiveAt_ElFinEsExclusivo()
    {
        var campaign = Make(startDays: -1, endDays: 0);
        Assert.False(campaign.IsLiveAt(Now));
        Assert.True(campaign.IsLiveAt(Now.AddTicks(-1)));
    }

    [Fact]
    public void Live_EligeLaDeMayorPrioridadVigente()
    {
        var low = Make(); low.Priority = 1;
        var high = Make(); high.Priority = 5;
        var future = Make(startDays: 1); future.Priority = 10;
        var off = Make(active: false); off.Priority = 20;

        var live = new[] { low, high, future, off }.AsQueryable().Live(Now).First();

        Assert.Same(high, live);
    }

    [Fact]
    public void Apply_NormalizaTextosVacios()
    {
        var campaign = Make();
        campaign.Apply(new CampaignCommand(" Black Friday ", null, " Hasta -40% ", "  ", "", null, null, " Audio ",
            CampaignTheme.Navy));

        Assert.Equal("Black Friday", campaign.Name);
        Assert.Equal(string.Empty, campaign.Eyebrow);
        Assert.Equal("Hasta -40%", campaign.Title);
        Assert.Null(campaign.Subtitle);
        Assert.Null(campaign.CtaLabel);
        Assert.Equal("audio", campaign.CategorySlug);
        Assert.Equal(CampaignTheme.Navy, campaign.Theme);
    }

    [Theory]
    [InlineData("https://cdn.prens.shop/banner.jpg", true)]
    [InlineData("/api/media/abc/campaigns/2026/11/x.webp", true)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("ftp://x/y.png", false)]
    public void Validador_SoloAceptaUrlsHttpORelativas(string url, bool valid)
    {
        var command = new CampaignCommand("Campaña", null, "Título", null, null, url, null, null);
        Assert.Equal(valid, new CampaignCommandValidator().Validate(command).IsValid);
    }

    [Fact]
    public void Validador_RechazaFinAnteriorAlInicio()
    {
        var command = new CampaignCommand("Campaña", null, "Título", null, null, null, null, null,
            StartsAt: Now, EndsAt: Now.AddDays(-1));
        Assert.False(new CampaignCommandValidator().Validate(command).IsValid);
    }
}
