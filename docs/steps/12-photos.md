# 12 — Photos of an operation

## Goal

An operation's photos become real files: they are uploaded from the order page, kept in blob
storage, and shown in a gallery. Uploading the photo an Unloading or a Disposal lacks closes the
Missing photo alert, so "Next: Upload photo" finally leads somewhere. The brief names file storage
for photos as optional; this step is beyond it.

## Decisions

- **Storage, serving and limits follow
  [ADR 0017](../adr/0017-photos-in-blob-storage-behind-the-api.md)**: blobs behind the port
  `IPhotoStorage`, a row per photo, the API streams the bytes, content checked by signature.
- **A photo belongs to an operation.** `Photo` is an entity of the `Order` aggregate under
  `Operation`. `Operation.PhotoCount` stays a stored column and always equals the number of its
  photos — the aggregate keeps it so, and the read side does not change.
- **An operation is recorded first and its photos are attached after.** The operations request
  loses `photoCount`: a number without files is what this step replaces. An Unloading recorded
  from the page therefore raises the Missing photo alert, and the upload that follows closes it.
  Recording stays a small JSON request with its own tests; the upload is a separate multipart
  request that can fail and be retried on its own.
- **Attaching photos is a domain operation**: `Order.AttachPhotos(operationId, photos, now)`.
  It is refused unless the order is active (Ready or In progress), like recording an operation,
  and when the operation would exceed its limit. After it, if the shipment has an open Missing
  photo alert and every operation of a type that requires a photo has at least one, the alert is
  resolved.
- **The upload uses the optimistic concurrency of the write flow**
  ([ADR 0013](../adr/0013-optimistic-concurrency-on-order-version.md)): the request carries the
  version the page saw and the order's version moves.
- **The sample data keeps its numbers.** A seeded operation gets as many photo rows as its count
  says, each pointing at one of a few sample images that ship with the application. `NewOperation`
  carries the photos of a new operation instead of a count; the API passes none.
- **The sample images are generated placeholders**, not photographs: a few small pictures drawn
  by a script kept in the repository, so nothing of unknown origin is committed.

## Contract

### Domain

- `Photo`: `Id`, `OperationId`, `StorageKey`, `ContentType`, `SizeBytes`, `UploadedAt`.
- `NewPhoto(StorageKey, ContentType, SizeBytes)`; `NewOperation.Photos` replaces
  `NewOperation.PhotoCount`. The key of an upload is made before the photo exists, so it is a
  UUID of its own, not the photo's id.
- `Operation.MaxPhotoCount = 10`. `Order.AttachPhotos` returns the photos added or an error:
  the order is not active, the shipment's operation is not found, the limit would be exceeded.

### Schema

One additive migration: the table `photos` (`id`, `operation_id` → `operations` on delete cascade,
`storage_key`, `content_type`, `size_bytes`, `uploaded_at`), indexed by `operation_id`. No column
is dropped; `operations.photo_count` stays.

### Storage

- `IPhotoStorage` in Application: `SaveAsync(key, content, contentType)`, `OpenAsync(key)`,
  `DeleteAsync(keys)`, `DeleteUploadsAsync()`, and what the seeder needs to make sure a sample
  exists. Keys: `uploads/<photo id>` for an upload, `samples/<name>` for a sample image.
- Infrastructure implements it with `Azure.Storage.Blobs` (the current stable version, looked up)
  on the container `photos`, created at startup when missing.
- `ConnectionStrings:PhotoStorage` is required; the API refuses to start without it, with a
  message that names the setting. `docker-compose.yml` gains an `azurite` service (pinned image,
  blob endpoint only, a health check) and gives the API its connection string;
  `appsettings.Development.json` points `dotnet run` at the same emulator on localhost.
- A rebuild of the sample data deletes every blob under `uploads/` and uploads the sample images
  that are missing, inside the same gated window as the database rebuild.

### `POST /api/v1/orders/{number}/operations/{operationId}/photos`

`multipart/form-data`: `version` (the version the page saw) and `files` (one to five files).

- `201` — `{ "version": 22, "photoCount": 3, "photos": [ { "id": "…", "contentType":
  "image/jpeg", "sizeBytes": 48211 } ], "resolvedAlertTypes": ["MissingPhoto"] }` — the photos
  just added, the operation's new count, the order's new version, and the alerts this upload
  closed (empty when none).
- `400` — no file, more than five, a file over 5 MB, a file that is not a JPEG, PNG or WebP by its
  signature, `version` missing or not positive. The problem names the file.
- `404` — no such order, or no such operation on it.
- `409` — a stale version; the order is not active; the operation would have more than ten
  photos; the demo already holds 200 uploaded photos (they are deleted at the next rebuild).
- `415` — the body is not `multipart/form-data`.
- `429` — more than four uploads are in flight; the route has a concurrency limit and no queue.
- `503` — uploads are switched off (`Photos:UploadsEnabled` is `false`).

