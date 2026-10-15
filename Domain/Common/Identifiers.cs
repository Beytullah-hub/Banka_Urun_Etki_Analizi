namespace BankProductImpact.Domain.Common;

// Dinamik parent–child ağacı için güçlü tipli kimlikler (bkz. teknik doküman).
// Node bir ağaçta parent, başka bir ağaçta child olabilir; bağlantı NodeRelation'da tutulur.

public readonly record struct NodeId(string Value)
{
    public override string ToString() => Value;
}

public readonly record struct NodeRelationId(string Value)
{
    public override string ToString() => Value;
}
