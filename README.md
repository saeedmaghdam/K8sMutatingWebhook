# Kubernetes Mutating Webhook for Ingress Client Certificate Authentication

This repository is retained for historical reference only.

The active homelab direction is to use normal server-side TLS for `*.kub.lab` and not to inject ingress client-certificate authentication annotations automatically.

Do not deploy this webhook in the current cluster baseline.

## What is a Mutating Webhook?

A Mutating Webhook is a Kubernetes admission controller that intercepts requests to the Kubernetes API server and can modify the objects before they are stored. In this case, the webhook intercepts Ingress creation/update requests and adds the necessary NGINX annotations for client certificate authentication.

## Historical Behavior

When a new Ingress resource is created or an existing one is updated, this webhook:

1. Intercepts the request to create/update an Ingress resource
2. Checks if the Ingress has the special annotation to skip client certificate authentication
3. If the skip annotation is set to "true", it removes any existing client certificate annotations
4. If the skip annotation doesn't exist or is set to "false", it adds all required client certificate annotations
5. Returns the modified Ingress object to the Kubernetes API server

## Historical Annotations

The webhook historically enforced a fixed set of NGINX ingress client-certificate annotations.

These annotations configure the NGINX Ingress Controller to:
- Require client certificates
- Verify client certificates against the CA certificate in the specified Secret
- Allow verification depth of 1 (client certificate signed directly by the CA)
- Pass the client certificate to upstream services

## Historical Skip Mechanism

If you needed to expose an Ingress publicly without client certificate authentication, you added a dedicated skip annotation understood by the webhook.

When this annotation is set to "true":
- If the Ingress already has client certificate annotations, they will be removed
- If the Ingress is new, no client certificate annotations will be added

If this annotation is set to "false" or not present, all required client certificate annotations will be added automatically.

Example behavior: an ingress marked to bypass webhook mutation would remain on normal server-side TLS only.

## Historical Deployment

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
