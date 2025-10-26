using Xunit;
using MDTool.Core;
using MDTool.Models;

namespace MDTool.Tests.Core;

public class ConditionalEvaluatorTests
{
    // Helper to create a simple args accessor
    private static IArgsAccessor CreateArgs(Dictionary<string, object> args)
    {
        return new ArgsJsonAccessor(args);
    }

    #region Expression Parsing and Evaluation

    [Fact]
    public void Evaluate_SimpleEquality_True()
    {
        var content = "{{#if ROLE == 'TEST'}}\nIncluded\n{{/if}}";
        var args = CreateArgs(new Dictionary<string, object> { ["ROLE"] = "TEST" });

        var result = ConditionalEvaluator.Evaluate(content, args);

        Assert.True(result.Success);
        Assert.Contains("Included", result.Value);
        Assert.DoesNotContain("{{#if", result.Value);
    }

    [Fact]
    public void Evaluate_SimpleEquality_False()
    {
        var content = "{{#if ROLE == 'TEST'}}\nExcluded\n{{/if}}";
        var args = CreateArgs(new Dictionary<string, object> { ["ROLE"] = "PROD" });

        var result = ConditionalEvaluator.Evaluate(content, args);

        Assert.True(result.Success);
        Assert.DoesNotContain("Excluded", result.Value);
    }

    [Fact]
    public void Evaluate_NotEquals_Works()
    {
        var content = "{{#if ROLE != 'TEST'}}\nIncluded\n{{/if}}";
        var args = CreateArgs(new Dictionary<string, object> { ["ROLE"] = "PROD" });

        var result = ConditionalEvaluator.Evaluate(content, args);

        Assert.True(result.Success);
        Assert.Contains("Included", result.Value);
    }

    [Fact]
    public void Evaluate_LogicalAnd_True()
    {
        var content = "{{#if ROLE == 'TEST' && ENABLED == true}}\nIncluded\n{{/if}}";
        var args = CreateArgs(new Dictionary<string, object> { ["ROLE"] = "TEST", ["ENABLED"] = true });

        var result = ConditionalEvaluator.Evaluate(content, args);

        Assert.True(result.Success);
        Assert.Contains("Included", result.Value);
    }

    [Fact]
    public void Evaluate_LogicalAnd_False()
    {
        var content = "{{#if ROLE == 'TEST' && ENABLED == true}}\nExcluded\n{{/if}}";
        var args = CreateArgs(new Dictionary<string, object> { ["ROLE"] = "TEST", ["ENABLED"] = false });

        var result = ConditionalEvaluator.Evaluate(content, args);

        Assert.True(result.Success);
        Assert.DoesNotContain("Excluded", result.Value);
    }

    [Fact]
    public void Evaluate_LogicalOr_True()
    {
        var content = "{{#if ROLE == 'TEST' || ROLE == 'REPORT'}}\nIncluded\n{{/if}}";
        var args = CreateArgs(new Dictionary<string, object> { ["ROLE"] = "REPORT" });

        var result = ConditionalEvaluator.Evaluate(content, args);

        Assert.True(result.Success);
        Assert.Contains("Included", result.Value);
    }

    [Fact]
    public void Evaluate_LogicalNot_True()
    {
        var content = "{{#if !DEBUG}}\nIncluded\n{{/if}}";
        var args = CreateArgs(new Dictionary<string, object> { ["DEBUG"] = false });

        var result = ConditionalEvaluator.Evaluate(content, args);

        Assert.True(result.Success);
        Assert.Contains("Included", result.Value);
    }

    [Fact]
    public void Evaluate_Parentheses_CorrectPrecedence()
    {
        var content = "{{#if (ROLE == 'TEST' || ROLE == 'REPORT') && ENABLED == true}}\nIncluded\n{{/if}}";
        var args = CreateArgs(new Dictionary<string, object> { ["ROLE"] = "REPORT", ["ENABLED"] = true });

        var result = ConditionalEvaluator.Evaluate(content, args);

        Assert.True(result.Success);
        Assert.Contains("Included", result.Value);
    }

    [Fact]
    public void Evaluate_ContainsFunction_CaseInsensitive()
    {
        var content = "{{#if contains(ROLE, 'test')}}\nIncluded\n{{/if}}";
        var args = CreateArgs(new Dictionary<string, object> { ["ROLE"] = "TEST_ENV" });

        var result = ConditionalEvaluator.Evaluate(content, args);

        Assert.True(result.Success);
        Assert.Contains("Included", result.Value);
    }