The request body is limited to what five files of 5 MB need. A photo or operation id that is not
a UUID is a `400`. Every refusal comes before a byte reaches storage.

### `GET /api/v1/orders/{number}/photos`

`200` — every photo of the order, in the order of the page: shipment position, then the
operation's time, then upload time.

```json
[ { "id": "…", "operationId": "…", "shipmentLabel": "FR001383-1", "operationType": "Unloading",
    "contentType": "image/png", "sizeBytes": 20312, "uploadedAt": "2026-11-17T13:55:00+00:00" } ]
```

`404` — no such order. The detail response does not change, except that each operation now
carries its `id` if it did not.

### `GET /api/v1/orders/{number}/photos/{photoId}`

`200` with the image bytes, `Content-Type` as stored, `Content-Length`, and
`Cache-Control: private, max-age=86400, immutable` (a photo never changes) and
`Cross-Origin-Resource-Policy: same-origin`, so another site cannot embed it. `404` — no such
order or photo on it. `429` — more than 120 requests in a minute: the route has a fixed window,
since its responses are the only large ones the API sends.

### Routes

`ApiRoutes.Orders.Photos`, `.Photo` and `.OperationPhotos`, with `OperationIdParameter` and
`PhotoIdParameter`. Endpoint folders `Orders/ListPhotos`, `Orders/GetPhoto`,
`Orders/AttachPhotos`; application folders the same.

### Page (`order.html`)

- **Add-operation dialog**: the numeric "Photos" field becomes a file input (`multiple`, accepting
  JPEG, PNG and WebP) with the limits said next to it and checked before sending. On submit the
  page records the operation, then uploads the chosen files to it, then loads the order again. If
  the upload fails the operation stays recorded and the toast says why.
- **An operation's row**: "📷 n" is a button that opens the gallery of that operation.
- **The header's 📷 button** opens the gallery of the whole order, grouped by shipment and
  operation. It is no longer marked as outside the assignment.
- **The gallery** is a dialog: thumbnails (the original image, scaled by CSS, loaded lazily) that
  open the full image in a new tab; for one operation of an active order, an "Add photos" control
  that uploads and refreshes the gallery, the order and the Orders badge. An empty gallery says
  so.
- After an upload that closed an alert the toast says "Missing photo alert closed".
- Rules of step 7 hold: no inline script, keyboard reachable, the dialog returns the focus.

### Out of the application code (done with the deployment, not by the implementer)

A storage account and its `photos` container in Azure, the secret `storage-connection` on the
container app, `ConnectionStrings__PhotoStorage` in `deploy/containerapp.yaml`, and the stage in
`docs/deployment.md`.

## Acceptance criteria

- [ ] Recording an Unloading from the page with two photos chosen leaves the operation with
      "📷 2", no Missing photo alert, and both images in its gallery; recorded without photos it
      raises the alert, and adding one photo from the gallery closes it and lowers the Orders
      badge when it was the order's only reason to need attention.
- [ ] On the seeded data every operation's gallery holds as many images as its count; `FR001383`
      shows the same total in its header as before this step.
- [ ] A text file renamed to `.jpg`, a sixth file, a file over 5 MB, an eleventh photo on one
      operation and an upload to a completed order are each refused with a problem that says
      which rule was broken; nothing is stored.
- [ ] A stale version is refused with `409` and leaves no blob behind.
- [ ] After `docker compose restart api` the uploaded photos are gone from the galleries and from
      storage, and the sample images are still served.
- [ ] `GET …/photos/{photoId}` serves the stored type with the cache header; a photo of another
      order under this order's number is `404`.
- [ ] `docker compose up` from a clean clone starts the stack with Azurite and the pages work;
      `dotnet test` starts its own emulator.
- [ ] The OpenAPI document describes the three endpoints; `README.md`, `docs/plan.md`,
      `docs/er-diagram.md`, `CONTEXT.md` and `docs/steps/03-seed.md` say what changed.

## Tests

- `Order.AttachPhotos`: adds the photos and the count, resolves the Missing photo alert only when
  every operation that requires a photo has one, leaves a Quantity delta alert alone, is refused
  on a draft and on a completed order, at the limit, and for an unknown operation — unit.
- Upload: the happy path, each validation rule, the signature check, the stale version with the
  blob removed, the unknown order and operation, the cap on uploaded photos — integration, against
  PostgreSQL and Azurite.
- List and get: the order of the list, the bytes and headers of a photo, the `404`s — integration.
- The seed: photo rows equal the counts, every row's sample exists in storage; a rebuild deletes
  uploads and keeps samples — integration.
- The operations request without `photoCount`: the existing write-flow tests, updated.

## Out of scope

- Deleting or replacing a photo, captions, comments (still a count).
- Thumbnails, image conversion, EXIF handling, virus scanning.
- Sign-in and per-user limits; a managed identity for storage (see the ADR).
