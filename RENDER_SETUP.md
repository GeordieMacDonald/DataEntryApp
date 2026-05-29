# Deploying TifSnippetApp to Render

This guide outlines the steps to deploy the **TifSnippetApp** web service to **Render** using the provided `render.yaml` blueprint and a Docker configuration.

---

## Overview

Unlike Azure App Service, **Render** does not have native, pre-installed support for the .NET 9.0 runtime. To deploy on Render, we use a custom **Docker** container built from Microsoft's official .NET SDK and ASP.NET Core runtime images.

At runtime, the service reads TIFF images and `AnalysisResults.csv` from a local path and writes reviewer decisions to `CaptureResults.csv`. We attach a **Render Disk** to the Web Service for persistent, write-enabled file storage.

---

## Deployment Steps

### 1. Push Code to GitHub/GitLab
Make sure your repository has `Dockerfile` and `render.yaml` in its root folder and push your changes to your git provider:

```bash
git add Dockerfile render.yaml
git commit -m "Add Dockerfile and render.yaml for Render deployment"
git push origin main
```

### 2. Deploy using Render Blueprints
1. Log in to your [Render Dashboard](https://dashboard.render.com/).
2. Click **New** (top right) and select **Blueprint**.
3. Connect your repository containing the code.
4. Render will automatically parse your `render.yaml` file:
   - It will prompt you to name the blueprint group.
   - It will list the web service (`tif-snippet-app`) and its persistent disk (`snippet-data`).
5. Click **Apply** to deploy the service.

---

## Uploading Data to the Render Disk

Since the application requires `AnalysisResults.csv` and TIFF images to run, you need to copy these files onto the newly created persistent Render Disk mounted at `/data`.

### Method A: Upload using SSH (Recommended)

Render allows you to SSH directly into your running service containers.

1. Go to your **Web Service** page on the Render Dashboard.
2. In the left sidebar, click **SSH**.
3. Follow the instructions to add your SSH key to your Render account (if you haven't already).
4. Copy the SSH command provided by Render (e.g., `ssh tif-snippet-app-xxx@ssh.oregon.render.com`).
5. Use `scp` or `rsync` from your local machine to upload files directly into the `/data` directory:

```bash
# Upload AnalysisResults.csv
scp -P 22 AnalysisResults.csv tif-snippet-app-xxx@ssh.oregon.render.com:/data/

# Upload TIFF images
scp -P 22 *.tif tif-snippet-app-xxx@ssh.oregon.render.com:/data/
```

### Method B: Upload via wget / curl inside SSH

If your dataset is hosted elsewhere (e.g., Google Drive, AWS S3, or Dropbox):
1. SSH into the container:
   ```bash
   ssh tif-snippet-app-xxx@ssh.oregon.render.com
   ```
2. Navigate to the `/data` directory:
   ```bash
   cd /data
   ```
3. Fetch the files:
   ```bash
   wget https://example.com/path/to/AnalysisResults.csv
   wget https://example.com/path/to/image.tif
   ```

---

## Validation

1. Browse to your Render URL (e.g., `https://tif-snippet-app.onrender.com`).
2. Go to the Data Entry tab. The application should load the crop snippets.
3. Review one snippet. If successful, check the disk to ensure `CaptureResults.csv` has been created or updated:
   ```bash
   ssh tif-snippet-app-xxx@ssh.oregon.render.com "cat /data/CaptureResults.csv"
   ```
