// Default annotations to enforce
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

string[][] RequiredAnnotations = new[]
{
    new[] {"nginx.ingress.kubernetes.io/auth-tls-verify-client", "on"},
    new[] {"nginx.ingress.kubernetes.io/auth-tls-secret", "default/ca-secret"},
    new[] {"nginx.ingress.kubernetes.io/auth-tls-verify-depth", "1"},
    new[] {"nginx.ingress.kubernetes.io/auth-tls-pass-certificate-to-upstream", "true"}
};

// Annotation name to check for skipping client certificate authentication
const string SkipClientCertAnnotation = "k8s-mutating-webhook/skip-client-cert";

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();

app.MapPost("/mutate-ingress", ([FromBody] JsonElement admissionReviewRequest) =>
{
    app.Logger.LogInformation("Received request to mutate ingress object, admissionReviewRequest: {admissionReviewRequest}", admissionReviewRequest.ToString());
    var requestUid = admissionReviewRequest.GetProperty("request").GetProperty("uid").GetString();
    var kind = admissionReviewRequest.GetProperty("request").GetProperty("kind").GetProperty("kind").GetString();
    if (kind != "Ingress")
    {
        app.Logger.LogInformation("Request kind is not Ingress, skipping");
        return Results.Ok(new
        {
            apiVersion = "admission.k8s.io/v1",
            kind = "AdmissionReview",
            response = new
            {
                uid = requestUid,
                allowed = true
            }
        });
    }

    var ingress = admissionReviewRequest.GetProperty("request").GetProperty("object");    // Extract annotations first to check for skip annotation
    var annotationsElement = ingress.GetProperty("metadata").TryGetProperty("annotations", out var annotations)
        ? annotations
        : JsonDocument.Parse("{}").RootElement;
    app.Logger.LogInformation("Annotations: {annotations}", annotations.ToString());
    var annotationsDict = annotations.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString());
    app.Logger.LogInformation("AnnotationsDict: {annotationsDict}", annotationsDict);
    
    // Check if we should skip based on annotation
    bool skipClientCertEnforcement = false;
    if (annotationsDict.TryGetValue(SkipClientCertAnnotation, out var skipValue) &&
        !string.IsNullOrEmpty(skipValue))
    {
        skipClientCertEnforcement = skipValue.Equals("true", StringComparison.OrdinalIgnoreCase) || skipValue == "1";
    }

    if (skipClientCertEnforcement)
    {
        app.Logger.LogInformation($"Removing client certificate annotations due to {SkipClientCertAnnotation}=true annotation");
        
        // Remove client certificate annotations if they exist
        foreach (var annotation in RequiredAnnotations)
        {
            if (annotationsDict.ContainsKey(annotation[0]))
            {
                annotationsDict.Remove(annotation[0]);
            }
        }
    }
    else
    {
        app.Logger.LogInformation($"Adding client certificate annotations as {SkipClientCertAnnotation} is not true");
        
        // Ensure required annotations with fallback logic
        if (!annotationsDict.ContainsKey("nginx.ingress.kubernetes.io/auth-tls-verify-client") ||
            (annotationsDict["nginx.ingress.kubernetes.io/auth-tls-verify-client"] != "on" && annotationsDict["nginx.ingress.kubernetes.io/auth-tls-verify-client"] != "optional_no_ca"))
        {
            annotationsDict["nginx.ingress.kubernetes.io/auth-tls-verify-client"] = "on"; // Default to "on"
        }

        foreach (var annotation in RequiredAnnotations)
        {
            if (!annotationsDict.ContainsKey(annotation[0]))
            {
                annotationsDict[annotation[0]] = annotation[1];
            }
        }

        // Ensure minimum depth of 1
        if (!annotationsDict.ContainsKey("nginx.ingress.kubernetes.io/auth-tls-verify-depth") || int.Parse(annotationsDict["nginx.ingress.kubernetes.io/auth-tls-verify-depth"]!) < 1)
        {
            annotationsDict["nginx.ingress.kubernetes.io/auth-tls-verify-depth"] = "1";
        }
    }

    app.Logger.LogInformation("AnnotationsDict after modifications: {annotationsDict}", annotationsDict);

    var patch = new[]
    {
        new
        {
            op = "add",
            path = "/metadata/annotations",
            value = annotationsDict
        }
    };
    app.Logger.LogInformation("Patch: {patch}", patch);

    var patchJson = JsonSerializer.Serialize(patch);

    var response = new
    {
        apiVersion = "admission.k8s.io/v1",
        kind = "AdmissionReview",
        response = new
        {
            uid = requestUid,
            allowed = true,
            patchType = "JSONPatch",
            patch = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(patchJson))
        }
    };
    app.Logger.LogInformation("Response: {response}", response);

    return Results.Ok(response);
})
.WithName("MutateIngress")
.WithOpenApi();

app.Run();
