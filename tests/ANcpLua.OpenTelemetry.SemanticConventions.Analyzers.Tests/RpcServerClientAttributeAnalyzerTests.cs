// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

using Qyl.OpenTelemetry.SemanticConventions.Analyzers;

namespace Qyl.OpenTelemetry.SemanticConventions.Analyzers.Tests;

public class RpcServerClientAttributeAnalyzerTests
{
    // Inline fake span type with a SetTag(string, object) method — avoids needing
    // System.Diagnostics.DiagnosticSource as a test-time package reference. The
    // analyzer matches by method name, not by receiver type, so this is sufficient.
    private const string FakeSpanShim = """
        public class FakeSpan
        {
            public FakeSpan SetTag(string key, object? value) => this;
        }
        """;

    [Fact]
    public async Task RpcServer_With_ClientAddress_Reports_QYL0005()
    {
        const string testCode = FakeSpanShim + """

            class Server
            {
                void Handle(FakeSpan activity)
                {
                    activity.SetTag("rpc.system", "grpc");
                    activity.SetTag({|#0:"client.address"|}, "10.0.0.1");
                    activity.SetTag("server.address", "0.0.0.0");
                }
            }
            """;

        var expected = new DiagnosticResult("QYL0005", DiagnosticSeverity.Warning)
            .WithLocation(0)
            .WithArguments("client.address");

        await new CSharpAnalyzerTest<RpcServerClientAttributeAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
            ExpectedDiagnostics = { expected },
        }.RunAsync();
    }

    [Fact]
    public async Task RpcServer_With_ClientPort_Reports_QYL0005()
    {
        const string testCode = FakeSpanShim + """

            class Server
            {
                void Handle(FakeSpan activity)
                {
                    activity.SetTag("rpc.service", "Greeter");
                    activity.SetTag({|#0:"client.port"|}, 54321);
                }
            }
            """;

        var expected = new DiagnosticResult("QYL0005", DiagnosticSeverity.Warning)
            .WithLocation(0)
            .WithArguments("client.port");

        await new CSharpAnalyzerTest<RpcServerClientAttributeAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
            ExpectedDiagnostics = { expected },
        }.RunAsync();
    }

    [Fact]
    public async Task NonRpc_Block_With_ClientAddress_Reports_Nothing()
    {
        const string testCode = FakeSpanShim + """

            class HttpServer
            {
                void Handle(FakeSpan activity)
                {
                    activity.SetTag("http.request.method", "GET");
                    activity.SetTag("client.address", "10.0.0.1");
                }
            }
            """;

        await new CSharpAnalyzerTest<RpcServerClientAttributeAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
        }.RunAsync();
    }

    [Fact]
    public async Task RpcServer_Without_ClientAttrs_Reports_Nothing()
    {
        const string testCode = FakeSpanShim + """

            class Server
            {
                void Handle(FakeSpan activity)
                {
                    activity.SetTag("rpc.system", "grpc");
                    activity.SetTag("rpc.service", "Greeter");
                    activity.SetTag("server.address", "0.0.0.0");
                    activity.SetTag("server.port", 50051);
                }
            }
            """;

        await new CSharpAnalyzerTest<RpcServerClientAttributeAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
        }.RunAsync();
    }

    [Fact]
    public async Task RpcServer_With_Both_Forbidden_Keys_Reports_Both()
    {
        const string testCode = FakeSpanShim + """

            class Server
            {
                void Handle(FakeSpan activity)
                {
                    activity.SetTag("rpc.method", "SayHello");
                    activity.SetTag({|#0:"client.address"|}, "10.0.0.1");
                    activity.SetTag({|#1:"client.port"|}, 54321);
                }
            }
            """;

        var addr = new DiagnosticResult("QYL0005", DiagnosticSeverity.Warning)
            .WithLocation(0)
            .WithArguments("client.address");
        var port = new DiagnosticResult("QYL0005", DiagnosticSeverity.Warning)
            .WithLocation(1)
            .WithArguments("client.port");

        await new CSharpAnalyzerTest<RpcServerClientAttributeAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
            ExpectedDiagnostics = { addr, port },
        }.RunAsync();
    }
}
