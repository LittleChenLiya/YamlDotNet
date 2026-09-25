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

#nullable enable

using System;
using System.Collections.Generic;
using FluentAssertions;
using Xunit;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.Serialization.Preflight;

namespace YamlDotNet.Test.Serialization
{
    public class PreflightValidationTests
    {
        private static int constructedCount;
        private static int setterCount;

        public abstract class Animal
        {
            private string name = string.Empty;

            protected Animal()
            {
                constructedCount++;
            }

            public string Name
            {
                get => name;
                set
                {
                    setterCount++;
                    name = value;
                }
            }
        }

        public class Cat : Animal
        {
            public int Lives { get; set; }
        }

        public class Dog : Animal
        {
            public bool Good { get; set; }
        }

        public class Owner
        {
            public Owner()
            {
                constructedCount++;
            }

            public Animal Pet { get; set; } = null!;
            public List<Animal> Pets { get; set; } = new List<Animal>();
            public Cat? FavoriteCat { get; set; }
            public Dog? Buddy { get; set; }
            public int? Age { get; set; }
        }

        public class Node
        {
            public Node()
            {
                constructedCount++;
            }

            public string Name { get; set; } = string.Empty;
            public Node? Next { get; set; }
        }

        public class CatOwner
        {
            public CatOwner()
            {
                constructedCount++;
            }

            public List<Cat> Cats { get; set; } = new List<Cat>();
        }

        public PreflightValidationTests()
        {
            constructedCount = 0;
            setterCount = 0;
        }

        private static IDeserializer CreatePreflightDeserializer(Action<PreflightValidationOptions>? configure = null)
        {
            return new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .WithTagMapping("!cat", typeof(Cat))
                .WithTagMapping("!dog", typeof(Dog))
                .WithPreflightValidation(configure)
                .Build();
        }

        [Fact]
        public void ValidTaggedGraphDeserializesSuccessfully()
        {
            var yaml = string.Join("\n",
                "pet: !cat",
                "  name: tom",
                "  lives: 9",
                "pets:",
                "  - !dog",
                "    name: rex",
                "    good: true",
                "  - !cat",
                "    name: bob",
                "favoriteCat: !cat",
                "  name: tom",
                "age: 42");

            var sut = CreatePreflightDeserializer();

            var owner = sut.Deserialize<Owner>(yaml);

            owner.Pet.Should().BeOfType<Cat>();
            ((Cat)owner.Pet).Lives.Should().Be(9);
            owner.Pets.Should().HaveCount(2);
            owner.Pets[0].Should().BeOfType<Dog>();
            owner.Pets[1].Should().BeOfType<Cat>();
            owner.FavoriteCat.Should().BeOfType<Cat>();
            owner.Age.Should().Be(42);
            constructedCount.Should().BeGreaterThan(0);
            setterCount.Should().BeGreaterThan(0);
        }

        [Fact]
        public void IllegalTagAtTheEndRejectsInputWithoutAnySideEffects()
        {
            var yaml = string.Join("\n",
                "pet: !cat",
                "  name: tom",
                "pets:",
                "  - !dog",
                "    name: rex",
                "favoriteCat: !cat",
                "  name: bob",
                "buddy: !shark",
                "  name: jaws");

            var sut = CreatePreflightDeserializer();

            var act = () => sut.Deserialize<Owner>(yaml);

            var exception = act.Should().Throw<PreflightValidationException>().Which;
            exception.Errors.Should().ContainSingle();
            exception.Errors[0].Code.Should().Be(PreflightValidationErrorCodes.UnknownTag);

            // Even though the illegal tag is at the very end of the input, not a single
            // constructor or property setter may have run.
            constructedCount.Should().Be(0);
            setterCount.Should().Be(0);
        }

        [Fact]
        public void UnknownTagIsRejected()
        {
            var yaml = string.Join("\n",
                "pet: !shark",
                "  name: jaws");

            var sut = CreatePreflightDeserializer();

            var act = () => sut.Deserialize<Owner>(yaml);

            var exception = act.Should().Throw<PreflightValidationException>().Which;
            exception.Errors[0].Code.Should().Be(PreflightValidationErrorCodes.UnknownTag);
            exception.Errors[0].Message.Should().Contain("!shark");
            constructedCount.Should().Be(0);
        }

