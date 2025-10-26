using Xunit;
using MDTool.Models;

namespace MDTool.Tests.Models;

public class UnitTests
{
    [Fact]
    public void Value_ShouldBeAvailable()
    {
        // Act
        var unit = Unit.Value;

        // Assert - Unit is a value type, just verify it's accessible
        Assert.Equal(default(Unit), unit);
    }

    [Fact]
    public void Value_ShouldBeSingleton()
    {
        // Act
        var unit1 = Unit.Value;
        var unit2 = Unit.Value;

        // Assert - should be the same instance
        Assert.Equal(unit1, unit2);
    }

    [Fact]
    public void DefaultValue_ShouldEqualStaticValue()
    {
        // Act
        var defaultUnit = default(Unit);
        var staticUnit = Unit.Value;

        // Assert
        Assert.Equal(defaultUnit, staticUnit);
    }

    [Fact]
    public void ProcessingResult_ShouldWorkWithUnit()
    {
        // Act
        var result = ProcessingResult<Unit>.Ok(Unit.Value);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(Unit.Value, result.Value);
    }
}
