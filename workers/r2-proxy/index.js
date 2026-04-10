// ============================================================
// SubashaVentures — R2 Proxy Cloudflare Worker
// ============================================================
// All writes require a valid Supabase JWT.
// Public GETs (store logos, product images) are unauthenticated.
//
// Path ownership rules:
//   users/{user_id}/**        → authenticated user's id must match
//   partners/{partner_id}/**  → user must have matching partner_id claim
//                               in user_metadata, OR be superior_admin
//
// Endpoints:
//   GET    /{objectKey}                → public read
//   GET    /list?prefix=&max_keys=     → authenticated list
//   POST   /presign                    → authenticated presigned upload URL
//   PUT    /upload/{objectKey}         → authenticated direct upload
//   DELETE /delete/{objectKey}         → authenticated delete
// ============================================================

const CORS_HEADERS = {
  "Access-Control-Allow-Origin": "https://www.mysubasha.com",
  "Access-Control-Allow-Methods": "GET, PUT, POST, DELETE, OPTIONS",
  "Access-Control-Allow-Headers": "Authorization, Content-Type, apikey",
  "Access-Control-Max-Age": "86400",
};

const ALLOWED_CONTENT_TYPES = new Set([
  "image/jpeg",
  "image/jpg",
  "image/png",
  "image/webp",
  "image/gif",
]);

const MAX_UPLOAD_SIZE = 5 * 1024 * 1024; // 5 MB

// ── Entry point ───────────────────────────────────────────────

export default {
  async fetch(request, env, ctx) {
    if (request.method === "OPTIONS") {
      return new Response(null, { status: 204, headers: CORS_HEADERS });
    }

    try {
      return await handleRequest(request, env);
    } catch (err) {
      console.error("Unhandled worker error:", err);
      return errorResponse(500, "Internal server error");
    }
  },
};

// ── Router ────────────────────────────────────────────────────

async function handleRequest(request, env) {
  const url = new URL(request.url);
  const pathname = url.pathname;

  // Strip leading slash
  const path = pathname.startsWith("/") ? pathname.slice(1) : pathname;

  // ── Public read ───────────────────────────────────────────
  if (request.method === "GET" && !path.startsWith("list")) {
    return handlePublicGet(path, env);
  }

  // ── All other routes require auth ─────────────────────────
  const user = await authenticateRequest(request, env);
  if (!user) {
    return errorResponse(401, "Unauthorized — invalid or missing token");
  }

  const isAdmin = isAdminUser(user);

  if (request.method === "GET" && path === "list") {
    return handleList(url, env, user, isAdmin);
  }

  if (request.method === "POST" && path === "presign") {
    return handlePresign(request, env, user, isAdmin);
  }

  if (request.method === "PUT" && path.startsWith("upload/")) {
    const objectKey = decodeURIComponent(path.slice("upload/".length));
    return handleUpload(request, objectKey, env, user, isAdmin);
  }

  if (request.method === "DELETE" && path.startsWith("delete/")) {
    const objectKey = decodeURIComponent(path.slice("delete/".length));
    return handleDelete(objectKey, env, user, isAdmin);
  }

  return errorResponse(404, "Route not found");
}

// ── Auth ──────────────────────────────────────────────────────

async function authenticateRequest(request, env) {
  const authHeader = request.headers.get("Authorization");
  if (!authHeader || !authHeader.startsWith("Bearer ")) return null;

  const token = authHeader.slice(7);

  try {
    const response = await fetch(`${env.SUPABASE_URL}/auth/v1/user`, {
      headers: {
        Authorization: `Bearer ${token}`,
        apikey: env.SUPABASE_ANON_KEY,
      },
    });

    if (!response.ok) return null;

    const user = await response.json();
    if (!user || !user.id) return null;

    return user;
  } catch {
    return null;
  }
}

function isAdminUser(user) {
  const role =
    user?.app_metadata?.role ||
    user?.user_metadata?.role ||
    user?.role;
  return role === "superior_admin";
}

// ── Path permission check ──────────────────────────────────────

function canAccessPath(objectKey, user, isAdmin) {
  if (isAdmin) return true;

  // Validate no path traversal
  if (objectKey.includes("..") || objectKey.includes("//")) return false;

  const userId = user.id;
  const partnerId = user?.user_metadata?.partner_id;

  // users/{user_id}/**
  if (objectKey.startsWith(`users/${userId}/`)) return true;

  // partners/{partner_id}/**  — requires partner_id claim set by approve-partner-application edge fn
  if (partnerId && objectKey.startsWith(`partners/${partnerId}/`)) return true;

  return false;
}

