# Deploying TifSnippetApp to Azure

This guide walks through deploying the TifSnippetApp Snippet web service to **Azure App Service** with persistent data storage on **Azure Files**.

---

## Overview

TifSnippetApp consists of:
- **ASP.NET Core 9 backend** (`TifSnippetApp`) — hosts the REST API and serves the Blazor frontend
- **Blazor WebAssembly frontend** (`TifSnippetApp.Client`) — compiled and served as static assets by the backend

At runtime the service reads TIFF images and `AnalysisResults.csv` from a local path, and writes reviewer decisions to `CaptureResults.csv` in the same location. In Azure that local path is replaced by an **Azure Files share** mounted into the App Service.

---

## Prerequisites

| Tool | Version | Notes |
|------|---------|-------|
| [.NET SDK](https://dotnet.microsoft.com/download/dotnet/9.0) | 9.0 | Required to build and publish |
| [Azure CLI](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli) | Latest | Used for all resource provisioning |
| Azure subscription | — | Contributor access required |

---

## Step 1 — Prepare the Code

The dataset path in `SnippetService.cs` is currently hardcoded to `D:\datasets\Attestations Cleaned up\`. Before deploying you must make it configurable via an application setting.

### 1a — Add a configuration key

In `TifSnippetApp/appsettings.json`, add a `DatasetPath` key:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "DatasetPath": "D:\\datasets\\Attestations Cleaned up\\"
}
```

The local value keeps your development environment working. The Azure value will be set as an App Setting and override it at runtime.

### 1b — Update `SnippetService.cs`

Change the constructor to read from configuration instead of using a hardcoded path:

```csharp
public SnippetService(IWebHostEnvironment env, IConfiguration config)
{
    _datasetPath = config["DatasetPath"]
        ?? throw new InvalidOperationException("DatasetPath configuration is missing.");
    _csvPath = Path.Combine(_datasetPath, "AnalysisResults.csv");
    _resultCsvPath = Path.Combine(_datasetPath, "CaptureResults.csv");
}
```

### 1c — Register `IConfiguration` in DI (already available)

`IConfiguration` is automatically available via ASP.NET Core's DI container — no additional registration is needed in `Program.cs`.

---

## Step 2 — Provision Azure Resources

All commands below use the Azure CLI. Run `az login` first.

```bash
# --- variables (edit to suit) ---
RESOURCE_GROUP="rg-snippetapp"
LOCATION="eastus"
APP_SERVICE_PLAN="plan-snippetapp"
WEB_APP_NAME="snippetapp-<your-unique-suffix>"   # must be globally unique
STORAGE_ACCOUNT="stsnippetapp<suffix>"            # 3-24 lowercase alphanumeric
FILE_SHARE="snippetdata"
```

### 2a — Resource group

```bash
az group create \
  --name "$RESOURCE_GROUP" \
  --location "$LOCATION"
```

### 2b — App Service Plan (Windows, B2 or higher recommended for TIFF processing)

```bash
az appservice plan create \
  --name "$APP_SERVICE_PLAN" \
  --resource-group "$RESOURCE_GROUP" \
  --sku B2 \
  --is-windows
```

> **Why Windows?** The source data uses Windows-style paths. A Windows plan makes local path separators consistent and avoids cross-platform path issues with the CSV and TIFF files.

### 2c — Web App (.NET 9)

```bash
az webapp create \
  --name "$WEB_APP_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --plan "$APP_SERVICE_PLAN" \
  --runtime "DOTNET|9.0"
```

### 2d — Storage Account and File Share

```bash
az storage account create \
  --name "$STORAGE_ACCOUNT" \
  --resource-group "$RESOURCE_GROUP" \
  --location "$LOCATION" \
  --sku Standard_LRS \
  --kind StorageV2

az storage share create \
  --account-name "$STORAGE_ACCOUNT" \
  --name "$FILE_SHARE" \
  --quota 100
```

---

## Step 3 — Upload Data Files to Azure Files

Use [Azure Storage Explorer](https://azure.microsoft.com/en-us/products/storage/storage-explorer/) or the Azure CLI to upload your data.

```bash
# Retrieve the storage account key
STORAGE_KEY=$(az storage account keys list \
  --account-name "$STORAGE_ACCOUNT" \
  --resource-group "$RESOURCE_GROUP" \
  --query "[0].value" -o tsv)

# Upload AnalysisResults.csv
az storage file upload \
  --account-name "$STORAGE_ACCOUNT" \
  --account-key "$STORAGE_KEY" \
  --share-name "$FILE_SHARE" \
  --source "path/to/local/AnalysisResults.csv" \
  --path "AnalysisResults.csv"

# Upload TIFF images (repeat for each file, or use azcopy for bulk upload)
az storage file upload \
  --account-name "$STORAGE_ACCOUNT" \
  --account-key "$STORAGE_KEY" \
  --share-name "$FILE_SHARE" \
  --source "path/to/local/Image.tif" \
  --path "Image.tif"
```

For bulk uploads, [AzCopy](https://learn.microsoft.com/en-us/azure/storage/common/storage-use-azcopy-v10) is faster:

```bash
azcopy copy "path/to/local/dataset/*" \
  "https://$STORAGE_ACCOUNT.file.core.windows.net/$FILE_SHARE/?<SAS-token>" \
  --recursive
```

`CaptureResults.csv` does **not** need to be uploaded — the application creates it automatically on first save.

---

## Step 4 — Mount Azure Files to the App Service

```bash
az webapp config storage-account add \
  --name "$WEB_APP_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --custom-id "snippetdata" \
  --storage-type AzureFiles \
  --account-name "$STORAGE_ACCOUNT" \
  --share-name "$FILE_SHARE" \
  --access-key "$STORAGE_KEY" \
  --mount-path "C:\datasets\snippetdata"
```

The share will be accessible inside the App Service at `C:\datasets\snippetdata`.

---

## Step 5 — Configure Application Settings

Set the `DatasetPath` App Setting to point at the mounted share, and configure the environment to `Production`:

```bash
az webapp config appsettings set \
  --name "$WEB_APP_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --settings \
    "ASPNETCORE_ENVIRONMENT=Production" \
    "DatasetPath=C:\\datasets\\snippetdata\\"
```

---

## Step 6 — Build and Deploy

### Option A — Deploy via ZIP publish (recommended for first deploy)

```bash
# Publish the solution to a local folder
dotnet publish TifSnippetApp/TifSnippetApp.csproj \
  -c Release \
  -o ./publish

# Zip and deploy
Compress-Archive -Path ./publish/* -DestinationPath ./publish.zip -Force

az webapp deploy \
  --name "$WEB_APP_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --src-path "./publish.zip" \
  --type zip
```

### Option B — Deploy via GitHub Actions (recommended for ongoing CI/CD)

1. In the Azure portal, open your Web App → **Deployment Center**.
2. Select **GitHub** as the source and authorise the connection.
3. Choose your repository, branch (`main`), and build provider (GitHub Actions).
4. Azure will generate a workflow file (`.github/workflows/`) that builds and deploys on every push.

---

## Step 7 — Validate the Deployment

1. Browse to `https://<your-web-app-name>.azurewebsites.net` — the Blazor UI should load.
2. Check that snippets appear in the Data Entry page (confirms TIFF images and CSV are being read).
3. Accept or edit a snippet and confirm no errors appear in the browser console (confirms `CaptureResults.csv` is being written).
4. Review live logs in the Azure portal: **Web App → Log stream**.

---

## Troubleshooting

| Symptom | Likely cause | Fix |
|---------|-------------|-----|
| Blank snippet list | `DatasetPath` App Setting is wrong or Azure Files is not mounted | Verify the mount path and App Setting; check **Log stream** for path errors |
| 500 error on save | App Service process lacks write permission to the file share | Confirm the file share is mounted read/write (default for Azure Files) |
| App fails to start | .NET 9 runtime not installed | Confirm the Web App runtime is set to `DOTNET\|9.0` via the portal |
| Images not loading | TIFF filenames in `AnalysisResults.csv` don't match uploaded files | Verify filenames are case-correct and the files exist in the share root |

---

## Architecture Summary

```
Browser (Blazor WASM)
      │  HTTP requests
      ▼
Azure App Service (ASP.NET Core 9)
      │  reads/writes via mapped drive
      ▼
Azure Files share  ──►  AnalysisResults.csv
                   ──►  *.tif images
                   ◄──  CaptureResults.csv  (written by app)
```
