// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#pragma warning disable CS0618 // suppress compiler's [Obsolete] noise so we can see analyzer output

using System.Diagnostics;
using OpenTelemetry.SemanticConventions;

namespace Consumer;

/// <summary>
/// Each method is a deliberate trigger site for one analyzer rule.
/// Building this project should produce exactly the diagnostics noted in
/// each method's leading comment.
/// </summary>
public static class IntentionalViolations
{
    // EXPECT: QYL0010 (deprecated semconv constant — http.method replaced by http.request.method)
    public static void DeprecatedHttpMethod_FiresQYL0010(Activity activity)
    {
        activity?.SetTag(HttpAttributes.AttributeHttpMethod, "GET");
    }

    // EXPECT: QYL0010 (NetSockHostAddr is deprecated, replaced by network.local.address)
    public static void DeprecatedNetSockHostAddr_FiresQYL0010(Activity activity)
    {
        activity?.SetTag(NetAttributes.AttributeNetSockHostAddr, "10.0.0.1");
    }

    // EXPECT: QYL0011 ×2 (literal "http.request.method" and "http.response.status_code"
    //                      should be the typed constants)
    public static void HardcodedLiterals_FireQYL0011(Activity activity)
    {
        activity?.SetTag("http.request.method", "POST");
        activity?.SetTag("http.response.status_code", 200);
    }

    // EXPECT: nothing (using typed constants is correct)
    public static void TypedConstants_FireNothing(Activity activity)
    {
        activity?.SetTag(HttpAttributes.AttributeHttpRequestMethod, "POST");
        activity?.SetTag(HttpAttributes.AttributeHttpResponseStatusCode, 200);
    }

    // EXPECT: QYL0005 (RPC server span sets client.address)
    public static void RpcServerWithClientAddress_FiresQYL0005(Activity activity)
    {
        activity?.SetTag("rpc.system", "grpc");
        activity?.SetTag("rpc.service", "Greeter");
        activity?.SetTag("client.address", "192.168.1.50");
        activity?.SetTag("server.address", "0.0.0.0");
    }

    // EXPECT: QYL0001 (gen_ai.execute_tool span without gen_ai.tool.name)
    public static void GenAiExecuteToolWithoutName_FiresQYL0001(Activity activity)
    {
        activity?.SetTag("gen_ai.operation.name", "execute_tool");
        activity?.SetTag("gen_ai.system", "openai");
    }

    // EXPECT: nothing (gen_ai.tool.name present)
    public static void GenAiExecuteToolWithName_FiresNothing(Activity activity, string toolName)
    {
        activity?.SetTag("gen_ai.operation.name", "execute_tool");
        activity?.SetTag("gen_ai.tool.name", toolName);
        activity?.SetTag("gen_ai.system", "openai");
    }

    // EXPECT: QYL0002 (graphql.document is opt-in, surface as Info)
    public static void GraphqlDocument_FiresQYL0002(Activity activity, string query)
    {
        activity?.SetTag("graphql.document", query);
    }

    // EXPECT: nothing (graphql.operation.name is fine, it's not graphql.document)
    public static void GraphqlOperationName_FiresNothing(Activity activity, string opName)
    {
        activity?.SetTag("graphql.operation.name", opName);
        activity?.SetTag("graphql.operation.type", "query");
    }

    // STRESS: a method that combines QYL0001, QYL0005, QYL0011 in one block
    public static void Combined_FireMultiple(Activity activity)
    {
        activity?.SetTag("rpc.system", "grpc");                           // QYL0011 (literal -> RpcAttributes)
        activity?.SetTag("rpc.service", "Greeter");                       // QYL0011
        activity?.SetTag("client.address", "10.0.0.1");                   // QYL0005 + QYL0011
        activity?.SetTag("client.port", 54321);                           // QYL0005 + QYL0011
        activity?.SetTag("gen_ai.operation.name", "execute_tool");        // QYL0001 (no tool name) + QYL0011
        activity?.SetTag("gen_ai.system", "openai");                      // QYL0011
    }
}