        [Fact]
        public void AmbiguousPolymorphicBranchWithoutTagIsRejected()
        {
            var yaml = string.Join("\n",
                "pet:",
                "  name: tom");

            var sut = CreatePreflightDeserializer();

            var act = () => sut.Deserialize<Owner>(yaml);

            var exception = act.Should().Throw<PreflightValidationException>().Which;
            exception.Errors[0].Code.Should().Be(PreflightValidationErrorCodes.AmbiguousPolymorphicType);
            constructedCount.Should().Be(0);
        }

        [Fact]
        public void CompatibleAliasIsAcceptedAtEveryUsePosition()
        {
            var yaml = string.Join("\n",
                "pet: !cat &c",
                "  name: tom",
                "favoriteCat: *c");

            var sut = CreatePreflightDeserializer();

            var owner = sut.Deserialize<Owner>(yaml);

            owner.Pet.Should().BeOfType<Cat>();
            owner.FavoriteCat.Should().BeSameAs(owner.Pet);
        }

        [Fact]
        public void IncompatibleAliasReportsBothTheAliasAndAnchorMarks()
        {
            var yaml = string.Join("\n",
                "pet: !cat &c",
                "  name: tom",
                "buddy: *c");

            var sut = CreatePreflightDeserializer();

            var act = () => sut.Deserialize<Owner>(yaml);

            var exception = act.Should().Throw<PreflightValidationException>().Which;
            var error = exception.Errors.Should().ContainSingle().Which;
            error.Code.Should().Be(PreflightValidationErrorCodes.IncompatibleAlias);

            // The primary mark points at the alias use position...
            error.Start.Line.Should().Be(3);
            // ...and the related mark points at the anchor definition.
            error.RelatedStart.Line.Should().Be(1);
            error.RelatedMessage.Should().NotBeNullOrEmpty();

            constructedCount.Should().Be(0);
            setterCount.Should().Be(0);
        }

        [Fact]
        public void SameAnchorReferencedFromTwoIncompatiblePositionsReportsBothMarks()
        {
            var yaml = string.Join("\n",
                "buddy: !dog &d",
                "  name: rex",
                "pet: !cat &c",
                "  name: tom",
                "favoriteCat: *d");

            var sut = CreatePreflightDeserializer();

            var act = () => sut.Deserialize<Owner>(yaml);

            var exception = act.Should().Throw<PreflightValidationException>().Which;
            var error = exception.Errors.Should().ContainSingle().Which;
            error.Code.Should().Be(PreflightValidationErrorCodes.IncompatibleAlias);
            error.Start.Line.Should().Be(5);
            error.RelatedStart.Line.Should().Be(1);
            constructedCount.Should().Be(0);
        }

        [Fact]
        public void CircularReferencesAreAllowed()
        {
            var yaml = string.Join("\n",
                "&a",
                "name: root",
                "next: *a");

            var sut = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .WithPreflightValidation()
                .Build();

            var node = sut.Deserialize<Node>(yaml);

            node.Name.Should().Be("root");
            node.Next.Should().BeSameAs(node);
        }

        [Fact]
        public void NullablePropertiesAcceptValuesAndNulls()
        {
            var yaml = string.Join("\n",
                "pet: !cat",
                "  name: tom",
                "favoriteCat: ~",
                "age: ~");

            var sut = CreatePreflightDeserializer();

            var owner = sut.Deserialize<Owner>(yaml);

            owner.Age.Should().BeNull();
            owner.FavoriteCat.Should().BeNull();
            owner.Pet.Should().BeOfType<Cat>();
        }

        [Fact]
        public void CollectionElementTypesAreChecked()
        {
            var yaml = string.Join("\n",
                "cats:",
                "  - !cat",
                "    name: tom",
                "  - !dog",
                "    name: rex");

            var sut = CreatePreflightDeserializer();

            var act = () => sut.Deserialize<CatOwner>(yaml);

            var exception = act.Should().Throw<PreflightValidationException>().Which;
            exception.Errors[0].Code.Should().Be(PreflightValidationErrorCodes.IncompatibleTaggedType);
            constructedCount.Should().Be(0);
        }

