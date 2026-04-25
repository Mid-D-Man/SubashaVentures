// workers/r2-proxy/index.js
// ============================================================
// SubashaVentures — R2 Proxy Cloudflare Worker
// ============================================================

const ALLOWED_ORIGINS = [
  "https://mysubasha.com",
  "https://www.mysubasha.com",
];

function getCorsHeaders(request) {
  const origin = request.headers.get("Origin") || "";
  const allowedOrigin = ALLOWED_ORIGINS.includes(origin)
    ? origin
    : ALLOWED_ORIGINS[0];
  return {
    "Access-Control-Allow-Origin": allowedOrigin,
    "Access-Control-Allow-Methods": "GET, PUT, POST, DELETE, OPTIONS",
    "Access-Control-Allow-Headers": "Authorization, Content-Type, apikey",
    "Access-Control-Max-Age": "86400",
    "Vary": "Origin",
  };
}

const ALLOWED_CONTENT_TYPES = new Set([
  "image/jpeg",
  "image/jpg",
  "image/png",
  "image/webp",
  "image/gif",
]);

const MAX_UPLOAD_SIZE = 5 * 1024 * 1024; // 5 MB

export default {
  async fetch(request, env, ctx) {
    if (request.method === "OPTIONS") {
      return new Response(null, {
        status: 204,
        headers: getCorsHeaders(request),
      });
    }

    try {
      return await handleRequest(request, env);
    } catch (err) {
      console.error("Unhandled worker error:", err);
      return errorResponse(request, 500, "Internal server error");
    }
  },
};

