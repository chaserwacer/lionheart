using lionheart.Model.Analysis;
using lionheart.Tests.Support;

namespace lionheart.Tests.Domain;

public class DecorationStateTests
{
    [Fact]
    public void NotDecorated_reports_empty()
    {
        Assert.False(new NotDecorated().IsDecorated);
    }

    [Fact]
    public void Decorated_reports_filled_and_carries_its_decoration()
    {
        var decorated = new Decorated(Some.Decoration);

        Assert.True(decorated.IsDecorated);
        JsonAssert.Equal(Some.Decoration, decorated.Decoration);
    }
}
