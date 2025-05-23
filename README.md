# Kubernetes Mutating Webhook for Ingress Client Certificate Authentication

This repository contains a Kubernetes Mutating Webhook implementation that automatically adds client certificate authentication annotations to Ingress resources, ensuring that all ingresses in your cluster require client certificate authentication by default.

## What is a Mutating Webhook?

A Mutating Webhook is a Kubernetes admission controller that intercepts requests to the Kubernetes API server and can modify the objects before they are stored. In this case, the webhook intercepts Ingress creation/update requests and adds the necessary NGINX annotations for client certificate authentication.

## How it Works

When a new Ingress resource is created or an existing one is updated, this webhook:

1. Intercepts the request to create/update an Ingress resource
2. Checks if the Ingress has the special annotation to skip client certificate authentication
3. If the skip annotation is set to "true", it removes any existing client certificate annotations
4. If the skip annotation doesn't exist or is set to "false", it adds all required client certificate annotations
5. Returns the modified Ingress object to the Kubernetes API server

## Required Annotations

The webhook enforces the following annotations:

```
nginx.ingress.kubernetes.io/auth-tls-verify-client: "on"
nginx.ingress.kubernetes.io/auth-tls-secret: "default/ca-secret"
nginx.ingress.kubernetes.io/auth-tls-verify-depth: "1"
nginx.ingress.kubernetes.io/auth-tls-pass-certificate-to-upstream: "true"
```

These annotations configure the NGINX Ingress Controller to:
- Require client certificates
- Verify client certificates against the CA certificate in the specified Secret
- Allow verification depth of 1 (client certificate signed directly by the CA)
- Pass the client certificate to upstream services

## How to Skip Client Certificate Authentication

If you need to expose an Ingress publicly without client certificate authentication (for example, a public API endpoint or a registry), add the following annotation to your Ingress resource:

```yaml
k8s-mutating-webhook/skip-client-cert: "true"
```

When this annotation is set to "true":
- If the Ingress already has client certificate annotations, they will be removed
- If the Ingress is new, no client certificate annotations will be added

If this annotation is set to "false" or not present, all required client certificate annotations will be added automatically.

Example Ingress with skip annotation:

```yaml
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: public-api
  annotations:
    k8s-mutating-webhook/skip-client-cert: "true"
spec:
  rules:
  - host: api.example.com
    http:
      paths:
      - path: /
        pathType: Prefix
        backend:
          service:
            name: api-service
            port:
              number: 80
```

## Deployment

### Prerequisites

- Kubernetes cluster with NGINX Ingress Controller
- TLS certificates for the webhook (included in the certs directory)
- kubectl access to the cluster

### Installing the Webhook

1. Deploy the webhook using Helm or kubectl:

```bash
kubectl apply -f MutatingWebhookConfiguration.yaml
kubectl apply -f helm/templates/deployment.yaml
kubectl apply -f helm/templates/service.yaml
```

2. Verify the webhook is running:

```bash
kubectl get pods -l app=k8smutatingwebhook
```

## Configuration

The webhook uses a CA certificate and server certificate for TLS. These are mounted from a Kubernetes Secret. The certificate should be valid for:

```
k8smutatingwebhook-service.default.svc
```

## Development

### Building the Docker Image

```bash
docker build -t k8smutatingwebhook:latest -f K8sMutatingWebhook/Dockerfile .
```

### Local Testing

You can run the webhook locally for testing:

```bash
dotnet run --project K8sMutatingWebhook/K8sMutatingWebhook.csproj
```

## Troubleshooting

### Logs

Check the webhook logs for any issues:

```bash
kubectl logs -l app=k8smutatingwebhook
```

### Common Issues

- **Webhook not intercepting requests**: Check the MutatingWebhookConfiguration and ensure the rules match your Ingress resources.
- **TLS errors**: Ensure the certificates are valid and mounted correctly.
- **Webhook returning errors**: Check the logs for detailed error messages.

## License

This project is licensed under the MIT License - see the LICENSE file for details.
