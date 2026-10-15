using System.Windows.Media;
using BankProductImpact.Domain.Common;

namespace BankProductImpact.Presentation.ViewModels;

/// <summary>
/// Generik ağaç düğümlerine (metrik gruplarında olduğu gibi) otomatik, kategoriye benzer
/// renk atar. Node'un grubu olmadığından renk, kimliğin kararlı bir hash'inden seçilir;
/// böylece aynı node her zaman aynı rengi alır ve ağaç Repo kokpiti gibi renkli görünür.
/// </summary>
public static class NodeAccentPalette
{
    private static readonly Brush[] Palette = CreatePalette();

    public static Brush ForNode(NodeId id) => Palette[StableIndex(id.Value)];

    private static int StableIndex(string value)
    {
        // .NET'in çalıştırma başına değişen string hash'i yerine kararlı bir hash kullanılır.
        unchecked
        {
            var hash = 17;
            foreach (var ch in value)
                hash = hash * 31 + ch;
            return (hash & 0x7fffffff) % Palette.Length;
        }
    }

    private static Brush[] CreatePalette()
    {
        // Yalnızca temanın gerçek grup renkleri (Repo kokpitiyle birebir aynı palet).
        var colors = new[]
        {
            "#37E0C8", // turkuaz
            "#59C98F", // yeşil
            "#E8B454", // amber
            "#E56B6F", // mercan
            "#5A9BE0", // mavi
            "#B08CE8", // mor
        };

        var brushes = new Brush[colors.Length];
        for (var i = 0; i < colors.Length; i++)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colors[i]));
            brush.Freeze(); // performans + thread güvenliği
            brushes[i] = brush;
        }
        return brushes;
    }
}
