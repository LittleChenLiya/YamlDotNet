// This file is part of YamlDotNet - A .NET library for YAML.
// Copyright (c) Antoine Aubry and contributors
//
// Permission is hereby granted, free of charge, to any person obtaining a copy of
// this software and associated documentation files (the "Software"), to deal in
// the Software without restriction, including without limitation the rights to
// use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies
// of the Software, and to permit persons to whom the Software is furnished to do
// so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System;
using System.Collections.Generic;
using Xunit;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

#nullable enable

namespace YamlDotNet.Test.Serialization;

public class ReferenceBudgetTests
{
    private sealed class SharedItem
    {
        public string Name { get; set; } = string.Empty;
    }

    private sealed class SharedHolder
    {
        public SharedItem? First { get; set; }
        public SharedItem? Second { get; set; }
    }

    private sealed class SelfReferential
    {
        public string Name { get; set; } = string.Empty;
        public SelfReferential? Self { get; set; }
    }

    // Constructs exactly 3 nodes: the root mapping, the "first" mapping and the "name" scalar.
    // The "second" property is an alias and must not be counted as a construction.
    private const string SharedAliasYaml = """
        First: &shared
          Name: test
        Second: *shared
        """;

    // Three nested anchored nodes: &a (depth 1), &b (depth 2), &c (depth 3).
    private const string ThreeLevelAliasChain = """
        &a
        - &b
          - &c
            - leaf
        """;

    // Constructs exactly 2 nodes: the root mapping and the "name" scalar.
    // The "self" property is an alias to the node that is still being constructed.
    private const string SelfReferentialYaml = """
        &root
        Name: me
        Self: *root
        """;

    // Constructs exactly 4 nodes: the sequence and its three scalar items.
    private const string ThreeItemSequence = """
        - a
        - b
        - c
        """;

    [Fact]
    public void Shared_Alias_Preserves_Object_Identity_And_Is_Not_Counted_As_A_Construction()
    {
        var sut = new DeserializerBuilder()
            .WithReferenceBudget(new ReferenceBudget(maxConstructedNodes: 3))
            .Build();

        var result = sut.Deserialize<SharedHolder>(SharedAliasYaml);

        Assert.NotNull(result);
        Assert.NotNull(result.First);
        Assert.Equal("test", result.First!.Name);
        Assert.Same(result.First, result.Second);
    }

    [Fact]
    public void Shared_Alias_Would_Exceed_The_Node_Budget_If_It_Were_Counted_As_A_Construction()
    {
        // If the alias were counted as a construction, 4 nodes would be needed and a budget of 3 would fail.
        // Since the alias is not counted, only a budget of 2 (one less than the 3 real nodes) fails.
        var sut = new DeserializerBuilder()
            .WithReferenceBudget(new ReferenceBudget(maxConstructedNodes: 2))
            .Build();

        var ex = Assert.Throws<ReferenceBudgetExceededException>(() => sut.Deserialize<SharedHolder>(SharedAliasYaml));

        Assert.Equal(ReferenceBudgetCategory.ConstructedNodes, ex.Category);
        Assert.Equal(2, ex.Limit);
    }

    [Fact]
    public void Alias_Resolution_Depth_Budget_Rejects_The_Third_Level_Of_A_Three_Level_Alias_Chain()
    {
        var sut = new DeserializerBuilder()
            .WithReferenceBudget(new ReferenceBudget(maxAliasResolutionDepth: 2))
            .Build();

        var ex = Assert.Throws<ReferenceBudgetExceededException>(() => sut.Deserialize<List<List<List<string>>>>(ThreeLevelAliasChain));

        Assert.Equal(ReferenceBudgetCategory.AliasResolutionDepth, ex.Category);
        Assert.Equal(2, ex.Limit);
        Assert.Equal(3, ex.Start.Line); // The "&c" anchored node starts on line 3.
    }

    [Fact]
    public void Alias_Resolution_Depth_Budget_Accepts_A_Chain_That_Exactly_Reaches_The_Limit()
    {
        var sut = new DeserializerBuilder()
            .WithReferenceBudget(new ReferenceBudget(maxAliasResolutionDepth: 3))
            .Build();

        var result = sut.Deserialize<List<List<List<string>>>>(ThreeLevelAliasChain);

        Assert.Equal(new List<List<List<string>>> { new() { new() { "leaf" } } }, result);
    }

    [Fact]
    public void Same_Deserializer_Can_Deserialize_Valid_Input_After_A_Budget_Was_Exceeded()
    {
        var sut = new DeserializerBuilder()
            .WithReferenceBudget(new ReferenceBudget(maxAliasResolutionDepth: 2))
            .Build();

        Assert.Throws<ReferenceBudgetExceededException>(() => sut.Deserialize<List<List<List<string>>>>(ThreeLevelAliasChain));

        var result = sut.Deserialize<List<string>>("- a\n- b");

        Assert.Equal(new List<string> { "a", "b" }, result);
    }

    [Fact]
    public void Self_Reference_Through_An_Alias_Is_Supported_Within_The_Budget()
    {
        var sut = new DeserializerBuilder()
            .WithReferenceBudget(new ReferenceBudget(maxConstructedNodes: 2, maxAliasResolutionDepth: 1))
            .Build();

        var result = sut.Deserialize<SelfReferential>(SelfReferentialYaml);

        Assert.NotNull(result);
        Assert.Equal("me", result.Name);
        Assert.Same(result, result.Self);
    }

    [Fact]
    public void Node_Budget_Accepts_A_Document_That_Exactly_Reaches_The_Limit()
    {
        var sut = new DeserializerBuilder()
            .WithReferenceBudget(new ReferenceBudget(maxConstructedNodes: 4))
            .Build();

        var result = sut.Deserialize<List<string>>(ThreeItemSequence);

        Assert.Equal(new List<string> { "a", "b", "c" }, result);
    }

    [Fact]
    public void Node_Budget_Rejects_A_Document_One_Node_Below_The_Required_Count()
    {
        var sut = new DeserializerBuilder()
            .WithReferenceBudget(new ReferenceBudget(maxConstructedNodes: 3))
            .Build();

        var ex = Assert.Throws<ReferenceBudgetExceededException>(() => sut.Deserialize<List<string>>(ThreeItemSequence));

        Assert.Equal(ReferenceBudgetCategory.ConstructedNodes, ex.Category);
        Assert.Equal(3, ex.Limit);
        Assert.True(ex.Start.Line > 0);
    }

    [Fact]
    public void Deserializer_Without_A_Budget_Keeps_The_Default_Unlimited_Behavior()
    {
        var sut = new DeserializerBuilder().Build();

        var chain = sut.Deserialize<List<List<List<string>>>>(ThreeLevelAliasChain);
        Assert.Equal(new List<List<List<string>>> { new() { new() { "leaf" } } }, chain);

        var shared = sut.Deserialize<SharedHolder>(SharedAliasYaml);
        Assert.Same(shared!.First, shared.Second);
    }

    [Fact]
    public void Budget_Requires_At_Least_One_Positive_Limit()
    {
        Assert.Throws<ArgumentException>(() => new ReferenceBudget());
        Assert.Throws<ArgumentOutOfRangeException>(() => new ReferenceBudget(maxConstructedNodes: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ReferenceBudget(maxAliasResolutionDepth: -1));
        Assert.Throws<ArgumentNullException>(() => new DeserializerBuilder().WithReferenceBudget(null!));
    }
}
