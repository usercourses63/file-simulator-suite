# OpenShift RBAC Configuration for File Simulator Integration

**Target:** OpenShift Container Platform (OCP) 4.x
**Purpose:** Define RBAC permissions for .NET application to dynamically manage NAS PV/PVC resources

## Overview

OpenShift **fully supports Kubernetes RBAC** and extends it with additional security features. The standard Kubernetes RBAC manifests work identically in OCP, with some OCP-specific enhancements available.

---

## 1. Standard Kubernetes RBAC (Works in OCP)

### ServiceAccount

Create a ServiceAccount for your .NET application:

```yaml
apiVersion: v1
kind: ServiceAccount
metadata:
  name: file-access-manager
  namespace: your-namespace  # Replace with your project name
```

### ClusterRole (For PV Operations)

PersistentVolumes are **cluster-scoped**, so you need a ClusterRole:

```yaml
apiVersion: rbac.authorization.k8s.io/v1
kind: ClusterRole
metadata:
  name: nas-pv-manager
rules:
  # PersistentVolume operations (cluster-scoped resources)
  - apiGroups: [""]
    resources: ["persistentvolumes"]
    verbs: ["get", "list", "watch", "create", "update", "patch", "delete"]

  # Read services to get NFS server endpoints
  - apiGroups: [""]
    resources: ["services"]
    verbs: ["get", "list"]
    # Optional: restrict to file-simulator namespace only
```

### ClusterRoleBinding (Bind ClusterRole to ServiceAccount)

```yaml
apiVersion: rbac.authorization.k8s.io/v1
kind: ClusterRoleBinding
metadata:
  name: file-access-manager-pv-binding
roleRef:
  apiGroup: rbac.authorization.k8s.io
  kind: ClusterRole
  name: nas-pv-manager
subjects:
  - kind: ServiceAccount
    name: file-access-manager
    namespace: your-namespace  # Your application's namespace/project
```

### Role (For Namespace-Scoped Resources)

PVC, ConfigMap, Deployment are **namespace-scoped**, so use a Role:

```yaml
apiVersion: rbac.authorization.k8s.io/v1
kind: Role
metadata:
  name: nas-resource-manager
  namespace: your-namespace  # Replace with your project name
rules:
  # PersistentVolumeClaim operations
  - apiGroups: [""]
    resources: ["persistentvolumeclaims"]
    verbs: ["get", "list", "watch", "create", "update", "patch", "delete"]

  # ConfigMap operations
  - apiGroups: [""]
    resources: ["configmaps"]
    verbs: ["get", "list", "watch", "create", "update", "patch", "delete"]

  # Deployment operations (for adding/removing volume mounts)
  - apiGroups: ["apps"]
    resources: ["deployments"]
    verbs: ["get", "list", "update", "patch"]

  # Optional: Pod operations for validation
  - apiGroups: [""]
    resources: ["pods"]
    verbs: ["get", "list"]
```

### RoleBinding (Bind Role to ServiceAccount)

```yaml
apiVersion: rbac.authorization.k8s.io/v1
kind: RoleBinding
metadata:
  name: file-access-manager-binding
  namespace: your-namespace  # Your application's namespace/project
roleRef:
  apiGroup: rbac.authorization.k8s.io
  kind: Role
  name: nas-resource-manager
subjects:
  - kind: ServiceAccount
    name: file-access-manager
    namespace: your-namespace
```

---

## 2. OCP-Specific Enhancements (Optional)

### Using OCP Built-in Roles

OCP provides pre-defined roles that you can use instead of creating custom ones:

```yaml
apiVersion: rbac.authorization.k8s.io/v1
kind: RoleBinding
metadata:
  name: file-access-manager-edit
  namespace: your-namespace
roleRef:
  apiGroup: rbac.authorization.k8s.io
  kind: ClusterRole
  name: edit  # OCP built-in role (can edit most resources)
subjects:
  - kind: ServiceAccount
    name: file-access-manager
    namespace: your-namespace
```

**OCP Built-in Roles:**
- `admin` - Full control over namespace resources
- `edit` - Create/update/delete most resources (no RBAC management)
- `view` - Read-only access

