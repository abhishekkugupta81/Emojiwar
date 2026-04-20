type HttpMethod = "GET" | "POST" | "PATCH";

function getEnv(name: string): string {
  const value = Deno.env.get(name);
  if (!value) {
    throw new Error(`Missing required environment variable: ${name}`);
  }

  return value;
}

function buildServiceHeaders(includeJsonContentType = false): HeadersInit {
  const serviceRoleKey = getEnv("SUPABASE_SERVICE_ROLE_KEY");
  const headers: Record<string, string> = {
    apikey: serviceRoleKey,
    Authorization: `Bearer ${serviceRoleKey}`,
  };

  if (includeJsonContentType) {
    headers["Content-Type"] = "application/json";
  }

  return headers;
}

async function restRequest<T>(method: HttpMethod, path: string, body?: unknown, extraHeaders?: HeadersInit): Promise<T> {
  const supabaseUrl = getEnv("SUPABASE_URL");
  const headers = new Headers(buildServiceHeaders(body !== undefined));

  if (extraHeaders) {
    const providedHeaders = new Headers(extraHeaders);
    for (const [key, value] of providedHeaders.entries()) {
      headers.set(key, value);
    }
  }

  const response = await fetch(`${supabaseUrl}/rest/v1/${path}`, {
    method,
    headers,
    body: body === undefined ? undefined : JSON.stringify(body),
  });

  if (!response.ok) {
    throw new Error(`Supabase REST request failed (${response.status}): ${await response.text()}`);
  }

  return await response.json() as T;
}

export async function resolveRequestUserId(request: Request): Promise<string> {
  const authHeader = request.headers.get("Authorization");
  if (!authHeader) {
    throw new Error("Missing Authorization header.");
  }

  const supabaseUrl = getEnv("SUPABASE_URL");
  const response = await fetch(`${supabaseUrl}/auth/v1/user`, {
    headers: {
      apikey: getEnv("SUPABASE_SERVICE_ROLE_KEY"),
      Authorization: authHeader,
    },
  });

  if (!response.ok) {
    throw new Error(`Unable to resolve request user (${response.status}).`);
  }

  const payload = await response.json() as { id?: string };
  if (!payload.id) {
    throw new Error("Authenticated user id was missing from auth response.");
  }

  return payload.id;
}

export async function selectRows<T>(path: string): Promise<T[]> {
  return await restRequest<T[]>("GET", path);
}

export async function insertRows<T>(path: string, body: unknown): Promise<T[]> {
  return await restRequest<T[]>("POST", path, body, { Prefer: "return=representation" });
}

export async function patchRows<T>(path: string, body: unknown): Promise<T[]> {
  return await restRequest<T[]>("PATCH", path, body, { Prefer: "return=representation" });
}
