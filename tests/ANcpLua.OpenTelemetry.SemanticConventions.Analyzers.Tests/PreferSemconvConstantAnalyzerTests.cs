// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace OpenTelemetry.SemanticConventions.Analyzers.Tests;

public class PreferSemconvConstantAnalyzerTests
{
    private const string SemconvFixture = """
        using System.Collections.Generic;

        namespace OpenTelemetry.SemanticConventions.Attributes
        {
            public static class HttpAttributes
            {
                public const string AttributeHttpRequestMethod = "http.request.method";
                public const string AttributeHttpResponseStatusCode = "http.response.status_code";
            }

            public static class ServerAttributes
            {
                public const string AttributeServerAddress = "server.address";
                public const string AttributeServerPort = "server.port";
            }
        }

        public class FakeSpan
        {
            public FakeSpan SetTag(string key, object? value) => this;
            public FakeSpan SetBaggage(string key, string? value) => this;
        }

        public sealed class ActivitySource
        {
            public FakeSpan? StartActivity(string name, object? kind = null, object? parent = null, IEnumerable<KeyValuePair<string, object?>>? tags = null) => null;
        }

        public sealed class TagList
        {
            public void Add(string key, object? value) { }
        }

        public sealed class ResourceBuilder
        {
            public ResourceBuilder AddAttributes(IEnumerable<KeyValuePair<string, object?>> attributes) => this;
        }

        public readonly struct ActivityEvent
        {
            public ActivityEvent(string name, object? timestamp = null, IEnumerable<KeyValuePair<string, object?>>? tags = null) { }
        }

        public sealed class Counter<T>
        {
            public void Add(T delta, params KeyValuePair<string, object?>[] tags) { }
        }

        public readonly struct Measurement<T>
        {
            public Measurement(T value, params KeyValuePair<string, object?>[] tags) { }
        }
        """;