**Recommendation:** Use `edit` role for simplicity if your security policy allows. It includes PVC, ConfigMap, and Deployment permissions but NOT PV permissions (those need ClusterRole).

### Security Context Constraints (SCC)

**Important:** If your .NET application pod needs to manage Kubernetes resources, ensure it has appropriate SCC:

```yaml
# Usually not needed - the default 'restricted' SCC is sufficient
# Your app talks to K8s API server, not running privileged operations

# Only needed if your app does filesystem operations requiring elevated permissions
```

For managing Kubernetes resources via API, you typically **DON'T need custom SCC**. The default `restricted` SCC is fine.

---

## 3. Complete OCP RBAC Manifest

### Single File for OCP Deployment

```yaml
# file-access-manager-rbac.yaml
# Deploy to OCP with: oc apply -f file-access-manager-rbac.yaml

---
apiVersion: v1
kind: ServiceAccount
metadata:
  name: file-access-manager
  namespace: your-namespace  # CHANGE THIS to your OCP project name

---
# ClusterRole for PersistentVolume operations (cluster-scoped)
apiVersion: rbac.authorization.k8s.io/v1
kind: ClusterRole
metadata:
  name: nas-pv-manager
rules:
  - apiGroups: [""]
    resources: ["persistentvolumes"]
    verbs: ["get", "list", "watch", "create", "update", "patch", "delete"]

  - apiGroups: [""]
    resources: ["services"]
    verbs: ["get", "list"]

---
# ClusterRoleBinding (grants PV permissions to ServiceAccount)
apiVersion: rbac.authorization.k8s.io/v1
kind: ClusterRoleBinding
metadata:
  name: file-access-manager-pv-binding
roleRef:
  apiGroup: rbac.authorization.k8s.io
  kind: ClusterRole
  name: nas-pv-manager
subjects:
  - kind: ServiceAccount
    name: file-access-manager
    namespace: your-namespace  # CHANGE THIS

---
# Role for namespace-scoped resources (PVC, ConfigMap, Deployment)
apiVersion: rbac.authorization.k8s.io/v1
kind: Role
metadata:
  name: nas-resource-manager
  namespace: your-namespace  # CHANGE THIS
rules:
  - apiGroups: [""]
    resources: ["persistentvolumeclaims"]
    verbs: ["get", "list", "watch", "create", "update", "patch", "delete"]

  - apiGroups: [""]
    resources: ["configmaps"]
    verbs: ["get", "list", "watch", "create", "update", "patch", "delete"]

  - apiGroups: ["apps"]
    resources: ["deployments"]
    verbs: ["get", "list", "update", "patch"]

  - apiGroups: [""]
    resources: ["pods"]
    verbs: ["get", "list"]

---
# RoleBinding (grants namespace permissions to ServiceAccount)
apiVersion: rbac.authorization.k8s.io/v1
kind: RoleBinding
metadata:
  name: file-access-manager-binding
  namespace: your-namespace  # CHANGE THIS
roleRef:
  apiGroup: rbac.authorization.k8s.io
  kind: Role
  name: nas-resource-manager
subjects:
  - kind: ServiceAccount
    name: file-access-manager
    namespace: your-namespace  # CHANGE THIS
```

**Deploy to OCP:**
```bash
# Replace your-namespace with your OCP project name
sed -i 's/your-namespace/my-project/g' file-access-manager-rbac.yaml

# Apply using oc CLI (OpenShift CLI)
oc apply -f file-access-manager-rbac.yaml

# Verify ServiceAccount created
oc get sa file-access-manager -n my-project

# Verify ClusterRole created
oc get clusterrole nas-pv-manager

# Verify Role created
oc get role nas-resource-manager -n my-project
```

---

## 4. Using ServiceAccount in Your Deployment

### Reference ServiceAccount in Deployment

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: your-app
  namespace: your-namespace
spec:
  replicas: 1
  selector:
    matchLabels:
      app: your-app
  template:
    metadata:
      labels:
        app: your-app
    spec:
      serviceAccountName: file-access-manager  # Use the ServiceAccount
      containers:
        - name: app
          image: your-dotnet-app:latest
          env:
            # KubernetesClient will automatically use in-cluster config
            # with this ServiceAccount's token
            - name: KUBERNETES__CONTEXT
              value: ""  # Empty means in-cluster config
            - name: KUBERNETES__NAMESPACE
              valueFrom:
                fieldRef:
                  fieldPath: metadata.namespace
