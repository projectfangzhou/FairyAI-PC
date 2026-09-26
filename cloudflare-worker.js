// Cloudflare Worker for FairyAI PC-Mobile relay
// Deploy this to Cloudflare Workers for P-M sync when devices aren't on the same LAN.
//
// Setup:
// 1. Go to Cloudflare Dashboard > Workers & Pages
// 2. Create a new Worker, paste this code
// 3. Set environment variable: FAIRYAI_SECRET (a strong random string)
// 4. Note the worker URL (e.g. https://fairyai-sync.your-subdomain.workers.dev)
// 5. Enter the URL in FairyAI installer "中转网站" field

export default {
  async fetch(request, env) {
    const corsHeaders = {
      'Access-Control-Allow-Origin': '*',
      'Access-Control-Allow-Methods': 'GET, POST, OPTIONS',
      'Access-Control-Allow-Headers': 'Content-Type, Authorization',
    };

    if (request.method === 'OPTIONS') {
      return new Response(null, { headers: corsHeaders });
    }

    // Authentication: require Bearer token matching FAIRYAI_SECRET
    const sharedSecret = env.FAIRYAI_SECRET;
    if (sharedSecret) {
      const auth = request.headers.get('Authorization');
      if (auth !== `Bearer ${sharedSecret}`) {
        return new Response(JSON.stringify({ error: 'Unauthorized' }), {
          status: 401,
          headers: { ...corsHeaders, 'Content-Type': 'application/json' }
        });
      }
    } else {
      // If no secret configured, reject all requests (fail-closed)
      return new Response(JSON.stringify({ error: 'Server not configured' }), {
        status: 503,
        headers: { ...corsHeaders, 'Content-Type': 'application/json' }
      });
    }

    const url = new URL(request.url);

    // POST /sync — receive data from source device
    if (request.method === 'POST' && url.pathname === '/sync') {
      try {
        const data = await request.json();
        // Sanitize deviceId to prevent injection
        const deviceId = String(data.target_device || 'unknown').replace(/[^a-zA-Z0-9_-]/g, '');
        const key = `sync_${deviceId}_${Date.now()}`;
        if (env.FAIRYAI_KV) {
          await env.FAIRYAI_KV.put(key, JSON.stringify(data), {
            expirationTtl: 86400 // 24h TTL
          });
        }
        return new Response(JSON.stringify({
          status: 'ok',
          key: key,
          message: 'Data queued for delivery'
        }), {
          headers: { ...corsHeaders, 'Content-Type': 'application/json' }
        });
      } catch (e) {
        return new Response(JSON.stringify({ error: e.message }), {
          status: 400,
          headers: { ...corsHeaders, 'Content-Type': 'application/json' }
        });
      }
    }

    // GET /poll/:deviceId — check for pending data
    if (request.method === 'GET' && url.pathname.startsWith('/poll/')) {
      const deviceId = url.pathname.split('/')[2]?.replace(/[^a-zA-Z0-9_-]/g, '') || '';
      if (env.FAIRYAI_KV && deviceId) {
        const list = await env.FAIRYAI_KV.list({ prefix: `sync_${deviceId}_` });
        const items = [];
        for (const key of list.keys) {
          const value = await env.FAIRYAI_KV.get(key.name);
          if (value) items.push(JSON.parse(value));
          await env.FAIRYAI_KV.delete(key.name);
        }
        return new Response(JSON.stringify({ items }), {
          headers: { ...corsHeaders, 'Content-Type': 'application/json' }
        });
      }
      return new Response(JSON.stringify({ items: [] }), {
        headers: { ...corsHeaders, 'Content-Type': 'application/json' }
      });
    }

    // GET /health — health check (no auth needed)
    return new Response(JSON.stringify({
      status: 'ok',
      service: 'FairyAI Relay',
      timestamp: new Date().toISOString()
    }), {
      headers: { ...corsHeaders, 'Content-Type': 'application/json' }
    });
  }
};
