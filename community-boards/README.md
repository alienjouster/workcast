# Community Boards

This folder contains community-contributed scraper configurations for common job boards.
Each file contains the board URL and the AI-generated CSS selector configuration needed to scrape it.

---

## How to import configurations

In the Workcast UI, click **+ Add Board**, then select the **Import Boards** tab.

You can load multiple configurations at once — mix and match files and URLs freely.

### From files

Select one or more `.json` files from this folder using the file picker. All selected files are parsed immediately and added to the import queue.

### From URLs

Paste one or more raw GitHub URLs into the text area (one per line), then click **Load**.

URL format:
```
https://raw.githubusercontent.com/alienjouster/workcast/master/community-boards/<filename>.json
```

A pre-filled test URL pointing to `example.json` is provided by default so you can verify the flow works before importing real boards.

### Reviewing the queue

Each loaded file appears as a queue entry showing its name, URL, and a green/red indicator for parse status. Invalid entries display the error and can be removed individually with the × button.

### Importing

Click **Import Boards** to import all valid entries in the queue at once. Each imported board is created immediately in **Active** status and an initial scrape is enqueued automatically — no AI analysis step is needed because the scraper configuration is already supplied.

If some imports fail (e.g. the board URL is already registered), the form stays open showing which entries failed. Successfully imported boards are marked with a green check.

> **Note:** CSS selectors can become stale when a job board redesigns its site. If a board
> stops returning ads, use **Auto-configure with AI** on the board detail page to regenerate
> the configuration and consider submitting a PR with the updated file.

---

## How to contribute your configuration

1. Go to the board detail page in Workcast.
2. Click **Export config** in the page header (visible once board analysis is complete).
3. Rename the downloaded file after the board domain (e.g. `indeed-fr.json`, `stackoverflow-jobs.json`).
4. Add it to this folder and open a pull request.

### Naming convention

| Pattern | Example |
|---|---|
| `{domain}.json` | `indeed.com.json` |
| `{domain}-{locale}.json` | `indeed-fr.json` |
| `{brand}-{product}.json` | `stackoverflow-jobs.json` |

---

## File format

```json
{
  "schemaVersion": "1",
  "name": "Human-readable board name",
  "url": "https://example-jobs.com/careers",
  "scheduleCron": "0 * * * *",
  "scraperConfig": {
    "paginationType": "url_param",
    "jobCardSelector": ".job-card",
    "fieldSelectors": {
      "detailUrl": "a.job-link",
      "title": ".job-title",
      "company": ".company-name",
      "location": ".job-location",
      "salaryRaw": ".salary",
      "postedAt": ".posted-date",
      "descriptionSnippet": ".job-summary",
      "externalId": null
    },
    "nextPageSelector": null,
    "urlParamName": "page",
    "urlParamIsOffset": false,
    "maxPages": null,
    "requiresJs": false,
    "suggestedDelayMs": 1000,
    "confidenceScore": 0.92,
    "analyzerNotes": "Optional notes from the AI analyzer.",
    "generatedAt": "2026-04-12T00:00:00Z"
  }
}
```

### `paginationType` values

| Value | Meaning |
|---|---|
| `url_param` | Pagination via a URL query parameter (e.g. `?page=2`) |
| `next_button` | Click a "next page" button to advance |
| `infinite_scroll` | Page loads more content as the user scrolls |
| `load_more_button` | A "load more" button appends results to the current page |
| `none` | All results are on a single page |