// ── Public GET ────────────────────────────────────────────────

async function handlePublicGet(objectKey, env) {
  if (!objectKey) return errorResponse(400, "Missing object key");

  // Block path traversal on public reads too
  if (objectKey.includes("..") || objectKey.includes("//")) {
    return errorResponse(400, "Invalid path");
  }

  const object = await env.R2_BUCKET.get(objectKey);

  if (!object) return errorResponse(404, "Object not found");

  const headers = new Headers({
    ...CORS_HEADERS,
    "Content-Type": object.httpMetadata?.contentType || "application/octet-stream",
    "Cache-Control": "public, max-age=31536000, immutable",
    "ETag": object.httpEtag,
  });

  return new Response(object.body, { headers });
}

// ── List ──────────────────────────────────────────────────────

async function handleList(url, env, user, isAdmin) {
  const prefix = url.searchParams.get("prefix") || "";
  const maxKeys = Math.min(parseInt(url.searchParams.get("max_keys") || "1000"), 1000);

  // Non-admins can only list their own paths
  if (!isAdmin) {
    const userId = user.id;
    const partnerId = user?.user_metadata?.partner_id;

    const allowedPrefixes = [`users/${userId}/`];
    if (partnerId) allowedPrefixes.push(`partners/${partnerId}/`);

    const allowed = allowedPrefixes.some((p) =>
      prefix.startsWith(p) || p.startsWith(prefix)
    );

    if (!allowed) return errorResponse(403, "Access denied to this prefix");
  }

  const listed = await env.R2_BUCKET.list({
    prefix: prefix || undefined,
    limit: maxKeys,
  });

  const objects = listed.objects.map((obj) => ({
    key: obj.key,
    size: obj.size,
    lastModified: obj.uploaded,
    contentType: obj.httpMetadata?.contentType || null,
  }));

  return jsonResponse({ objects, truncated: listed.truncated });
}

// ── Presign ───────────────────────────────────────────────────
// Generates a presigned PUT URL via R2's S3-compatible API.
// Requires R2_ACCOUNT_ID, R2_ACCESS_KEY_ID, R2_SECRET_ACCESS_KEY secrets.

async function handlePresign(request, env, user, isAdmin) {
  let body;
  try {
    body = await request.json();
  } catch {
    return errorResponse(400, "Invalid JSON body");
  }

  const { object_key, content_type, max_size } = body;

  if (!object_key || !content_type) {
    return errorResponse(400, "object_key and content_type are required");
  }

  if (!canAccessPath(object_key, user, isAdmin)) {
    return errorResponse(403, "Access denied to this path");
  }

  if (!ALLOWED_CONTENT_TYPES.has(content_type.toLowerCase())) {
    return errorResponse(400, `Content type not allowed: ${content_type}`);
  }

  try {
    const expiresIn = 300; // 5 minutes
    const presignedUrl = await generatePresignedUrl(
      object_key,
      content_type,
      expiresIn,
      env
    );

    return jsonResponse({ presignedUrl, expiresIn, objectKey: object_key });
  } catch (err) {
    console.error("Presign error:", err);
    return errorResponse(500, "Failed to generate presigned URL");
  }
}

async function generatePresignedUrl(objectKey, contentType, expiresIn, env) {
  const accountId = env.R2_ACCOUNT_ID;
  const accessKeyId = env.R2_ACCESS_KEY_ID;
  const secretAccessKey = env.R2_SECRET_ACCESS_KEY;
  const bucketName = "subasha-ventures";
  const region = "auto";

  const host = `${accountId}.r2.cloudflarestorage.com`;
  const endpoint = `https://${host}/${bucketName}/${objectKey}`;

  const now = new Date();
  const amzDate = now.toISOString().replace(/[:-]|\.\d{3}/g, "").slice(0, 15) + "Z";
  const dateStamp = amzDate.slice(0, 8);

  const credentialScope = `${dateStamp}/${region}/s3/aws4_request`;
  const credential = `${accessKeyId}/${credentialScope}`;

  const queryParams = new URLSearchParams({
    "X-Amz-Algorithm": "AWS4-HMAC-SHA256",
    "X-Amz-Credential": credential,
    "X-Amz-Date": amzDate,
    "X-Amz-Expires": String(expiresIn),
    "X-Amz-SignedHeaders": "host",
  });

  const canonicalQueryString = queryParams.toString();
  const canonicalHeaders = `host:${host}\n`;
  const signedHeaders = "host";

  const canonicalRequest = [
    "PUT",
    `/${bucketName}/${objectKey}`,
    canonicalQueryString,
    canonicalHeaders,
    signedHeaders,
    "UNSIGNED-PAYLOAD",
  ].join("\n");

  const hashedCanonicalRequest = await sha256(canonicalRequest);

  const stringToSign = [
    "AWS4-HMAC-SHA256",
    amzDate,
    credentialScope,
    hashedCanonicalRequest,
  ].join("\n");

  const signingKey = await getSigningKey(secretAccessKey, dateStamp, region);
  const signature = await hmacHex(signingKey, stringToSign);

  queryParams.set("X-Amz-Signature", signature);

  return `${endpoint}?${queryParams.toString()}`;
}

