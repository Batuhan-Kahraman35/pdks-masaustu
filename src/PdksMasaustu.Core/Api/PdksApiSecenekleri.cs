namespace PdksMasaustu.Core.Api;

/// <summary>appsettings.json → "Api" bölümü.</summary>
public sealed class PdksApiSecenekleri
{
    public const string Bolum = "Api";

    /// <summary>Ör. https://ornek-sirket.com/pdks-api/ (sonunda / olmalı).</summary>
    public string TabanAdres { get; set; } = "";

    public TimeSpan ZamanAsimi { get; set; } = TimeSpan.FromSeconds(20);
}
