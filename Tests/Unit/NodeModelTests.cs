using BankProductImpact.Domain.Common;
using BankProductImpact.Domain.Nodes;
using Xunit;

namespace BankProductImpact.Tests.Unit;

/// <summary>
/// Ortak node modelinin domain kuralları (teknik doküman 4): parent = child olamaz;
/// bir node hem parent hem child olabilir; aynı child birden fazla parent'a bağlanabilir.
/// </summary>
public sealed class NodeModelTests
{
    private static NodeRelation Relate(string parent, string child) =>
        new(new NodeRelationId($"{parent}-{child}"), new NodeId(parent), new NodeId(child));

    [Fact] // Parent'ın kendisine child olarak eklenememesi
    public void NodeRelation_ParentEsitChildOlamaz()
    {
        Assert.Throws<ArgumentException>(() => Relate("A", "A"));
    }

    [Fact] // Bir node hem parent hem child olabilir
    public void Node_HemParentHemChildOlabilir()
    {
        // X, A'nın child'ı; aynı X, B'nin parent'ı.
        var asChild = Relate("A", "X");
        var asParent = Relate("X", "B");

        Assert.Equal(new NodeId("X"), asChild.ChildNodeId);
        Assert.Equal(new NodeId("X"), asParent.ParentNodeId);
    }

    [Fact] // Aynı child birden fazla parent'a bağlanabilir
    public void AyniChild_BirdenFazlaParentaBaglanabilir()
    {
        var underA = Relate("A", "Y");
        var underB = Relate("B", "Y");

        Assert.Equal(new NodeId("Y"), underA.ChildNodeId);
        Assert.Equal(new NodeId("Y"), underB.ChildNodeId);
        Assert.NotEqual(underA.ParentNodeId, underB.ParentNodeId);
    }

    [Fact] // Ağaçtan çıkarma (pasife alma) node kaydını silmez; Node ayrı bir varlıktır
    public void Node_BaglantidanBagimsizdir()
    {
        var node = new Node(new NodeId("Z"), "Örnek", canBeRoot: true);
        // Bir ilişki pasifleştirilse bile Node nesnesi etkilenmez (ayrı model).
        var relation = new NodeRelation(new NodeRelationId("r"), new NodeId("root"), node.Id, isActive: false);

        Assert.False(relation.IsActive);
        Assert.Equal("Örnek", node.Name);
        Assert.True(node.IsActive);
    }
}