    [Fact]
    public async Task Hardcoded_HttpRequestMethod_Reports_OTSC0011()
    {
        const string testCode = SemconvFixture + """

            class Server
            {
                void Handle(FakeSpan activity)
                {
                    activity.SetTag({|#0:"http.request.method"|}, "GET");
                }
            }
            """;

        var expected = new DiagnosticResult("OTSC0011", DiagnosticSeverity.Info)
            .WithLocation(0)
            .WithArguments("http.request.method", "HttpAttributes.AttributeHttpRequestMethod");

        await new CSharpAnalyzerTest<PreferSemconvConstantAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
            ExpectedDiagnostics = { expected },
        }.RunAsync();
    }

    [Fact]
    public async Task TypedConstant_Reports_Nothing()
    {
        // Fully qualified to avoid `using` after namespace (CS1529).
        const string testCode = SemconvFixture + """

            class Server
            {
                void Handle(FakeSpan activity)
                {
                    activity.SetTag(OpenTelemetry.SemanticConventions.Attributes.HttpAttributes.AttributeHttpRequestMethod, "GET");
                }
            }
            """;

        await new CSharpAnalyzerTest<PreferSemconvConstantAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
        }.RunAsync();
    }

    [Fact]
    public async Task UnknownLiteral_Reports_Nothing()
    {
        const string testCode = SemconvFixture + """

            class Server
            {
                void Handle(FakeSpan activity)
                {
                    activity.SetTag("my.custom.tag", "value");
                }
            }
            """;

        await new CSharpAnalyzerTest<PreferSemconvConstantAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
        }.RunAsync();
    }

    [Fact]
    public async Task Multiple_Hardcoded_Reports_All()
    {
        const string testCode = SemconvFixture + """

            class Server
            {
                void Handle(FakeSpan activity)
                {
                    activity.SetTag({|#0:"server.address"|}, "0.0.0.0");
                    activity.SetTag({|#1:"server.port"|}, 8080);
                    activity.SetTag({|#2:"http.response.status_code"|}, 200);
                }
            }
            """;

        var addr = new DiagnosticResult("OTSC0011", DiagnosticSeverity.Info)
            .WithLocation(0)
            .WithArguments("server.address", "ServerAttributes.AttributeServerAddress");
        var port = new DiagnosticResult("OTSC0011", DiagnosticSeverity.Info)
            .WithLocation(1)
            .WithArguments("server.port", "ServerAttributes.AttributeServerPort");
        var code = new DiagnosticResult("OTSC0011", DiagnosticSeverity.Info)
            .WithLocation(2)
            .WithArguments("http.response.status_code", "HttpAttributes.AttributeHttpResponseStatusCode");

        await new CSharpAnalyzerTest<PreferSemconvConstantAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
            ExpectedDiagnostics = { addr, port, code },
        }.RunAsync();
    }

    [Fact]
    public async Task SetBaggage_Hardcoded_Key_Reports_OTSC0011()
    {
        const string testCode = SemconvFixture + """

            class Server
            {
                void Handle(FakeSpan activity)
                {
                    activity.SetBaggage({|#0:"http.request.method"|}, "GET");
                }
            }
            """;

        var expected = new DiagnosticResult("OTSC0011", DiagnosticSeverity.Info)
            .WithLocation(0)
            .WithArguments("http.request.method", "HttpAttributes.AttributeHttpRequestMethod");

        await new CSharpAnalyzerTest<PreferSemconvConstantAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
            ExpectedDiagnostics = { expected },
        }.RunAsync();
    }

    [Fact]
    public async Task TagList_Add_Hardcoded_Key_Reports_OTSC0011()
    {
        const string testCode = SemconvFixture + """

            class Server
            {
                void Handle(TagList tags)
                {
                    tags.Add({|#0:"server.address"|}, "localhost");
                }
            }
            """;

        var expected = new DiagnosticResult("OTSC0011", DiagnosticSeverity.Info)
            .WithLocation(0)
            .WithArguments("server.address", "ServerAttributes.AttributeServerAddress");

        await new CSharpAnalyzerTest<PreferSemconvConstantAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
            ExpectedDiagnostics = { expected },
        }.RunAsync();
    }

    [Fact]
    public async Task ResourceBuilder_AddAttributes_Hardcoded_Key_Reports_OTSC0011()
    {
        const string testCode = SemconvFixture + """

            class Server
            {
                void Handle(ResourceBuilder resourceBuilder)
                {
                    resourceBuilder.AddAttributes(new Dictionary<string, object?>
                    {
                        [{|#0:"server.address"|}] = "localhost",
                    });
                }
            }
            """;

        var expected = new DiagnosticResult("OTSC0011", DiagnosticSeverity.Info)
            .WithLocation(0)
            .WithArguments("server.address", "ServerAttributes.AttributeServerAddress");

        await new CSharpAnalyzerTest<PreferSemconvConstantAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
            ExpectedDiagnostics = { expected },
        }.RunAsync();
    }

    [Fact]
    public async Task ActivityEvent_Tags_Hardcoded_Key_Reports_OTSC0011_Once()
    {
        const string testCode = SemconvFixture + """

            class Server
            {
                ActivityEvent Create()
                {
                    return new ActivityEvent(
                        "cache.prune",
                        tags: new Dictionary<string, object?>
                        {
                            { {|#0:"server.address"|}, "localhost" },
                        });
                }
            }
            """;

        var expected = new DiagnosticResult("OTSC0011", DiagnosticSeverity.Info)
            .WithLocation(0)
            .WithArguments("server.address", "ServerAttributes.AttributeServerAddress");

        await new CSharpAnalyzerTest<PreferSemconvConstantAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
            ExpectedDiagnostics = { expected },
        }.RunAsync();
    }

    [Fact]
    public async Task ActivitySource_StartActivity_Tags_Hardcoded_Key_Reports_OTSC0011_Once()
    {
        const string testCode = SemconvFixture + """

            class Server
            {
                void Handle(ActivitySource source)
                {
                    source.StartActivity(
                        "GET /users",
                        tags: new[]
                        {
                            new KeyValuePair<string, object?>({|#0:"server.address"|}, "localhost"),
                        });
                }
            }
            """;

        var expected = new DiagnosticResult("OTSC0011", DiagnosticSeverity.Info)
            .WithLocation(0)
            .WithArguments("server.address", "ServerAttributes.AttributeServerAddress");

        await new CSharpAnalyzerTest<PreferSemconvConstantAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
            ExpectedDiagnostics = { expected },
        }.RunAsync();
    }

    [Fact]
    public async Task MetricCounter_Add_Hardcoded_Key_Reports_OTSC0011()
    {
        const string testCode = SemconvFixture + """

            class Server
            {
                void Handle(Counter<long> counter)
                {
                    counter.Add(1, new KeyValuePair<string, object?>({|#0:"server.address"|}, "localhost"));
                }
            }
            """;

        var expected = new DiagnosticResult("OTSC0011", DiagnosticSeverity.Info)
            .WithLocation(0)
            .WithArguments("server.address", "ServerAttributes.AttributeServerAddress");

        await new CSharpAnalyzerTest<PreferSemconvConstantAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
            ExpectedDiagnostics = { expected },
        }.RunAsync();
    }

    [Fact]
    public async Task Measurement_Tags_Hardcoded_Key_Reports_OTSC0011()
    {
        const string testCode = SemconvFixture + """

            class Server
            {
                Measurement<long> Create()
                {
                    return new Measurement<long>(1, new KeyValuePair<string, object?>({|#0:"server.address"|}, "localhost"));
                }
            }
            """;

        var expected = new DiagnosticResult("OTSC0011", DiagnosticSeverity.Info)
            .WithLocation(0)
            .WithArguments("server.address", "ServerAttributes.AttributeServerAddress");

        await new CSharpAnalyzerTest<PreferSemconvConstantAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
            ExpectedDiagnostics = { expected },
        }.RunAsync();
    }

    [Fact]
    public async Task NoSemconv_Reference_Compiles_And_Reports_Nothing()
    {
        // Without OpenTelemetry.SemanticConventions.Attributes types in the
        // compilation, the analyzer is a silent no-op (catalog is empty).
        const string testCode = """
            public class FakeSpan
            {
                public FakeSpan SetTag(string key, object? value) => this;
            }

            class Server
            {
                void Handle(FakeSpan activity)
                {
                    activity.SetTag("http.request.method", "GET");
                }
            }
            """;

        await new CSharpAnalyzerTest<PreferSemconvConstantAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
        }.RunAsync();
    }
}