```

### .NET In-Cluster Configuration

When running **inside OCP**, your .NET app automatically uses the ServiceAccount:

```csharp
// In Program.cs or Startup.cs
builder.Services.AddSingleton<Kubernetes>(sp =>
{
    // Automatically detects in-cluster config (uses ServiceAccount token)
    var config = KubernetesClientConfiguration.InClusterConfig();
    return new Kubernetes(config);
});

// The Kubernetes client will use the file-access-manager ServiceAccount permissions
```

**How it works:**
1. OCP mounts ServiceAccount token at `/var/run/secrets/kubernetes.io/serviceaccount/token`
2. KubernetesClient reads this token automatically
3. All API calls use the ServiceAccount's permissions
4. Your app can create PVs, PVCs, ConfigMaps, update Deployments as allowed by RBAC

---

## 5. OCP CLI Commands (oc vs kubectl)

OpenShift uses `oc` CLI (compatible with `kubectl`):

```bash
# Create ServiceAccount
oc create sa file-access-manager -n my-project

# Create Role
oc create role nas-resource-manager \
  --verb=get,list,create,update,patch,delete \
  --resource=persistentvolumeclaims,configmaps \
  -n my-project

# Create RoleBinding
oc create rolebinding file-access-manager-binding \
  --role=nas-resource-manager \
  --serviceaccount=my-project:file-access-manager \
  -n my-project

# Create ClusterRole (for PVs)
oc create clusterrole nas-pv-manager \
  --verb=get,list,create,update,patch,delete \
  --resource=persistentvolumes

# Create ClusterRoleBinding
oc create clusterrolebinding file-access-manager-pv-binding \
  --clusterrole=nas-pv-manager \
  --serviceaccount=my-project:file-access-manager
```

**Note:** `oc` and `kubectl` commands are interchangeable. Use whichever you prefer.

---

## 6. Verifying RBAC Permissions

### Test ServiceAccount Permissions

```bash
# Check if ServiceAccount can create PVCs
oc auth can-i create persistentvolumeclaims \
  --as=system:serviceaccount:my-project:file-access-manager \
  -n my-project
# Expected: yes

# Check if ServiceAccount can create PVs
oc auth can-i create persistentvolumes \
  --as=system:serviceaccount:my-project:file-access-manager
# Expected: yes

# Check if ServiceAccount can update deployments
oc auth can-i update deployments \
  --as=system:serviceaccount:my-project:file-access-manager \
  -n my-project
# Expected: yes

# Check if ServiceAccount can create configmaps
oc auth can-i create configmaps \
  --as=system:serviceaccount:my-project:file-access-manager \
  -n my-project
# Expected: yes
```

### List All Permissions for ServiceAccount

```bash
# See what the ServiceAccount can do
oc policy can-i --list \
  --as=system:serviceaccount:my-project:file-access-manager \
  -n my-project
```

---

## 7. OCP Project vs Kubernetes Namespace

**Important difference:**

- **Kubernetes:** Uses "namespaces"
- **OCP:** Uses "projects" (which are namespaces with additional RBAC/networking)

**In your .NET code:**
```csharp
// Works in both Kubernetes and OCP
var namespace_ = "my-project";  // In OCP, this is your project name

await _k8sClient.CoreV1.CreateNamespacedPersistentVolumeClaimAsync(
    pvc,
    namespace_,  // Use project name here
    cancellationToken: cancellationToken);
```

**OCP automatically handles the project → namespace mapping.**

---

## 8. Minimum RBAC for NAS Integration

If you want the **least privilege** approach:

```yaml
---
apiVersion: v1
kind: ServiceAccount
metadata:
  name: file-access-manager
  namespace: your-namespace

---
# Minimal ClusterRole - Only PV read/write
apiVersion: rbac.authorization.k8s.io/v1
kind: ClusterRole
metadata:
  name: nas-pv-readonly