    [Fact]
    public void Evaluate_StartsWithFunction_Works()
    {
        var content = "{{#if startsWith(AGENT, 'QA')}}\nIncluded\n{{/if}}";
        var args = CreateArgs(new Dictionary<string, object> { ["AGENT"] = "QA-Agent-1" });

        var result = ConditionalEvaluator.Evaluate(content, args);

        Assert.True(result.Success);
        Assert.Contains("Included", result.Value);
    }

    [Fact]
    public void Evaluate_EndsWithFunction_Works()
    {
        var content = "{{#if endsWith(FILE, '.md')}}\nIncluded\n{{/if}}";
        var args = CreateArgs(new Dictionary<string, object> { ["FILE"] = "README.md" });

        var result = ConditionalEvaluator.Evaluate(content, args);

        Assert.True(result.Success);
        Assert.Contains("Included", result.Value);
    }

    [Fact]
    public void Evaluate_InFunction_WithArray()
    {
        var content = "{{#if in(ROLE, ['TEST', 'REPORT', 'DEV'])}}\nIncluded\n{{/if}}";
        var args = CreateArgs(new Dictionary<string, object> { ["ROLE"] = "REPORT" });

        var result = ConditionalEvaluator.Evaluate(content, args);

        Assert.True(result.Success);
        Assert.Contains("Included", result.Value);
    }

    [Fact]
    public void Evaluate_MethodCallSyntax_Contains()
    {
        var content = "{{#if ROLE.Contains('TEST')}}\nIncluded\n{{/if}}";
        var args = CreateArgs(new Dictionary<string, object> { ["ROLE"] = "TEST_ENV" });

        var result = ConditionalEvaluator.Evaluate(content, args);

        Assert.True(result.Success);
        Assert.Contains("Included", result.Value);
    }

    [Fact]
    public void Evaluate_NumberComparison_Works()
    {
        var content = "{{#if COUNT == 5}}\nIncluded\n{{/if}}";
        var args = CreateArgs(new Dictionary<string, object> { ["COUNT"] = 5L });

        var result = ConditionalEvaluator.Evaluate(content, args);

        Assert.True(result.Success);
        Assert.Contains("Included", result.Value);
    }

    [Fact]
    public void Evaluate_BooleanComparison_Works()
    {
        var content = "{{#if ENABLED == true}}\nIncluded\n{{/if}}";
        var args = CreateArgs(new Dictionary<string, object> { ["ENABLED"] = true });

        var result = ConditionalEvaluator.Evaluate(content, args);

        Assert.True(result.Success);
        Assert.Contains("Included", result.Value);
    }

    #endregion

    #region Tag Matching and Structure

    [Fact]
    public void Evaluate_ElseIfBranch_TakesCorrectBranch()
    {
        var content = @"{{#if ROLE == 'TEST'}}
Test content
{{else if ROLE == 'REPORT'}}
Report content
{{/if}}";
        var args = CreateArgs(new Dictionary<string, object> { ["ROLE"] = "REPORT" });

        var result = ConditionalEvaluator.Evaluate(content, args);

        Assert.True(result.Success);
        Assert.Contains("Report content", result.Value);
        Assert.DoesNotContain("Test content", result.Value);
    }

    [Fact]
    public void Evaluate_ElseBranch_TakesWhenNoneMatch()
    {
        var content = @"{{#if ROLE == 'TEST'}}
Test content
{{else if ROLE == 'REPORT'}}
Report content
{{else}}
Default content
{{/if}}";
        var args = CreateArgs(new Dictionary<string, object> { ["ROLE"] = "OTHER" });

        var result = ConditionalEvaluator.Evaluate(content, args);

        Assert.True(result.Success);
        Assert.Contains("Default content", result.Value);
        Assert.DoesNotContain("Test content", result.Value);
        Assert.DoesNotContain("Report content", result.Value);
    }

