# 0017 — Photos live in blob storage and are served by the API

- Status: accepted
- Date: 2026-11-18

## Context

Until now an operation carried a number of photos and nothing else. The brief names file storage
for photos as optional; the domain already depends on photos — an Unloading or a Disposal without
one raises a Missing photo alert, and the card says "Next: Upload photo" — so without real files
that alert can never be closed by the client.

The demo is public and has no sign-in: anyone who opens the link can upload. It runs as one small
container on Azure with a managed PostgreSQL, costs at most 15 USD, and rebuilds its sample data
at every start and every night ([ADR 0015](0015-the-demo-rebuilds-its-sample-data.md)).

## Decision

- **The bytes go to blob storage; the database keeps one row per photo** — its operation, its
  storage key, the content type, the size and the instant. Azure Blob Storage in the deployment,
  the Azurite emulator under Docker Compose and in the tests: one implementation, one code path.
- **The application sees a port**, `IPhotoStorage`, with three operations: save, open, delete the
  uploads. `Azure.Storage.Blobs` is referenced only by Infrastructure.
- **The container is private and the API serves the photos**:
  `GET /api/v1/orders/{number}/photos/{photoId}` streams the blob. No public container, no
  shared-access signatures, and the pages stay on one origin under their Content-Security-Policy.
- **The API reaches storage with a connection string**, read from `ConnectionStrings:PhotoStorage`
  and held as a secret of the container app, like the database connection.
- **An upload is checked by its content, not by what the client says**: JPEG, PNG or WebP by
  signature, at most 5 MB a file, 5 files a request, 10 photos an operation, and 200 uploaded
  photos in all. The content type stored and served is the one found in the bytes.
- **The blobs are written before the row.** If the save of the order fails — a stale version —
  the blobs just written are deleted; one that slips through is removed by the next rebuild.
- **A rebuild of the sample data deletes every uploaded blob** and makes sure the sample images
  exist. The seeded photos are rows that point at a handful of generated sample images, so the
  seeded numbers and the galleries agree.
- **`Operation.PhotoCount` stays as a stored column**, kept equal to the number of photo rows by
  the aggregate. The list, the detail and the totals read it as before.

## Alternatives

- **Photos as `bytea` in PostgreSQL** — no new service, one transaction. But the smallest database
  tier would carry image traffic, backups would grow with it, and it is not the file storage the
  brief speaks of.
- **A public container, or shared-access signatures handed to the browser** — the API would not
  stream bytes. It adds a second origin to the Content-Security-Policy and lets anything uploaded
  be linked from anywhere, on a demo anyone can write to.
- **A managed identity instead of a connection string** — no key to hold. It needs a second
  credential path for Azurite, which has no identities, and the first deployment could not be
  tried locally. It is the first change if the demo were to stay.
- **A volume on the container** — Container Apps replicas have no durable disk without an Azure
  Files mount, which is the same new resource with a worse API.
- **Thumbnails made on upload** — the pages scale the original instead. An imaging library is a
  dependency and an attack surface the demo does not need for a dozen small pictures.

## Consequences

- A second stateful dependency: Compose and the test suite start Azurite, and the deployment has a
  storage account. The API does not start without `ConnectionStrings:PhotoStorage`.
- Readiness still means "the database answers". A storage outage fails uploads and galleries, not
  the whole cabinet.
- Every photo passes through the API's memory limits; the caps above keep that bounded.
- An upload is two writes that are not one transaction. The order of the writes and the rebuild
  make the leftovers harmless, not impossible.
- The storage account key is a long-lived secret. It lives only in the container app's secret and
  is rotated by regenerating the key and setting the secret again.
- Without sign-in the caps are the only protection, and nightly deletion is the backstop.
