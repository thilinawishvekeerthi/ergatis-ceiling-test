# Items API

A small REST API for managing a collection of items, built with [Express](https://expressjs.com/).
Storage is in-memory, so data resets whenever the process restarts.

## Requirements

- Node.js 18+ (tested on Node 22)

## Setup

```bash
npm install
```

## Running the server

```bash
npm start
```

The API listens on `http://localhost:3000` by default. Override the port with
the `PORT` environment variable:

```bash
PORT=8080 npm start
```

## Tests

```bash
npm test
```

Tests use [Jest](https://jestjs.io/) and [supertest](https://github.com/ladjs/supertest)
and exercise all endpoints through the Express app without binding a port.

## Endpoints

| Method   | Path          | Description            | Success | Errors       |
|----------|---------------|------------------------|---------|--------------|
| `GET`    | `/items`      | List all items         | `200`   | —            |
| `GET`    | `/items/:id`  | Get a single item      | `200`   | `404`        |
| `POST`   | `/items`      | Create a new item      | `201`   | `400`        |
| `PUT`    | `/items/:id`  | Replace an item        | `200`   | `400`, `404` |
| `DELETE` | `/items/:id`  | Delete an item         | `204`   | `404`        |

### Item shape

```json
{
  "id": "f47ac10b-58cc-4372-a567-0e02b2c3d479",
  "name": "Widget",
  "description": "A useful widget"
}
```

- `id` — server-generated UUID (read-only).
- `name` — required, non-empty string. Leading/trailing whitespace is trimmed.
- `description` — optional string, trimmed; defaults to `""`.

### Examples

Create an item:

```bash
curl -s -X POST http://localhost:3000/items \
  -H 'Content-Type: application/json' \
  -d '{"name":"Widget","description":"A useful widget"}'
```

List items:

```bash
curl -s http://localhost:3000/items
```

Update an item:

```bash
curl -s -X PUT http://localhost:3000/items/<id> \
  -H 'Content-Type: application/json' \
  -d '{"name":"Widget Pro","description":"Better"}'
```

Delete an item:

```bash
curl -s -o /dev/null -w '%{http_code}\n' -X DELETE http://localhost:3000/items/<id>
# 204
```

### Error responses

Errors are returned as a JSON object with an `error` field:

```json
{ "error": "Item not found" }
```

- `400` — missing or empty `name` on `POST`/`PUT`.
- `404` — no item matches the given `id` on `GET`/`PUT`/`DELETE`.

## Project layout

```
.
├── package.json
├── README.md
├── src
│   ├── app.js      # Express app and route handlers
│   └── server.js   # Starts the HTTP server
└── test
    └── items.test.js
```