async function handleRequest(request, env) {
  const url = new URL(request.url);
  const pathname = url.pathname;
  const path = pathname.startsWith("/") ? pathname.slice(1) : pathname;

  // Public read — no auth required
  if (request.method === "GET" && !path.startsWith("list")) {
    return handlePublicGet(request, path, env);
  }

  const user = await authenticateRequest(request, env);
  if (!user) {
    return errorResponse(request, 401, "Unauthorized — invalid or missing token");
  }

  const isAdmin = isAdminUser(user);

  if (request.method === "GET" && path === "list") {
    return handleList(request, url, env, user, isAdmin);
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
    return handleDelete(request, objectKey, env, user, isAdmin);
  }

  return errorResponse(request, 404, "Route not found");
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

function canAccessPath(objectKey, user, isAdmin) {
  if (isAdmin) return true;
  if (objectKey.includes("..") || objectKey.includes("//")) return false;

  const userId = user.id;
  const partnerId = user?.user_metadata?.partner_id;

  if (objectKey.startsWith(`users/${userId}/`)) return true;
  if (partnerId && objectKey.startsWith(`partners/${partnerId}/`)) return true;

  return false;
}

// ── Public GET ────────────────────────────────────────────────

async function handlePublicGet(request, objectKey, env) {
  if (!objectKey) return errorResponse(request, 400, "Missing object key");
  if (objectKey.includes("..") || objectKey.includes("//")) {
    return errorResponse(request, 400, "Invalid path");
  }

  const object = await env.R2_BUCKET.get(objectKey);
  if (!object) return errorResponse(request, 404, "Object not found");

  const headers = new Headers({
    ...getCorsHeaders(request),
    "Content-Type": object.httpMetadata?.contentType || "application/octet-stream",
    "Cache-Control": "public, max-age=31536000, immutable",
    "ETag": object.httpEtag,
  });

  return new Response(object.body, { headers });
}

// ── List ──────────────────────────────────────────────────────

async function handleList(request, url, env, user, isAdmin) {
  const prefix = url.searchParams.get("prefix") || "";
  const maxKeys = Math.min(parseInt(url.searchParams.get("max_keys") || "1000"), 1000);

  if (!isAdmin) {
    const userId = user.id;
    const partnerId = user?.user_metadata?.partner_id;
    const allowedPrefixes = [`users/${userId}/`];
    if (partnerId) allowedPrefixes.push(`partners/${partnerId}/`);
    const allowed = allowedPrefixes.some(
      (p) => prefix.startsWith(p) || p.startsWith(prefix)
    );
    if (!allowed) return errorResponse(request, 403, "Access denied to this prefix");
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

  return jsonResponse(request, { objects, truncated: listed.truncated });
}

// ── Presign ───────────────────────────────────────────────────

async function handlePresign(request, env, user, isAdmin) {
  let body;
  try {
    body = await request.json();
  } catch {
    return errorResponse(request, 400, "Invalid JSON body");
  }

  const { object_key, content_type, max_size } = body;
  if (!object_key || !content_type) {
    return errorResponse(request, 400, "object_key and content_type are required");
  }
  if (!canAccessPath(object_key, user, isAdmin)) {
    return errorResponse(request, 403, "Access denied to this path");
  }
  if (!ALLOWED_CONTENT_TYPES.has(content_type.toLowerCase())) {
    return errorResponse(request, 400, `Content type not allowed: ${content_type}`);
  }

  try {
    const expiresIn = 300;
    const presignedUrl = await generatePresignedUrl(object_key, content_type, expiresIn, env);
    return jsonResponse(request, { presignedUrl, expiresIn, objectKey: object_key });
  } catch (err) {
    console.error("Presign error:", err);
    return errorResponse(request, 500, "Failed to generate presigned URL");
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
  if (!objectKey) return errorResponse(request, 400, "Missing object key");
  if (!canAccessPath(objectKey, user, isAdmin)) {
    return errorResponse(request, 403, "Access denied to this path");
  }

  const contentType = request.headers.get("Content-Type") || "";
  if (!ALLOWED_CONTENT_TYPES.has(contentType.toLowerCase())) {
    return errorResponse(request, 400, `Content type not allowed: ${contentType}`);
  }

  const contentLength = parseInt(request.headers.get("Content-Length") || "0");
  if (contentLength > MAX_UPLOAD_SIZE) {
    return errorResponse(request, 413, `File too large. Max is ${MAX_UPLOAD_SIZE / 1024 / 1024} MB`);
  }

  const body = await request.arrayBuffer();
  if (body.byteLength > MAX_UPLOAD_SIZE) {
    return errorResponse(request, 413, "File too large");
  }

  await env.R2_BUCKET.put(objectKey, body, {
    httpMetadata: { contentType },
  });

  return jsonResponse(request, { success: true, objectKey, size: body.byteLength });
}

// ── Delete ────────────────────────────────────────────────────

async function handleDelete(request, objectKey, env, user, isAdmin) {
  if (!objectKey) return errorResponse(request, 400, "Missing object key");
  if (!canAccessPath(objectKey, user, isAdmin)) {
    return errorResponse(request, 403, "Access denied to this path");
  }

  await env.R2_BUCKET.delete(objectKey);
  return jsonResponse(request, { success: true, objectKey });
}

// ── Crypto helpers ────────────────────────────────────────────

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
    "raw", keyData, { name: "HMAC", hash: "SHA-256" }, false, ["sign"]
  );
  const signature = await crypto.subtle.sign("HMAC", cryptoKey, encoder.encode(message));
  return new Uint8Array(signature);
}

async function hmacHex(key, message) {
  const sig = await hmac(key, message);
  return Array.from(sig).map((b) => b.toString(16).padStart(2, "0")).join("");
}

async function getSigningKey(secretKey, dateStamp, region) {
  const kDate = await hmac(`AWS4${secretKey}`, dateStamp);
  const kRegion = await hmac(kDate, region);
  const kService = await hmac(kRegion, "s3");
  return hmac(kService, "aws4_request");
}

// ── Response helpers ──────────────────────────────────────────

function jsonResponse(request, data, status = 200) {
  return new Response(JSON.stringify(data), {
    status,
    headers: { ...getCorsHeaders(request), "Content-Type": "application/json" },
  });
}

function errorResponse(request, status, message) {
  return new Response(JSON.stringify({ success: false, error: message }), {
    status,
    headers: { ...getCorsHeaders(request), "Content-Type": "application/json" },
  });
}
