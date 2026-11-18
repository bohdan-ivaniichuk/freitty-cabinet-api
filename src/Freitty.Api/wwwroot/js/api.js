// Every request of the pages goes through here. A failure becomes an ApiError carrying what the
// ProblemDetails of the API says: status, code, title, detail and the field errors of a 400.

export class ApiError extends Error {
  constructor(status, problem) {
    super(problem.detail || problem.title);
    this.name = 'ApiError';
    this.status = status;
    this.code = problem.code ?? null;
    this.title = problem.title;
    this.detail = problem.detail ?? '';
    this.errors = problem.errors ?? [];
  }
}

// The address of an API path with its query. Parameters that are null or undefined are left out.
export function apiUrl(path, parameters = {}) {
  const url = new URL(path, location.href);
  for (const [name, value] of Object.entries(parameters)) {
    if (value !== null && value !== undefined) {
      url.searchParams.set(name, value);
    }
  }

  return url;
}

export function getJson(path, parameters = {}) {
  return request(apiUrl(path, parameters), { headers: { Accept: 'application/json' } });
}

export function postJson(path, body) {
  return request(new URL(path, location.href), {
    method: 'POST',
    headers: { Accept: 'application/json', 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  });
}

async function request(url, init) {
  let response;
  try {
    response = await fetch(url, init);
  } catch {
    throw new ApiError(0, {
      title: 'Network error',
      detail: 'The server could not be reached. Check the connection and reload the page.',
    });
  }

  const body = await readJson(response);
  if (!response.ok) {
    const fallback = { title: `${response.status} ${response.statusText}`.trim() };
    throw new ApiError(response.status, body ?? fallback);
  }

  return body;
}

async function readJson(response) {
  const contentType = response.headers.get('Content-Type') ?? '';
  return contentType.includes('json') ? response.json() : null;
}