rules:
  - apiGroups: [""]
    resources: ["persistentvolumes"]
    verbs: ["get", "list"]  # Read-only if PVs are pre-created

---
apiVersion: rbac.authorization.k8s.io/v1
kind: ClusterRoleBinding
metadata:
  name: file-access-manager-pv-readonly
roleRef:
  apiGroup: rbac.authorization.k8s.io
  kind: ClusterRole
  name: nas-pv-readonly
subjects:
  - kind: ServiceAccount
    name: file-access-manager
    namespace: your-namespace

---
# Minimal Role - Only PVC and ConfigMap
apiVersion: rbac.authorization.k8s.io/v1
kind: Role
metadata:
  name: nas-pvc-manager
  namespace: your-namespace
rules:
  # PVC operations (user creates/deletes PVCs for NAS servers)
  - apiGroups: [""]
    resources: ["persistentvolumeclaims"]
    verbs: ["get", "list", "watch", "create", "update", "patch", "delete"]

  # ConfigMap operations (service discovery)
  - apiGroups: [""]
    resources: ["configmaps"]
    verbs: ["get", "list", "watch", "create", "update", "patch"]

  # Deployment operations (add/remove volume mounts)
  - apiGroups: ["apps"]
    resources: ["deployments"]
    verbs: ["get", "list", "update", "patch"]

---
apiVersion: rbac.authorization.k8s.io/v1
kind: RoleBinding
metadata:
  name: file-access-manager-binding
  namespace: your-namespace
roleRef:
  apiGroup: rbac.authorization.k8s.io
  kind: Role
  name: nas-pvc-manager
subjects:
  - kind: ServiceAccount
    name: file-access-manager
    namespace: your-namespace
```

**This minimal setup assumes:**
- PVs are **pre-created** by cluster admin (read-only access sufficient)
- Your app only creates/deletes PVCs (user-level resources)
- Your app updates its own deployment

---

## 9. OCP Security Context Constraints (SCC)

**Do you need custom SCC?**

**NO** - For Kubernetes API operations (creating PVs, PVCs, ConfigMaps), you **DON'T need custom SCC**.

Your .NET application:
- Runs as a normal pod
- Talks to Kubernetes API server via HTTPS
- Uses ServiceAccount token for authentication
- Uses RBAC for authorization

**Default `restricted` SCC is sufficient.**

**You WOULD need custom SCC if:**
- Your app needs `privileged: true`
- Your app needs `hostPath` volumes
- Your app needs `hostNetwork: true`
- Your app needs specific Linux capabilities

**For NAS integration via Kubernetes API: DEFAULT SCC is fine.**

---

## 10. Complete OCP Deployment Example

### All-in-One Manifest

```yaml
# file-access-app-ocp.yaml
# Deploy to OCP with: oc apply -f file-access-app-ocp.yaml

---
# ServiceAccount
apiVersion: v1
kind: ServiceAccount
metadata:
  name: file-access-manager
  namespace: my-app  # CHANGE THIS

---
# ClusterRole (for PV operations)
apiVersion: rbac.authorization.k8s.io/v1
kind: ClusterRole
metadata:
  name: nas-pv-manager
rules:
  - apiGroups: [""]
    resources: ["persistentvolumes"]
    verbs: ["get", "list", "watch", "create", "update", "patch", "delete"]

---
# ClusterRoleBinding
apiVersion: rbac.authorization.k8s.io/v1
kind: ClusterRoleBinding
metadata:
  name: file-access-manager-pv-binding
roleRef:
  apiGroup: rbac.authorization.k8s.io
  kind: ClusterRole
  name: nas-pv-manager
subjects:
  - kind: ServiceAccount
    name: file-access-manager
    namespace: my-app  # CHANGE THIS

---
# Role (for namespace-scoped resources)
apiVersion: rbac.authorization.k8s.io/v1
kind: Role
metadata:
  name: nas-resource-manager
  namespace: my-app  # CHANGE THIS
rules:
  - apiGroups: [""]
    resources: ["persistentvolumeclaims", "configmaps"]
    verbs: ["get", "list", "watch", "create", "update", "patch", "delete"]
  - apiGroups: ["apps"]
    resources: ["deployments"]
    verbs: ["get", "list", "update", "patch"]

