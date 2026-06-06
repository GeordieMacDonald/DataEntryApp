# DataEntryApp

**DataEntryApp** is a demo data-entry web application built with **Blazor WebAssembly** (frontend) and **ASP.NET Core** (backend). Its purpose is to let a user review and validate OCR (Optical Character Recognition) results extracted from scanned TIFF document images.

---

## Architecture

The solution (`TifSnippetApp.sln`) has two projects:

### 1. `TifSnippetApp` — ASP.NET Core Server (Backend)
Hosts the API, serves static assets, and handles data processing.

- **`SnippetController`** (`Controllers/SnippetController.cs`) — REST API with four endpoints:
  - `GET /api/snippet/{index}` — get a single OCR snippet
  - `GET /api/snippet/image/{index}` — get a snippet's cropped image (optionally expanded 5x)
  - `GET /api/snippet/batch?start=&count=` — get a batch of snippets
  - `POST /api/snippet/save` — save a review decision

- **`SnippetService`** (`Services/SnippetService.cs`) — core business logic:
  - Reads an `AnalysisResults.csv` file that contains OCR output: filename, page number, field name, extracted text, confidence score, and bounding box polygon coordinates.
  - Loads the corresponding TIFF image file, uses **ImageSharp** to crop the image to the bounding box region, and returns it as a base64-encoded PNG.
  - Tracks which records have already been reviewed (via `CaptureResults.csv`), skipping them in future batches.
  - Saves reviewer decisions (Accept / Edit / Reject variants) back to `CaptureResults.csv`.

### 2. `TifSnippetApp.Client` — Blazor WebAssembly (Frontend)
Runs in the browser as a client-side app.

- **`DataEntry.razor`** (`Pages/DataEntry.razor`) — the main UI page:
  - Displays OCR snippets in a scrollable, continuous-review layout.
  - Shows a cropped image of the document region alongside the OCR-extracted text.
  - Tracks stats: Accepted, Edited, Rejected counts.
  - Supports keyboard-driven workflow, dark/light theme toggle, contrast/invert image filters, and configurable scroll animation.
  - Loads more snippets automatically as the user works through the queue.

- **`DocumentModels.cs`** (`Models/DocumentModels.cs`) — shared data models:
  - `SnippetInfo` — data sent from server to client (image, field name, content, confidence)
  - `SnippetSubmission` — data sent from client to server (review decision + captured/corrected text)
  - `SnippetStatus` enum — `Accepted`, `Edited`, `RejectedBlank`, `RejectedIllegible`, `RejectedOther`

---

## Data Flow

```
TIFF images + AnalysisResults.csv
          ↓
    SnippetService (server)
    - crops image regions
    - returns base64 PNG + OCR text
          ↓
    Blazor UI (browser)
    - shows image snippet + OCR text
    - user accepts/edits/rejects
          ↓
    POST /api/snippet/save
          ↓
    CaptureResults.csv (audit log)
```

---

## Summary

In short, this is a **human-in-the-loop OCR validation tool**: an AI/OCR pipeline has already processed scanned documents and produced text extractions with confidence scores. This app presents those extractions one-by-one to a human reviewer who confirms, corrects, or rejects each one, with results saved to a CSV for downstream use.