    [Fact]
    public void Evaluate_NestedConditionals_WorksCorrectly()
    {
        var content = @"{{#if OUTER == 'true'}}
Outer content
{{#if INNER == 'true'}}
Inner content
{{/if}}
{{/if}}";
        var args = CreateArgs(new Dictionary<string, object> { ["OUTER"] = "true", ["INNER"] = "true" });

        var result = ConditionalEvaluator.Evaluate(content, args);

        Assert.True(result.Success);
        Assert.Contains("Outer content", result.Value);
        Assert.Contains("Inner content", result.Value);
    }

    [Fact]
    public void Evaluate_MismatchedTags_ReturnsError()
    {
        var content = "{{#if ROLE == 'TEST'}}\nContent\n{{/else}}";
        var args = CreateArgs(new Dictionary<string, object> { ["ROLE"] = "TEST" });

        var result = ConditionalEvaluator.Evaluate(content, args);

        Assert.False(result.Success);
    }

    [Fact]
    public void Evaluate_UnclosedTag_ReturnsError()
    {
        var content = "{{#if ROLE == 'TEST'}}\nContent";
        var args = CreateArgs(new Dictionary<string, object> { ["ROLE"] = "TEST" });

        var result = ConditionalEvaluator.Evaluate(content, args);

        Assert.False(result.Success);
    }

    [Fact]
    public void Evaluate_ExtraClosingTag_ReturnsError()
    {
        var content = "Content\n{{/if}}";
        var args = CreateArgs(new Dictionary<string, object> { ["ROLE"] = "TEST" });

        var result = ConditionalEvaluator.Evaluate(content, args);

        Assert.False(result.Success);
    }

    #endregion

    #region Code Fence Protection

    [Fact]
    public void Evaluate_CodeFenceWithTags_IgnoresTags()
    {
        var content = @"Some content
```
{{#if ROLE == 'TEST'}}
This should be ignored
{{/if}}
```
More content";
        var args = CreateArgs(new Dictionary<string, object> { ["ROLE"] = "TEST" });

        var result = ConditionalEvaluator.Evaluate(content, args);

        Assert.True(result.Success);
        Assert.Contains("{{#if ROLE == 'TEST'}}", result.Value);
        Assert.Contains("This should be ignored", result.Value);
    }

    [Fact]
    public void Evaluate_TildaCodeFence_IgnoresTags()
    {
        var content = @"~~~
{{#if TEST}}
Ignored
{{/if}}
~~~";
        var args = CreateArgs(new Dictionary<string, object> { ["TEST"] = true });

        var result = ConditionalEvaluator.Evaluate(content, args);

        Assert.True(result.Success);
        Assert.Contains("{{#if TEST}}", result.Value);
    }

    #endregion

    #region Unknown Variables and Strict Mode

    [Fact]
    public void Evaluate_UnknownVariable_NonStrict_EvaluatesFalse()
    {
        var content = "{{#if UNKNOWN_VAR == 'TEST'}}\nExcluded\n{{/if}}\nIncluded";
        var args = CreateArgs(new Dictionary<string, object>());

        var result = ConditionalEvaluator.Evaluate(content, args);

        Assert.True(result.Success);
        Assert.Contains("Included", result.Value);
        Assert.DoesNotContain("Excluded", result.Value);
    }

    [Fact]
    public void Evaluate_UnknownVariable_Strict_ReturnsError()
    {
        var content = "{{#if UNKNOWN_VAR == 'TEST'}}\nContent\n{{/if}}";
        var args = CreateArgs(new Dictionary<string, object>());
        var options = new ConditionalOptions(Strict: true);

        var result = ConditionalEvaluator.Evaluate(content, args, options);

        Assert.False(result.Success);
    }

    #endregion

    #region Case Sensitivity

    [Fact]
    public void Evaluate_CaseInsensitiveStrings_Default()
    {
        var content = "{{#if ROLE == 'test'}}\nIncluded\n{{/if}}";
        var args = CreateArgs(new Dictionary<string, object> { ["ROLE"] = "TEST" });

        var result = ConditionalEvaluator.Evaluate(content, args);

        Assert.True(result.Success);
        Assert.Contains("Included", result.Value);
    }

    [Fact]
    public void Evaluate_CaseSensitiveStrings_Option()
    {
        var content = "{{#if ROLE == 'test'}}\nExcluded\n{{/if}}\nIncluded";
        var args = CreateArgs(new Dictionary<string, object> { ["ROLE"] = "TEST" });
        var options = new ConditionalOptions(CaseSensitiveStrings: true);

        var result = ConditionalEvaluator.Evaluate(content, args, options);

        Assert.True(result.Success);
        Assert.Contains("Included", result.Value);
        Assert.DoesNotContain("Excluded", result.Value);
    }

    [Fact]
    public void Evaluate_VariableLookup_CaseInsensitive()
    {
        var content = "{{#if role == 'TEST'}}\nIncluded\n{{/if}}";
        var args = CreateArgs(new Dictionary<string, object> { ["ROLE"] = "TEST" });

        var result = ConditionalEvaluator.Evaluate(content, args);

        Assert.True(result.Success);
        Assert.Contains("Included", result.Value);
    }

    #endregion

    #region Type Awareness

    [Fact]
    public void Evaluate_TypeMismatch_NonStrict_ReturnsFalse()
    {
        var content = "{{#if COUNT == 'five'}}\nExcluded\n{{/if}}\nIncluded";
        var args = CreateArgs(new Dictionary<string, object> { ["COUNT"] = 5L });

        var result = ConditionalEvaluator.Evaluate(content, args);

        Assert.True(result.Success);
        Assert.Contains("Included", result.Value);
        Assert.DoesNotContain("Excluded", result.Value);
    }

    [Fact]
    public void Evaluate_TypeMismatch_Strict_ReturnsError()
    {
        var content = "{{#if COUNT == 'five'}}\nContent\n{{/if}}";
        var args = CreateArgs(new Dictionary<string, object> { ["COUNT"] = 5L });
        var options = new ConditionalOptions(Strict: true);

        var result = ConditionalEvaluator.Evaluate(content, args, options);

        Assert.False(result.Success);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void Evaluate_MultipleBlocks_ProcessesIndependently()
    {
        var content = @"{{#if A == 'true'}}
A content
{{/if}}
Middle content
{{#if B == 'true'}}
B content
{{/if}}";
        var args = CreateArgs(new Dictionary<string, object> { ["A"] = "true", ["B"] = "false" });

        var result = ConditionalEvaluator.Evaluate(content, args);

        Assert.True(result.Success);
        Assert.Contains("A content", result.Value);
        Assert.Contains("Middle content", result.Value);
        Assert.DoesNotContain("B content", result.Value);
    }

    [Fact]
    public void Evaluate_EmptyBranch_HandlesCorrectly()
    {
        var content = @"{{#if ROLE == 'TEST'}}
{{else}}
Content
{{/if}}";
        var args = CreateArgs(new Dictionary<string, object> { ["ROLE"] = "PROD" });

        var result = ConditionalEvaluator.Evaluate(content, args);

        Assert.True(result.Success);
        Assert.Contains("Content", result.Value);
    }

    [Fact]
    public void Evaluate_ComplexExpression_WithMultipleOperators()
    {
        var content = "{{#if (ROLE == 'TEST' || ROLE == 'REPORT') && !DEBUG}}\nIncluded\n{{/if}}";
        var args = CreateArgs(new Dictionary<string, object>
        {
            ["ROLE"] = "TEST",
            ["DEBUG"] = false
        });

        var result = ConditionalEvaluator.Evaluate(content, args);

        Assert.True(result.Success);
        Assert.Contains("Included", result.Value);
    }

    [Fact]
    public void Evaluate_DotNotation_NestedProperty()
    {
        var args = CreateArgs(new Dictionary<string, object>
        {
            ["USER"] = new Dictionary<string, object>
            {
                ["NAME"] = "Alice"
            }
        });

        var content = "{{#if USER.NAME == 'Alice'}}\nIncluded\n{{/if}}";

        var result = ConditionalEvaluator.Evaluate(content, args);

        Assert.True(result.Success);
        Assert.Contains("Included", result.Value);
    }

    [Fact]
    public void EvaluateDetailed_ReturnsTrace()
    {
        var content = @"{{#if ROLE == 'TEST'}}
Test content
{{else}}
Other content
{{/if}}";
        var args = CreateArgs(new Dictionary<string, object> { ["ROLE"] = "TEST" });

        var result = ConditionalEvaluator.EvaluateDetailed(content, args);

        Assert.True(result.Success);
        Assert.NotNull(result.Value.trace);
        Assert.Single(result.Value.trace.Blocks);
        Assert.Equal(2, result.Value.trace.Blocks[0].Branches.Count);
        Assert.True(result.Value.trace.Blocks[0].Branches[0].Taken);
        Assert.False(result.Value.trace.Blocks[0].Branches[1].Taken);
    }

    [Fact]
    public void Evaluate_MaxNesting_ExceedsLimit_ReturnsError()
    {
        var content = "";
        for (int i = 0; i < 12; i++)
        {
            content += $"{{{{#if LEVEL{i} == 'true'}}}}\n";
        }
        content += "Content\n";
        for (int i = 0; i < 12; i++)
        {
            content += "{{/if}}\n";
        }

        var args = CreateArgs(new Dictionary<string, object> { ["LEVEL0"] = "true" });
        var options = new ConditionalOptions(MaxNesting: 10);

        var result = ConditionalEvaluator.Evaluate(content, args, options);

        Assert.False(result.Success);
    }

    #endregion
}