---
# RoleBinding
apiVersion: rbac.authorization.k8s.io/v1
kind: RoleBinding
metadata:
  name: file-access-manager-binding
  namespace: my-app  # CHANGE THIS
roleRef:
  apiGroup: rbac.authorization.k8s.io
  kind: Role
  name: nas-resource-manager
subjects:
  - kind: ServiceAccount
    name: file-access-manager
    namespace: my-app  # CHANGE THIS

---
# Deployment (your .NET application)
apiVersion: apps/v1
kind: Deployment
metadata:
  name: file-access-app
  namespace: my-app  # CHANGE THIS
spec:
  replicas: 1
  selector:
    matchLabels:
      app: file-access-app
  template:
    metadata:
      labels:
        app: file-access-app
    spec:
      serviceAccountName: file-access-manager  # Use the ServiceAccount
      containers:
        - name: app
          image: your-registry/your-app:latest  # CHANGE THIS
          ports:
            - containerPort: 8080
              protocol: TCP
          env:
            - name: ASPNETCORE_ENVIRONMENT
              value: "Production"
            - name: Kubernetes__ApplicationNamespace
              valueFrom:
                fieldRef:
                  fieldPath: metadata.namespace
            - name: Kubernetes__DeploymentName
              value: "file-access-app"
          # Application will automatically use in-cluster config
          # with ServiceAccount token mounted at /var/run/secrets/kubernetes.io/serviceaccount/token
```

---

## 11. Deployment Steps for OCP

### Step 1: Create Project (if needed)

```bash
# Create new project in OCP
oc new-project my-app

# Or use existing project
oc project my-app
```

### Step 2: Deploy RBAC Manifests

```bash
# Replace namespace placeholders
sed -i 's/your-namespace/my-app/g' file-access-manager-rbac.yaml

# Apply to OCP
oc apply -f file-access-manager-rbac.yaml

# Verify
oc get sa file-access-manager -n my-app
oc get clusterrole nas-pv-manager
oc get role nas-resource-manager -n my-app
```

### Step 3: Verify Permissions

```bash
# Test as ServiceAccount
oc auth can-i create persistentvolumes \
  --as=system:serviceaccount:my-app:file-access-manager
# Expected: yes

oc auth can-i create persistentvolumeclaims \
  --as=system:serviceaccount:my-app:file-access-manager \
  -n my-app
# Expected: yes

oc auth can-i update deployments \
  --as=system:serviceaccount:my-app:file-access-manager \
  -n my-app
# Expected: yes
```

### Step 4: Deploy Your Application

```bash
# Build and push your .NET app to OCP internal registry
oc new-build --name=file-access-app --binary --strategy=docker
oc start-build file-access-app --from-dir=./src/YourApp --follow

# Deploy with RBAC ServiceAccount
oc apply -f file-access-app-deployment.yaml

# Or use oc new-app
oc new-app your-registry/your-app:latest \
  --name=file-access-app \
  -n my-app

# Patch to use ServiceAccount
oc set serviceaccount deployment/file-access-app file-access-manager -n my-app
```

### Step 5: Verify In-Cluster Configuration

```bash
# Check pod is using ServiceAccount
oc get pod -n my-app -l app=file-access-app -o yaml | grep serviceAccountName
# Should show: serviceAccountName: file-access-manager

# Check token is mounted
oc exec -n my-app <pod-name> -- ls /var/run/secrets/kubernetes.io/serviceaccount/
# Should show: ca.crt, namespace, token

# Test API access from pod
oc exec -n my-app <pod-name> -- curl -k -H "Authorization: Bearer $(cat /var/run/secrets/kubernetes.io/serviceaccount/token)" \
  https://kubernetes.default.svc/api/v1/namespaces/my-app/persistentvolumeclaims
# Should return PVC list
```

---

## 12. OCP-Specific Considerations

### Image Pull Secrets

If your .NET app image is in a private registry:

```yaml
spec:
  serviceAccountName: file-access-manager
  imagePullSecrets:
    - name: my-registry-secret
  containers:
    - name: app
      image: private-registry/your-app:latest