        [Fact]
        public void UntaggedElementsOfConcreteCollectionElementTypeAreAllowed()
        {
            var yaml = string.Join("\n",
                "cats:",
                "  - name: tom",
                "  - !cat",
                "    name: bob");

            var sut = CreatePreflightDeserializer();

            var owner = sut.Deserialize<CatOwner>(yaml);

            owner.Cats.Should().HaveCount(2);
            owner.Cats.Should().OnlyContain(c => c is Cat);
        }

        [Fact]
        public void DuplicateAnchorsAreRejected()
        {
            var yaml = string.Join("\n",
                "pet: !cat &c",
                "  name: tom",
                "favoriteCat: !cat &c",
                "  name: bob");

            var sut = CreatePreflightDeserializer();

            var act = () => sut.Deserialize<Owner>(yaml);

            var exception = act.Should().Throw<PreflightValidationException>().Which;
            var error = exception.Errors.Should().ContainSingle().Which;
            error.Code.Should().Be(PreflightValidationErrorCodes.DuplicateAnchor);
            error.Start.Line.Should().Be(3);
            error.RelatedStart.Line.Should().Be(1);
            constructedCount.Should().Be(0);
        }

        [Fact]
        public void UnresolvableAliasIsRejected()
        {
            var yaml = string.Join("\n",
                "pet: !cat",
                "  name: tom",
                "favoriteCat: *missing");

            var sut = CreatePreflightDeserializer();

            var act = () => sut.Deserialize<Owner>(yaml);

            var exception = act.Should().Throw<PreflightValidationException>().Which;
            exception.Errors[0].Code.Should().Be(PreflightValidationErrorCodes.UnresolvableAlias);
            constructedCount.Should().Be(0);
        }

        [Fact]
        public void BufferedEventLimitIsEnforcedBeforeAnyConstruction()
        {
            var yaml = string.Join("\n",
                "pet: !cat",
                "  name: tom",
                "  lives: 9",
                "pets:",
                "  - !dog",
                "    name: rex",
                "    good: true",
                "age: 42");

            var sut = CreatePreflightDeserializer(o => o.MaxBufferedEvents = 5);

            var act = () => sut.Deserialize<Owner>(yaml);

            var exception = act.Should().Throw<PreflightValidationException>().Which;
            exception.Errors[0].Code.Should().Be(PreflightValidationErrorCodes.EventLimitExceeded);
            constructedCount.Should().Be(0);
            setterCount.Should().Be(0);
        }

        [Fact]
        public void BufferedEventsAreReusedForDeserialization()
        {
            var yaml = string.Join("\n",
                "pet: !cat",
                "  name: tom");

            var sut = CreatePreflightDeserializer();

            var owner = sut.Deserialize<Owner>(yaml);

            owner.Pet.Name.Should().Be("tom");
            constructedCount.Should().Be(2); // Owner + Cat
        }

        [Fact]
        public void PreflightValidationCanBeDisabled()
        {
            var yaml = string.Join("\n",
                "pet: !shark",
                "  name: jaws");

            var sut = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .WithTagMapping("!cat", typeof(Cat))
                .WithPreflightValidation()
                .WithoutPreflightValidation()
                .IgnoreUnmatchedProperties()
                .Build();

            // Without preflight validation the deserializer keeps its regular behavior,
            // which in this case fails while deserializing instead of upfront.
            var act = () => sut.Deserialize<Owner>(yaml);
            act.Should().Throw<Exception>().Which.Should().NotBeOfType<PreflightValidationException>();
        }

        [Fact]
        public void InvalidOptionsAreRejected()
        {
            var builder = new DeserializerBuilder();

            var act = () => builder.WithPreflightValidation(o => o.MaxBufferedEvents = 0);

            act.Should().Throw<ArgumentOutOfRangeException>();
        }
    }
}