// ── Direct Upload ─────────────────────────────────────────────

async function handleUpload(request, objectKey, env, user, isAdmin) {
  if (!objectKey) return errorResponse(400, "Missing object key");

  if (!canAccessPath(objectKey, user, isAdmin)) {
    return errorResponse(403, "Access denied to this path");
  }

  const contentType = request.headers.get("Content-Type") || "";

  if (!ALLOWED_CONTENT_TYPES.has(contentType.toLowerCase())) {
    return errorResponse(400, `Content type not allowed: ${contentType}`);
  }

  const contentLength = parseInt(request.headers.get("Content-Length") || "0");
  if (contentLength > MAX_UPLOAD_SIZE) {
    return errorResponse(413, `File too large. Maximum is ${MAX_UPLOAD_SIZE / 1024 / 1024} MB`);
  }

  const body = await request.arrayBuffer();

  if (body.byteLength > MAX_UPLOAD_SIZE) {
    return errorResponse(413, "File too large");
  }

  await env.R2_BUCKET.put(objectKey, body, {
    httpMetadata: { contentType },
  });

  return jsonResponse({
    success: true,
    objectKey,
    size: body.byteLength,
  });
}

// ── Delete ────────────────────────────────────────────────────

async function handleDelete(objectKey, env, user, isAdmin) {
  if (!objectKey) return errorResponse(400, "Missing object key");

  if (!canAccessPath(objectKey, user, isAdmin)) {
    return errorResponse(403, "Access denied to this path");
  }

  await env.R2_BUCKET.delete(objectKey);

  return jsonResponse({ success: true, objectKey });
}

// ── Crypto helpers (AWS4 signing) ─────────────────────────────

async function sha256(message) {
  const encoder = new TextEncoder();
  const data = encoder.encode(message);
  const hashBuffer = await crypto.subtle.digest("SHA-256", data);
  return Array.from(new Uint8Array(hashBuffer))
    .map((b) => b.toString(16).padStart(2, "0"))
    .join("");
}

async function hmac(key, message) {
  const encoder = new TextEncoder();
  const keyData = typeof key === "string" ? encoder.encode(key) : key;
  const cryptoKey = await crypto.subtle.importKey(
    "raw",
    keyData,
    { name: "HMAC", hash: "SHA-256" },
    false,
    ["sign"]
  );
  const signature = await crypto.subtle.sign(
    "HMAC",
    cryptoKey,
    encoder.encode(message)
  );
  return new Uint8Array(signature);
}

async function hmacHex(key, message) {
  const sig = await hmac(key, message);
  return Array.from(sig)
    .map((b) => b.toString(16).padStart(2, "0"))
    .join("");
}

async function getSigningKey(secretKey, dateStamp, region) {
  const kDate = await hmac(`AWS4${secretKey}`, dateStamp);
  const kRegion = await hmac(kDate, region);
  const kService = await hmac(kRegion, "s3");
  return hmac(kService, "aws4_request");
}

// ── Response helpers ──────────────────────────────────────────

function jsonResponse(data, status = 200) {
  return new Response(JSON.stringify(data), {
    status,
    headers: { ...CORS_HEADERS, "Content-Type": "application/json" },
  });
}

function errorResponse(status, message) {
  return new Response(JSON.stringify({ success: false, error: message }), {
    status,
    headers: { ...CORS_HEADERS, "Content-Type": "application/json" },
  });
}