```

### Routes (OCP-specific ingress)

Expose your settings UI via OCP Route:

```bash
# Create route for your app
oc expose svc/file-access-app -n my-app

# Get route URL
oc get route file-access-app -n my-app -o jsonpath='{.spec.host}'
```

### Resource Quotas

If your OCP project has resource quotas, ensure they allow PVC creation:

```bash
# Check project quotas
oc get resourcequota -n my-app

# Describe quota details
oc describe resourcequota -n my-app
```

If quota limits PVC count, request increase from cluster admin.

---

## 13. Troubleshooting

### Permission Denied Errors

**Error:** `persistentvolumes is forbidden: User "system:serviceaccount:my-app:file-access-manager" cannot create resource "persistentvolumes"`

**Solution:** ClusterRole not bound correctly. Verify:
```bash
oc get clusterrolebinding file-access-manager-pv-binding -o yaml
# Check subjects.namespace matches your project
```

### ServiceAccount Not Found

**Error:** `serviceaccounts "file-access-manager" not found`

**Solution:** Create ServiceAccount first:
```bash
oc create sa file-access-manager -n my-app
```

### In-Cluster Config Not Working

**Error:** `Unable to load in-cluster configuration`

**Solution:** Ensure ServiceAccount is set in deployment:
```bash
oc set serviceaccount deployment/your-app file-access-manager -n my-app
```

---

## 14. Security Best Practices for OCP

1. **Least Privilege:** Start with minimal permissions (get, list) and add as needed
2. **Namespace Isolation:** Use Role/RoleBinding for namespace resources, ClusterRole only for cluster-scoped (PV)
3. **ServiceAccount per App:** Don't share ServiceAccounts between applications
4. **Audit Logging:** OCP logs all API calls - monitor for unexpected operations
5. **Token Rotation:** OCP automatically rotates ServiceAccount tokens

---

## 15. Key Differences: OCP vs Minikube

| Aspect | Minikube (Dev) | OCP (Production) |
|--------|---------------|------------------|
| **RBAC** | Same (K8s RBAC) | Same + OCP extensions |
| **CLI** | kubectl | oc (compatible with kubectl) |
| **Namespace** | namespace | project (namespace++) |
| **ServiceAccount** | Standard K8s | Same, with SCC integration |
| **In-cluster config** | Works | Works identically |
| **PV/PVC** | Same API | Same API |
| **Security** | Minimal | SCC + RBAC + network policies |

**Bottom line:** Your .NET Kubernetes API code will work **identically** in OCP. The RBAC manifests are standard Kubernetes and fully supported.

---

## 16. Summary

**✅ Yes, RBAC is fully supported in OCP**

**What you need:**
1. **ServiceAccount** for your .NET app
2. **ClusterRole + ClusterRoleBinding** for PersistentVolume operations
3. **Role + RoleBinding** for PVC, ConfigMap, Deployment operations
4. **Reference ServiceAccount** in your deployment spec

**The code examples in DOTNET-K8S-INTEGRATION.md work without modification in OCP.**

**Apply the RBAC manifest above, and your .NET application will be able to:**
- ✅ Create and delete PersistentVolumes
- ✅ Create and delete PersistentVolumeClaims
- ✅ Create and update ConfigMaps
- ✅ Add and remove volume mounts from deployments
- ✅ Manage NAS servers dynamically from your settings UI

---

## Quick Start for OCP

```bash
# 1. Create project
oc new-project my-app

# 2. Apply RBAC
oc apply -f file-access-manager-rbac.yaml

# 3. Verify permissions
oc auth can-i create persistentvolumes --as=system:serviceaccount:my-app:file-access-manager
oc auth can-i create persistentvolumeclaims --as=system:serviceaccount:my-app:file-access-manager -n my-app

# 4. Deploy your .NET app with serviceAccountName: file-access-manager

# 5. Your app can now use KubernetesClient with in-cluster config
```

**All the .NET code examples from DOTNET-K8S-INTEGRATION.md will work in OCP without changes.**

---

**For complete .NET integration examples, see:** [`DOTNET-K8S-INTEGRATION.md`](DOTNET-K8S-INTEGRATION.md)
